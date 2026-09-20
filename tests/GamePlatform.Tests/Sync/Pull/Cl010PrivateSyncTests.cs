#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly Guid Installation=Guid.Parse("0199f9a0-1212-7777-8888-999999999999");
        private static readonly Guid Epoch=Guid.Parse("0199f9a0-aaaa-7777-8888-999999999999");
        private static readonly IReadOnlyList<SqliteMigration> Migrations=SqlitePlatformMigrationRegistry.Migrations.Concat(new[]{new SqliteMigration(7,"test-overlay",new[]{"CREATE TABLE test_overlay (name TEXT PRIMARY KEY, value INTEGER NOT NULL)"})}).ToArray();

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
            var crashing=new SqlitePrivateSyncStore(db,Scope,NoopProjector.Instance,_=>{},p=>{if(p==SyncCheckpoint.BeforeBootstrapInstall)throw new InjectedFailure();});await Assert.ThrowsAsync<InjectedFailure>(()=>crashing.StageBootstrapPageAsync(Page(Token(1),Token(2),5,false,null,Token(3),Mutation("wallet","coins",1,2)),CancellationToken.None));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection"));await Dispose(db);
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
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;s.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,installation_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,client_created_at,semantic_body,fingerprint,local_revision) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)","0199f9a0-bbbb-7777-8888-999999999999",Scope.BackendNamespace,Scope.AppId.Value,Scope.AccountId.Value,Stream.ToString(),Installation.ToString("D"),1,"pending-run","profile.patch",1,1,100L,new byte[]{1},new byte[]{2},1);s.Execute("UPDATE gp_stream_state SET next_sequence=2,local_revision=1 WHERE singleton=1");s.Execute("INSERT INTO test_overlay VALUES ('pending',42)");return true;},CancellationToken.None);
            var remote=new ScriptedRemote(Bootstrap(10,Token(7)),PageRemote(Token(1),10,false,null,Token(8),new RemoteProjectionMutation("wallet","coins",10,ProjectionMutationKind.Upsert,new byte[]{10})),RemotePullPage.Reset("history_expired"));var coordinator=new PrivateSyncCoordinator(new SqlitePrivateSyncStore(db,Scope,NoopProjector.Instance,t=>((SqliteTransactionSession)t).Execute("UPDATE test_overlay SET value=value+1 WHERE name='pending'")),remote,()=>100);Assert.Equal(PrivateSyncResult.ResetReconciled,await coordinator.PullOnceAsync(CancellationToken.None));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='pending'"));Assert.Equal(43,await Scalar(db,"SELECT value FROM test_overlay WHERE name='pending'"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE collection='wallet' AND entity_key='coins'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));await Dispose(db);
        }

        [Fact]
        public async Task LostSnapshotPageLeavesOldViewAndDurableResumeToken()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var remote=new ScriptedRemote(Bootstrap(10,Token(7)),null,RemotePullPage.Reset("visibility_changed"));var coordinator=new PrivateSyncCoordinator(Store(db),remote,()=>100);Assert.Equal(PrivateSyncResult.RemoteFailure,await coordinator.PullOnceAsync(CancellationToken.None));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));var progress=await Store(db).GetBootstrapProgressAsync(CancellationToken.None);Assert.NotNull(progress);Assert.Equal(Token(7),progress!.CopyPageToken());await Dispose(db);
        }

        [Fact]
        public async Task ExpiredStagedSessionIsReplacedByNewlyAuthorizedBootstrap()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var store=Store(db);await store.BeginBootstrapAsync(new BootstrapBoundary(Stream,"active",0,1,9,2,Epoch,50,new[]{"profile","progression","inventory","wallet","entitlements"},Token(9),Token(7)),CancellationToken.None);var remote=new ScriptedRemote(Bootstrap(10,Token(7)),PageRemote(Token(1),10,false,null,Token(8),new RemoteProjectionMutation("wallet","fresh",10,ProjectionMutationKind.Upsert,new byte[]{10})),RemotePullPage.Reset("history_expired"));var coordinator=new PrivateSyncCoordinator(store,remote,()=>100);Assert.Equal(PrivateSyncResult.BootstrapInstalled,await coordinator.BootstrapAsync(Stream,CancellationToken.None));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(10,checkpoint.CommittedThrough);Assert.Equal(Token(8),checkpoint.CopyCursor());Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='fresh'"));await Dispose(db);
        }

        [Fact]
        public async Task OversizedPullGroupRollsBackProjectionAndCursor()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var store=Store(db);var oversized=new StoredProjectionMutation("inventory","large",2,StoredProjectionKind.Upsert,new byte[262_145]);await Assert.ThrowsAsync<StorageException>(()=>store.ApplyPullPageAsync(new StoredPullPage(6,new[]{new StoredPullGroup(6,new[]{oversized})},Token(4),false),CancellationToken.None));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='large'"));await Dispose(db);
        }

        [Fact]
        public async Task LaterPullPageCannotReplayARevisionBeforeDurableCheckpoint()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var store=Store(db);await store.ApplyPullPageAsync(new StoredPullPage(9,new[]{new StoredPullGroup(7,new[]{Mutation("profile","self",2,7)})},Token(4),true),CancellationToken.None);await Assert.ThrowsAsync<StorageException>(()=>store.ApplyPullPageAsync(new StoredPullPage(9,new[]{new StoredPullGroup(6,new[]{Mutation("profile","self",3,6)})},Token(5),true),CancellationToken.None));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(7,checkpoint.CommittedThrough);Assert.Equal(Token(4),checkpoint.CopyCursor());Assert.Equal(2,await Long(db,"SELECT entity_revision FROM gp_confirmed_projection WHERE entity_key='self'"));await Dispose(db);
        }

        [Fact]
        public async Task BootstrapRejectsServerAheadAndLocalCommandGap()
        {
            using(var files=new TemporaryDatabase()){var db=await Open(files.Path);var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1),"active",1,2),CancellationToken.None);await Assert.ThrowsAsync<StorageException>(()=>store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3)),CancellationToken.None));Assert.Equal(0,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));await Dispose(db);}
            using(var files=new TemporaryDatabase()){var db=await Open(files.Path);await ConfigureStream(db,0,3,"pending");var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1),"active",0,1),CancellationToken.None);await Assert.ThrowsAsync<StorageException>(()=>store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3)),CancellationToken.None));Assert.Equal(0,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));await Dispose(db);}
        }

        [Fact]
        public async Task BootstrapPreservesAcceptedRejectedUncertainAndPendingContinuity()
        {
            using var files=new TemporaryDatabase();var db=await Open(files.Path);await ConfigureStream(db,2,5,"accepted","terminal_rejected","in_flight","pending");var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1),"active",2,3),CancellationToken.None);await store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3),Mutation("profile","self",1,1)),CancellationToken.None);Assert.Equal(1,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='accepted'"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='terminal_rejected'"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='in_flight'"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='pending'"));await Dispose(db);
        }

        [Fact]
        public async Task RetiredStreamDisablesCommandsAndRejectsPendingWork()
        {
            using(var files=new TemporaryDatabase()){var db=await Open(files.Path);var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1),"retired",0,null),CancellationToken.None);await store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3)),CancellationToken.None);Assert.Equal(0,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));Assert.Equal(5,(await store.GetPullCheckpointAsync(CancellationToken.None)).CommittedThrough);await Dispose(db);}
            using(var files=new TemporaryDatabase()){var db=await Open(files.Path);await ConfigureStream(db,0,2,"pending");var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1),"retired",0,null),CancellationToken.None);await Assert.ThrowsAsync<StorageException>(()=>store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3)),CancellationToken.None));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='pending'"));await Dispose(db);}
        }

        [Fact]
        public async Task FrozenStreamBoundaryRejectsRetiredSuccessorAndExhaustedActive()
        {
            Assert.Throws<ArgumentException>(()=>new BootstrapStart(Stream,"retired",0,1,5,1,Epoch,Collections(),Token(1),Token(1),1000));
            Assert.Throws<ArgumentException>(()=>new BootstrapStart(Stream,"active",long.MaxValue,null,5,1,Epoch,Collections(),Token(1),Token(1),1000));
            Assert.Null(new BootstrapStart(Stream,"retired",0,null,5,1,Epoch,Collections(),Token(1),Token(1),1000).NextSequence);
            using var files=new TemporaryDatabase();var db=await Open(files.Path);var store=Store(db);await Assert.ThrowsAsync<StorageException>(()=>store.BeginBootstrapAsync(Boundary(5,Token(1),"retired",0,1),CancellationToken.None));await Assert.ThrowsAsync<StorageException>(()=>store.BeginBootstrapAsync(Boundary(5,Token(1),"active",long.MaxValue,null),CancellationToken.None));await Dispose(db);
        }

        [Fact]
        public async Task ForeignStreamOutboxRowRollsBackBootstrapAndPreservesInstalledState()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await InsertCommandState(db,"0199f9a0-dddd-7777-8888-999999999999",0,2,"pending");await AssertRejectedInstallPreservesReady(db,Boundary(10,Token(7),"active",0,1));await Dispose(db);
        }

        [Theory]
        [InlineData("pending")]
        [InlineData("in_flight")]
        public async Task NonterminalAtOrBelowServerFinalRollsBackAndPreservesInstalledState(string state)
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await InsertCommandState(db,Stream.ToString(),1,2,state);await AssertRejectedInstallPreservesReady(db,Boundary(10,Token(7),"active",1,2));await Dispose(db);
        }

        [Theory]
        [InlineData("accepted")]
        [InlineData("terminal_rejected")]
        public async Task TerminalBeyondServerFinalRollsBackAndPreservesInstalledState(string state)
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await InsertCommandState(db,Stream.ToString(),0,2,state);await AssertRejectedInstallPreservesReady(db,Boundary(10,Token(7),"active",0,1));await Dispose(db);
        }

        [Fact]
        public async Task StorageModelsDefensivelyCopyCallerCollections()
        {
            using var files=new TemporaryDatabase();var db=await Open(files.Path);var names=new List<string>{"profile","progression","inventory","wallet","entitlements"};var boundary=new BootstrapBoundary(Stream,"active",0,1,5,1,Epoch,1000,names,Token(1),Token(1));names[0]="unknown";var entities=new[]{Mutation("profile","self",1,1)};var page=new StagedBootstrapPage(Token(1),Token(1),5,entities,false,null,Token(3));entities[0]=Mutation("unknown","bad",1,2);var store=Store(db);await store.BeginBootstrapAsync(boundary,CancellationToken.None);await store.StageBootstrapPageAsync(page,CancellationToken.None);var changes=new[]{Mutation("profile","self",2,2)};var group=new StoredPullGroup(6,changes);changes[0]=Mutation("unknown","bad",2,3);var groups=new[]{group};var pull=new StoredPullPage(6,groups,Token(4),false);groups[0]=new StoredPullGroup(6,new[]{Mutation("unknown","bad",2,3)});await store.ApplyPullPageAsync(pull,CancellationToken.None);Assert.Equal(2,await Long(db,"SELECT entity_revision FROM gp_confirmed_projection WHERE entity_key='self'"));await Dispose(db);
        }

        [Fact]
        public async Task ViewRemovalAndTombstoneRemainDistinctUntilResetReplacement()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var store=Store(db);await store.ApplyPullPageAsync(new StoredPullPage(6,new[]{new StoredPullGroup(6,new[]{Mutation("inventory","item",1,1)})},Token(4),false),CancellationToken.None);await store.ApplyPullPageAsync(new StoredPullPage(7,new[]{new StoredPullGroup(7,new[]{new StoredProjectionMutation("profile","self",2,StoredProjectionKind.RemoveFromView,Array.Empty<byte>()),new StoredProjectionMutation("inventory","item",2,StoredProjectionKind.Tombstone,Array.Empty<byte>())})},Token(5),false),CancellationToken.None);Assert.Equal("removed",await Text(db,"SELECT state FROM gp_confirmed_projection WHERE entity_key='self'"));Assert.Equal("tombstone",await Text(db,"SELECT state FROM gp_confirmed_projection WHERE entity_key='item'"));var remote=new ScriptedRemote(Bootstrap(10,Token(7)),PageRemote(Token(1),10,false,null,Token(8),new RemoteProjectionMutation("wallet","coins",10,ProjectionMutationKind.Upsert,new byte[]{10})),RemotePullPage.Reset("visibility_changed"));Assert.Equal(PrivateSyncResult.ResetReconciled,await new PrivateSyncCoordinator(store,remote,()=>100).PullOnceAsync(CancellationToken.None));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE state IN ('removed','tombstone')"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='coins' AND state='visible'"));await Dispose(db);
        }

        [Fact]
        public async Task ProjectorReceivesPrivateFeedGroupsInOrderAfterTheirRawChanges()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);var projector=new RecordingProjector();var store=new SqlitePrivateSyncStore(db,Scope,projector,_=>{});
            await store.ApplyPullPageAsync(new StoredPullPage(7,new[]{new StoredPullGroup(6,new[]{Mutation("profile","self",2,6)}),new StoredPullGroup(7,new[]{Mutation("wallet","coins",1,7)})},Token(4),false),CancellationToken.None);
            Assert.Equal(new long[]{6,7},projector.Revisions);Assert.Equal(new long[]{2,1},projector.RawRevisions);Assert.Equal(new[]{"profile:self","wallet:coins"},projector.Keys);var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(7,checkpoint.CommittedThrough);Assert.Equal(Token(4),checkpoint.CopyCursor());await Dispose(db);
        }

        [Fact]
        public async Task ProjectorFailureRollsBackRawTypedOverlayAndCheckpointThenSurvivesReopen()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;s.Execute("CREATE TABLE test_typed_projection (feed_revision INTEGER NOT NULL)");s.Execute("INSERT INTO test_overlay VALUES ('rebuild',0)");return true;},CancellationToken.None);
            var projector=new WritingProjector(failGroup:7,writeOverlay:true);var store=new SqlitePrivateSyncStore(db,Scope,projector,t=>((SqliteTransactionSession)t).Execute("UPDATE test_overlay SET value=value+1 WHERE name='rebuild'"));
            await Assert.ThrowsAsync<InjectedFailure>(()=>store.ApplyPullPageAsync(new StoredPullPage(7,new[]{new StoredPullGroup(6,new[]{Mutation("profile","self",2,6)}),new StoredPullGroup(7,new[]{Mutation("wallet","coins",1,7)})},Token(4),false),CancellationToken.None));
            Assert.Equal(1,await Long(db,"SELECT entity_revision FROM gp_confirmed_projection WHERE collection='profile' AND entity_key='self'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='coins'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM test_typed_projection"));Assert.Equal(0,await Scalar(db,"SELECT value FROM test_overlay WHERE name='rebuild'"));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(Token(3),checkpoint.CopyCursor());await Dispose(db);
            db=await SqliteDatabase.OpenAsync(files.Path,Scope,Migrations,CancellationToken.None);checkpoint=await Store(db).GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(Token(3),checkpoint.CopyCursor());Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM test_typed_projection"));await Dispose(db);
        }

        [Fact]
        public async Task BootstrapReplacementProjectsCompleteSnapshotBeforeReadyAndCursorPublication()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await db.ExecuteAsync(Scope,t=>{((SqliteTransactionSession)t).Execute("CREATE TABLE test_snapshot_projection (entity_key TEXT NOT NULL)");return true;},CancellationToken.None);var projector=new RecordingProjector();var store=new SqlitePrivateSyncStore(db,Scope,projector,_=>{});
            await store.BeginBootstrapAsync(Boundary(10,Token(7)),CancellationToken.None);await store.StageBootstrapPageAsync(Page(Token(1),Token(7),10,false,null,Token(8),Mutation("profile","self",2,10),Mutation("wallet","coins",1,11)),CancellationToken.None);
            Assert.Equal(1,projector.SnapshotCalls);Assert.Equal(new[]{"profile:self","wallet:coins"},projector.SnapshotKeys);Assert.Equal(5,projector.CheckpointDuringSnapshot);var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(10,checkpoint.CommittedThrough);Assert.Equal(Token(8),checkpoint.CopyCursor());await Dispose(db);
        }

        [Fact]
        public async Task BootstrapProjectorFailurePreservesPriorReadyCursorAndRawViewAfterReopen()
        {
            using var files=new TemporaryDatabase();var db=await OpenReady(files.Path);await db.ExecuteAsync(Scope,t=>{((SqliteTransactionSession)t).Execute("CREATE TABLE test_typed_projection (feed_revision INTEGER NOT NULL)");return true;},CancellationToken.None);var store=new SqlitePrivateSyncStore(db,Scope,new WritingProjector(failSnapshot:true),_=>{});
            await store.BeginBootstrapAsync(Boundary(10,Token(7)),CancellationToken.None);await Assert.ThrowsAsync<InjectedFailure>(()=>store.StageBootstrapPageAsync(Page(Token(1),Token(7),10,false,null,Token(8),Mutation("wallet","coins",1,10)),CancellationToken.None));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(Token(3),checkpoint.CopyCursor());Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='coins'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM test_typed_projection"));await Dispose(db);
            db=await SqliteDatabase.OpenAsync(files.Path,Scope,Migrations,CancellationToken.None);checkpoint=await Store(db).GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(Token(3),checkpoint.CopyCursor());Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));await Dispose(db);
        }

        private static BootstrapBoundary Boundary(long through,byte[] first,string state="active",long finalized=0,long? next=1)=>new BootstrapBoundary(Stream,state,finalized,next,through,1,Epoch,1000,new[]{"profile","progression","inventory","wallet","entitlements"},Token(1),first);
        private static BootstrapStart Bootstrap(long through,byte[] first)=>new BootstrapStart(Stream,"active",0,1,through,1,Epoch,new[]{new SnapshotCollection("profile",1,true),new SnapshotCollection("progression",1,true),new SnapshotCollection("inventory",1,true),new SnapshotCollection("wallet",1,true),new SnapshotCollection("entitlements",1,true)},Token(1),first,1000);
        private static IReadOnlyList<SnapshotCollection> Collections()=>new[]{new SnapshotCollection("profile",1,true),new SnapshotCollection("progression",1,true),new SnapshotCollection("inventory",1,true),new SnapshotCollection("wallet",1,true),new SnapshotCollection("entitlements",1,true)};
        private static StagedBootstrapPage Page(byte[] session,byte[] requested,long through,bool more,byte[]? next,byte[]? cursor,params StoredProjectionMutation[] entities)=>new StagedBootstrapPage(session,requested,through,entities,more,next,cursor);
        private static BootstrapPage PageRemote(byte[] session,long through,bool more,byte[]? next,byte[]? cursor,params RemoteProjectionMutation[] entities)=>new BootstrapPage(session,through,entities,more,next,cursor);
        private static StoredProjectionMutation Mutation(string collection,string key,long revision,byte value)=>new StoredProjectionMutation(collection,key,revision,StoredProjectionKind.Upsert,new[]{value});
        private static byte[] Token(byte value){var token=new byte[12];Array.Fill(token,value);return token;}
        private static async Task<SqliteDatabase> Open(string path){var db=await SqliteDatabase.OpenAsync(path,Scope,Migrations,CancellationToken.None);await db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;if(s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state")==0)s.Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,?,0,1,0,0)",Scope.BackendNamespace,Scope.AppId.Value,Scope.AccountId.Value,Stream.ToString(),Installation.ToString("D"));return true;},CancellationToken.None);return db;}
        private static Task<bool> ConfigureStream(SqliteDatabase db,long finalized,long next,params string[] states)=>db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;for(var i=0;i<states.Length;i++){var sequence=i+1;s.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,installation_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,client_created_at,semantic_body,fingerprint,local_revision,delivery_state,leased_until,terminal_result) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",Guid.Parse($"0199f9a0-cccc-7777-8888-{sequence:D12}").ToString(),Scope.BackendNamespace,Scope.AppId.Value,Scope.AccountId.Value,Stream.ToString(),Installation.ToString("D"),sequence,"run-"+sequence,"profile.patch",1,1,100L,new byte[]{1},new byte[]{2},sequence,states[i],states[i]=="in_flight"?(object)999:null!,states[i]=="accepted"||states[i]=="terminal_rejected"?(object)new byte[]{3}:null!);}s.Execute("UPDATE gp_stream_state SET finalized_through=?,next_sequence=?,local_revision=? WHERE singleton=1",finalized,next,states.Length);return true;},CancellationToken.None);
        private static Task<bool> InsertCommandState(SqliteDatabase db,string rowStream,long finalized,long next,string state)=>db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;s.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,installation_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,client_created_at,semantic_body,fingerprint,local_revision,delivery_state,leased_until,terminal_result) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)","0199f9a0-eeee-7777-8888-000000000001",Scope.BackendNamespace,Scope.AppId.Value,Scope.AccountId.Value,rowStream,Installation.ToString("D"),1,"review-run","profile.patch",1,1,100L,new byte[]{1},new byte[]{2},1,state,state=="in_flight"?(object)999:null!,state=="accepted"||state=="terminal_rejected"?(object)new byte[]{3}:null!);s.Execute("UPDATE gp_stream_state SET finalized_through=?,next_sequence=?,local_revision=1 WHERE singleton=1",finalized,next);return true;},CancellationToken.None);
        private static async Task AssertRejectedInstallPreservesReady(SqliteDatabase db,BootstrapBoundary boundary){var store=Store(db);await store.BeginBootstrapAsync(boundary,CancellationToken.None);await Assert.ThrowsAsync<StorageException>(()=>store.StageBootstrapPageAsync(Page(Token(1),Token(7),10,false,null,Token(8),Mutation("wallet","replacement",10,10)),CancellationToken.None));var checkpoint=await store.GetPullCheckpointAsync(CancellationToken.None);Assert.Equal(5,checkpoint.CommittedThrough);Assert.Equal(Token(3),checkpoint.CopyCursor());Assert.Equal(1,await Scalar(db,"SELECT ready FROM gp_stream_state WHERE singleton=1"));Assert.Equal(1,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='self'"));Assert.Equal(0,await Scalar(db,"SELECT COUNT(*) FROM gp_confirmed_projection WHERE entity_key='replacement'"));}
        private static SqlitePrivateSyncStore Store(SqliteDatabase db)=>new SqlitePrivateSyncStore(db,Scope,NoopProjector.Instance,_=>{});
        private static async Task<SqliteDatabase> OpenReady(string path){var db=await Open(path);var store=Store(db);await store.BeginBootstrapAsync(Boundary(5,Token(1)),CancellationToken.None);await store.StageBootstrapPageAsync(Page(Token(1),Token(1),5,false,null,Token(3),Mutation("profile","self",1,1)),CancellationToken.None);return db;}
        private static Task<int> Scalar(SqliteDatabase db,string sql)=>db.ExecuteAsync(Scope,t=>((SqliteTransactionSession)t).ExecuteScalar<int>(sql),CancellationToken.None);
        private static Task<long> Long(SqliteDatabase db,string sql)=>db.ExecuteAsync(Scope,t=>((SqliteTransactionSession)t).ExecuteScalar<long>(sql),CancellationToken.None);
        private static Task<string> Text(SqliteDatabase db,string sql)=>db.ExecuteAsync(Scope,t=>((SqliteTransactionSession)t).ExecuteScalar<string>(sql),CancellationToken.None);
        private static async Task Dispose(SqliteDatabase db)=>Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
        private sealed class InjectedFailure:Exception{}
        private sealed class NoopProjector:IPrivateSyncProjectionProjector
        {
            public static readonly NoopProjector Instance=new NoopProjector();
            public void ProjectConfirmedGroup(ILocalStorageTransaction transaction,StoredPullGroup group){}
            public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction,IReadOnlyList<StoredProjectionMutation> snapshot){}
        }
        private sealed class RecordingProjector:IPrivateSyncProjectionProjector
        {
            public List<long> Revisions { get; }=new List<long>();public List<long> RawRevisions { get; }=new List<long>();public List<string> Keys { get; }=new List<string>();public int SnapshotCalls { get; private set; }public List<string> SnapshotKeys { get; }=new List<string>();public long CheckpointDuringSnapshot { get; private set; }
            public void ProjectConfirmedGroup(ILocalStorageTransaction transaction,StoredPullGroup group){var s=(SqliteTransactionSession)transaction;Revisions.Add(group.Revision);foreach(var change in group.Changes){Keys.Add(change.Collection+":"+change.EntityKey);RawRevisions.Add(s.ExecuteScalar<long>("SELECT entity_revision FROM gp_confirmed_projection WHERE collection=? AND entity_key=?",change.Collection,change.EntityKey));}}
            public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction,IReadOnlyList<StoredProjectionMutation> snapshot){var s=(SqliteTransactionSession)transaction;SnapshotCalls++;foreach(var entity in snapshot)SnapshotKeys.Add(entity.Collection+":"+entity.EntityKey);CheckpointDuringSnapshot=s.ExecuteScalar<long>("SELECT committed_through FROM gp_sync_state WHERE singleton=1");}
        }
        private sealed class WritingProjector:IPrivateSyncProjectionProjector
        {
            private readonly long? failGroup;private readonly bool failSnapshot,writeOverlay;public WritingProjector(long? failGroup=null,bool failSnapshot=false,bool writeOverlay=false){this.failGroup=failGroup;this.failSnapshot=failSnapshot;this.writeOverlay=writeOverlay;}
            public void ProjectConfirmedGroup(ILocalStorageTransaction transaction,StoredPullGroup group){var s=(SqliteTransactionSession)transaction;s.Execute("INSERT INTO test_typed_projection VALUES (?)",group.Revision);if(writeOverlay)s.Execute("UPDATE test_overlay SET value=value+1 WHERE name='rebuild'");if(failGroup==group.Revision)throw new InjectedFailure();}
            public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction,IReadOnlyList<StoredProjectionMutation> snapshot){((SqliteTransactionSession)transaction).Execute("INSERT INTO test_typed_projection VALUES (?)",snapshot.Count);if(failSnapshot)throw new InjectedFailure();}
        }
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
