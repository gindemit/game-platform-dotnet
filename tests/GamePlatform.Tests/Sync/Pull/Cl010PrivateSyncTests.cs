#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Sync.Pull;

namespace GamePlatform.Tests.Sync.Pull
{
    public sealed class Cl010PrivateSyncTests
    {
        private static readonly StorageScope Scope=new StorageScope("test-backend",new PlatformId("01890f3e-7a6b-7c8d-9e0f-102030405060"),new PlatformId("00112233-4455-4677-8899-aabbccddeeff"));
        private static readonly ClientStreamId Stream=new ClientStreamId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
        private static readonly Guid Epoch=Guid.Parse("0199f9a0-aaaa-7777-8888-999999999999");
        private static readonly IReadOnlyList<SqliteMigration> Migrations=new[]{SqliteOutboxMigration.Create(1),new SqliteMigration(2,"test-overlay",new[]{"CREATE TABLE test_overlay (name TEXT PRIMARY KEY, value INTEGER NOT NULL)"}),SqlitePrivateSyncMigration.Create(3)};

        [Fact]
        public async Task MultiPageBootstrapIsInvisibleUntilFinalAtomicInstall()
        {
            using var files=new TemporaryDatabase();var db=await Open(files.Path);var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1)),CancellationToken.None);
            await store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,true,Token(2),null,Mutation("profile","self",1,1)),CancellationToken.None);
            Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection"));Assert.Equal(0,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));
            await store.StageBootstrapPageAsync(Page(Token(1),Token(2),5,false,null,Token(3),Mutation("inventory","item:1",2,2)),CancellationToken.None);
            Assert.Equal(2,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection"));Assert.Equal(1,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(Token(3),checkpoint.CopyCursor());await Dispose(db);
        }

        [Fact]
        public async Task InterruptedFinalInstallRollsBackAndResumesAfterReopen()
        {
            using var files=new TemporaryDatabase();var db=await Open(files.Path);var normal=Store(db);await normal.BeginBootstrapAsync(Boundary(5,Token(1)),CancellationToken.None);await normal.StageBootstrapPageAsync(Page(Token(1),Token(1),5,true,Token(2),null,Mutation("profile","old",1,1)),CancellationToken.None);
            var crashing=new SqlitePrivateSyncStore(db,Scope,_=>{},p=>{if(p==SyncCheckpoint.BeforeBootstrapInstall)throw new InjectedFailure();});await Assert.ThrowsAsync<InjectedFailure>(()=>crashing.StageBootstrapPageAsync(Page(Token(1),Token(2),5,false,null,Token(3),Mutation("wallet","coins",1,2)),CancellationToken.None));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection"));await Dispose(db);
            db=await SqliteDatabase.OpenAsync(files.Path,Scope,Migrations,CancellationToken.None);normal=Store(db);var progress=await normal.GetBootstrapProgressAsync(CancellationToken.None);Assert.NotNull(progress);Assert.Equal(Token(2),progress!.CopyPageToken());await normal.StageBootstrapPageAsync(Page(Token(1),Token(2),5,false,null,Token(3),Mutation("wallet","coins",1,2)),CancellationToken.None);Assert.Equal(2,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection"));await Dispose(db);
        }

        [Fact]
        public async Task PullUsesOneFixedBoundaryAndAdvancesOnlyFromServerCursor()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var store=Store(db);await store.ApplyPullPageAsync(new StoredPullPage(9,new[]{new StoredPullGroup(7,new[]{Mutation("profile","self",2,7)})},Token(4),true),CancellationToken.None);var mid=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(7,mid.CommittedThrough);Assert.Equal(9,mid.FixedThrough);Assert.Equal(Token(4),mid.CopyCursor());
            await store.ApplyPullPageAsync(new StoredPullPage(9,Array.Empty<StoredPullGroup>(),Token(5),false),CancellationToken.None);var done=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(9,done.CommittedThrough);Assert.Null(done.FixedThrough);Assert.Equal(Token(5),done.CopyCursor());await Dispose(db);
        }

        [Fact]
        public async Task InvalidOrStalePullCannotRegressProjectionOrCheckpoint()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var store=Store(db);await store.ApplyPullPageAsync(new StoredPullPage(6,new[]{new StoredPullGroup(6,new[]{Mutation("profile","self",5,9)})},Token(4),false),CancellationToken.None);
            await Assert.ThrowsAsync<StorageException>(()=>store.ApplyPullPageAsync(new StoredPullPage(8,new[]{new StoredPullGroup(8,new[]{new StoredProjectionMutation("unknown","x",1,StoredProjectionKind.Upsert,new byte[]{1})})},Token(5),false),CancellationToken.None));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(6,checkpoint.CommittedThrough);Assert.Equal(5,await Long(db,"SELECT entity_revision FROM gp_confirmed_projection WHERE collection='profile' AND entity_key='self'"));
            await store.ApplyPullPageAsync(new StoredPullPage(7,new[]{new StoredPullGroup(7,new[]{Mutation("profile","self",4,3)})},Token(6),false),CancellationToken.None);Assert.Equal(5,await Long(db,"SELECT entity_revision FROM gp_confirmed_projection WHERE collection='profile' AND entity_key='self'"));await Dispose(db);
        }

        [Fact]
        public async Task ResetReplacesConfirmedViewButPreservesPendingCommandsAndOverlay()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;s.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,semantic_body,fingerprint,local_revision) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)","0199f9a0-bbbb-7777-8888-999999999999",Scope.BackendNamespace,Scope.AppId.Value,Scope.AccountId.Value,Stream.ToString(),1,"pending-run","profile.patch",1,1,new byte[]{1},new byte[]{2},1);s.Execute("INSERT INTO test_overlay VALUES ('pending',42)");return true;},CancellationToken.None);
            var remote=new ScriptedRemote(Bootstrap(10,Token(7)),PageRemote(Token(1),10,false,null,Token(8),new RemoteProjectionMutation("wallet","coins",10,ProjectionMutationKind.Upsert,new byte[]{10})),RemotePullPage.Reset("history_expired"));var coordinator=new PrivateSyncCoordinator(new SqlitePrivateSyncStore(db,Scope,t=>((SqliteTransactionSession)t).Execute("UPDATE test_overlay SET value=value+1 WHERE name='pending'")),remote,()=>100);Assert.Equal(PrivateSyncResult.ResetReconciled,await coordinator.PullOnceAsync(CancellationToken.None));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='pending'"));Assert.Equal(43,await Scalar(db,"SELECT value FROM test_overlay WHERE name='pending'"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE collection='wallet' AND entity_key='coins'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));await Dispose(db);
        }

        [Fact]
        public async Task LostSnapshotPageLeavesOldViewAndDurableResumeToken()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var remote=new ScriptedRemote(Bootstrap(10,Token(7)),null,RemotePullPage.Reset("visibility_changed"));var coordinator=new PrivateSyncCoordinator(Store(db),remote,()=>100);Assert.Equal(PrivateSyncResult.RemoteFailure,await coordinator.PullOnceAsync(CancellationToken.None));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));var progress=await Store(db).GetBootstrapProgressAsync(CancellationToken.None);Assert.NotNull(progress);Assert.Equal(Token(7),progress!.CopyPageToken());await Dispose(db);
        }

        private static BootstrapBoundary Boundary(long through,byte[] first)=>new BootstrapBoundary(Stream,through,1,Epoch,1000,new[]{"profile","progression","inventory","wallet","entitlements"},Token(1),first);
        private static BootstrapStart Bootstrap(long through,byte[] first)=>new BootstrapStart(Stream,0,1,through,1,Epoch,new[]{new SnapshotCollection("profile",1,true),new SnapshotCollection("progression",1,true),new SnapshotCollection("inventory",1,true),new SnapshotCollection("wallet",1,true),new SnapshotCollection("entitlements",1,true)},Token(1),first,1000);
        private static StagedBootstrapPage Page(byte[] session,byte[] requested,long through,bool more,byte[]? next,byte[]? cursor,params StoredProjectionMutation[] entities)=>new StagedBootstrapPage(session,requested,through,entities,more,next,cursor);
        private static BootstrapPage PageRemote(byte[] session,long through,bool more,byte[]? next,byte[]? cursor,params RemoteProjectionMutation[] entities)=>new BootstrapPage(session,through,entities,more,next,cursor);
        private static StoredProjectionMutation Mutation(string collection,string key,long revision,byte value)=>new StoredProjectionMutation(collection,key,revision,StoredProjectionKind.Upsert,new[]{value});
        private static byte[] Token(byte value){var token=new byte[12];Array.Fill(token,value);return token;}
        private static async Task<SqliteDatabase> Open(string path){var db=await SqliteDatabase.OpenAsync(path,Scope,Migrations,CancellationToken.None);await db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;if(s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state")==0)s.Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,0,1,0,0)",Scope.BackendNamespace,Scope.AppId.Value,Scope.AccountId.Value,Stream.ToString());return true;},CancellationToken.None);return db;}
        private static SqlitePrivateSyncStore Store(SqliteDatabase db)=>new SqlitePrivateSyncStore(db,Scope,_=>{});
        private static async Task<SqliteDatabase> OpenReady(string path){var db=await Open(path);var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1)),CancellationToken.None);await store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3),Mutation("profile","self",1,1)),CancellationToken.None);return db;}
        private static Task<int> Scalar(SqliteDatabase db,string sql)=>db.ExecuteAsync(Scope,t=>((SqliteTransactionSession)t).ExecuteScalar<int>(sql),CancellationToken.None);
        private static Task<long> Long(SqliteDatabase db,string sql)=>db.ExecuteAsync(Scope,t=>((SqliteTransactionSession)t).ExecuteScalar<long>(sql),CancellationToken.None);
        private static async Task Dispose(SqliteDatabase db)=>Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
        private sealed class InjectedFailure:Exception{}
        private sealed class ScriptedRemote:IPrivateSyncRemote
        {
            private readonly BootstrapStart start;private readonly BootstrapPage? page;private readonly RemotePullPage pull;
            public ScriptedRemote(BootstrapStart start,BootstrapPage? page,RemotePullPage pull){this.start=start;this.page=page;this.pull=pull;}
            public Task<RemoteResult<BootstrapStart>> StartBootstrapAsync(ClientStreamId streamId,CancellationToken token)=>Task.FromResult(RemoteResult<BootstrapStart>.Success(start));
            public Task<RemoteResult<BootstrapPage>> GetBootstrapPageAsync(byte[] session,byte[] pageToken,int maximumBytes,CancellationToken token)=>Task.FromResult(page==null?RemoteResult<BootstrapPage>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)):RemoteResult<BootstrapPage>.Success(page));
            public Task<RemoteResult<RemotePullPage>> PullAsync(byte[] cursor,int maximumBytes,CancellationToken token)=>Task.FromResult(RemoteResult<RemotePullPage>.Success(pull));
        }
        private sealed class TemporaryDatabase:IDisposable{private readonly string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"game-platform-cl010",Guid.NewGuid().ToString("N"));public TemporaryDatabase(){Directory.CreateDirectory(directory);Path=System.IO.Path.Combine(directory,"platform.sqlite3");}public string Path{get;}public void Dispose(){try{if(Directory.Exists(directory))Directory.Delete(directory,true);}catch(IOException){}}}
    }
}
