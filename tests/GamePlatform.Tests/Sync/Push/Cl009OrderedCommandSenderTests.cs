using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Sync.Push;

namespace GamePlatform.Tests.Sync.Push
{
    public sealed class Cl009OrderedCommandSenderTests
    {
        private static readonly AppId App=new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId User=new PlatformUserId(Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"));
        private static readonly OwnerScope Owner=new OwnerScope(new BackendNamespace("test-backend"),App,User);
        private static readonly StorageScope Scope=new StorageScope("test-backend",new PlatformId(App.ToString()),new PlatformId(User.ToString()));
        private static readonly ClientStreamId Stream=new ClientStreamId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
        private static readonly Guid Installation=Guid.Parse("0199f9a0-1212-7777-8888-999999999999");
        private static readonly IReadOnlyList<SqliteMigration> Migrations=SqlitePlatformMigrationRegistry.Migrations.Concat(new[]{new SqliteMigration(7,"test-projection",new[]{"CREATE TABLE test_projection (name TEXT PRIMARY KEY, value INTEGER NOT NULL)"})}).ToArray();

        [Fact] public async Task AcceptedOutcomeIsDurableBeforeLaterSequenceRuns(){using var files=new TemporaryDatabase();var db=await Open(files.Path);await Admit(db,"0199f9a0-2222-7777-8888-999999999999","one");var remote=new ScriptedRemote(RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(RemoteCommandStatus.Accepted,new byte[]{4,5})));var sender=Sender(db,remote,()=>100);Assert.Equal(SendCycleResult.Finalized,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(SendCycleResult.Idle,await sender.RunOnceAsync(CancellationToken.None));var state=await Inspect(db);Assert.Equal(1,state.Item1);Assert.Equal("accepted",state.Item2);Assert.Equal(new byte[]{4,5},state.Item3);Assert.Single(remote.Commands);Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));}

        [Fact] public async Task TerminalRejectionFinalizesSequenceAndUnblocksNext(){using var files=new TemporaryDatabase();var db=await Open(files.Path);await Admit(db,"0199f9a0-3333-7777-8888-999999999999","one");await Admit(db,"0199f9a0-4444-7777-8888-999999999999","two");var remote=new ScriptedRemote(RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(RemoteCommandStatus.TerminalRejected,null)),RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(RemoteCommandStatus.Accepted,null)));var sender=Sender(db,remote,()=>100);Assert.Equal(SendCycleResult.Finalized,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(SendCycleResult.Finalized,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(new long[]{1,2},remote.Commands.ConvertAll(value=>value.Sequence));var finalized=await db.ExecuteAsync(Scope,t=>((SqliteTransactionSession)t).ExecuteScalar<long>("SELECT finalized_through FROM gp_stream_state WHERE singleton=1"),CancellationToken.None);Assert.Equal(2,finalized);Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));}

        [Fact] public async Task KnownRetryableFailureReleasesTheSameImmutableCommand(){using var files=new TemporaryDatabase();var db=await Open(files.Path);await Admit(db,"0199f9a0-5555-7777-8888-999999999999","one");var remote=new ScriptedRemote(RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.Dependency,503)),RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(RemoteCommandStatus.Accepted,null)));var sender=Sender(db,remote,()=>100);Assert.Equal(SendCycleResult.Retryable,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(SendCycleResult.Finalized,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(remote.Commands[0].OperationId,remote.Commands[1].OperationId);Assert.Equal(remote.Commands[0].SemanticBody,remote.Commands[1].SemanticBody);Assert.Equal(Installation,remote.Commands[0].InstallationId);Assert.Equal(remote.Commands[0].InstallationId,remote.Commands[1].InstallationId);Assert.Equal(1_789_555_200_000,remote.Commands[0].ClientCreatedAt);Assert.Equal(remote.Commands[0].ClientCreatedAt,remote.Commands[1].ClientCreatedAt);Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));}

        [Fact] public async Task UncertainOutcomeKeepsLeaseAndRetriesOnlyAfterExpiry(){using var files=new TemporaryDatabase();var db=await Open(files.Path);await Admit(db,"0199f9a0-6666-7777-8888-999999999999","one");long now=100;var remote=new ScriptedRemote(RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)),RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(RemoteCommandStatus.Accepted,null)));var sender=Sender(db,remote,()=>now);Assert.Equal(SendCycleResult.OutcomeUncertain,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(SendCycleResult.Idle,await sender.RunOnceAsync(CancellationToken.None));now=30_100;Assert.Equal(SendCycleResult.Finalized,await sender.RunOnceAsync(CancellationToken.None));Assert.Equal(2,remote.Commands.Count);Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));}

        [Fact] public async Task CompetingSendersCannotLeaseOneSequenceTwice(){using var files=new TemporaryDatabase();var db=await Open(files.Path);await Admit(db,"0199f9a0-7777-7777-8888-999999999999","one");var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var remote=new BlockingRemote(entered,release);var first=Sender(db,remote,()=>100).RunOnceAsync(CancellationToken.None);await entered.Task;var second=await Sender(db,remote,()=>100).RunOnceAsync(CancellationToken.None);Assert.Equal(SendCycleResult.Idle,second);release.SetResult(true);Assert.Equal(SendCycleResult.Finalized,await first);Assert.Equal(1,remote.Calls);Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));}

        private static OrderedCommandSender Sender(SqliteDatabase db,ICommandRemote remote,Func<long> now)=>new OrderedCommandSender(new SqliteCommandDeliveryStore(db,Scope),remote,now);
        private static async Task<SqliteDatabase> Open(string path){var db=await SqliteDatabase.OpenAsync(path,Scope,Migrations,CancellationToken.None);await db.ExecuteAsync(Scope,t=>{((SqliteTransactionSession)t).Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,?,1,1,0,0)",Owner.Backend.Value,Owner.AppId.ToString(),Owner.UserId.ToString(),Stream.ToString(),Installation.ToString("D"));return true;},CancellationToken.None);return db;}
        private static Task<CommandAdmission> Admit(SqliteDatabase db,string id,string name)=>new SqliteAtomicCommandStore(db,Scope,Owner,new Fingerprint(),new FixedClock()).CommitAsync(name,new CommandDraft(Owner,new OperationId(Guid.Parse(id)),Stream,"profile.patch",1,1,new byte[]{1,2,3}),(t,r)=>((SqliteTransactionSession)t).Execute("INSERT INTO test_projection VALUES (?,?)",name,r),CancellationToken.None);
        private static Task<(long,string,byte[])> Inspect(SqliteDatabase db)=>db.ExecuteAsync(Scope,t=>{var s=(SqliteTransactionSession)t;return(s.ExecuteScalar<long>("SELECT finalized_through FROM gp_stream_state WHERE singleton=1"),s.ExecuteScalar<string>("SELECT delivery_state FROM gp_outbox WHERE sequence=1"),s.ExecuteScalar<byte[]>("SELECT terminal_result FROM gp_outbox WHERE sequence=1"));},CancellationToken.None);
        private sealed class ScriptedRemote:ICommandRemote{private readonly Queue<RemoteResult<RemoteCommandOutcome>> results;public ScriptedRemote(params RemoteResult<RemoteCommandOutcome>[] values){results=new Queue<RemoteResult<RemoteCommandOutcome>>(values);}public List<RemoteCommand> Commands{get;}=new List<RemoteCommand>();public Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command,CancellationToken token){Commands.Add(command);return Task.FromResult(results.Dequeue());}}
        private sealed class BlockingRemote:ICommandRemote{private readonly TaskCompletionSource<bool> entered,release;public BlockingRemote(TaskCompletionSource<bool> entered,TaskCompletionSource<bool> release){this.entered=entered;this.release=release;}public int Calls{get;private set;}public async Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command,CancellationToken token){Calls++;entered.SetResult(true);await release.Task;return RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(RemoteCommandStatus.Accepted,null));}}
        private sealed class Fingerprint:ICommandFingerprint{public int GetFingerprintLength(int version)=>32;public void Compute(OwnerScope owner,OperationId operation,ClientStreamId stream,Guid installation,long sequence,string kind,int schema,int version,long clientCreatedAt,ReadOnlySpan<byte> body,Span<byte> destination){SHA256.HashData(Encoding.UTF8.GetBytes(operation+"|"+sequence)).CopyTo(destination);}}
        private sealed class FixedClock:IUnixMillisecondClock{public long GetUnixMilliseconds()=>1_789_555_200_000;}
        private sealed class TemporaryDatabase:IDisposable{private readonly string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"game-platform-cl009",Guid.NewGuid().ToString("N"));public TemporaryDatabase(){Directory.CreateDirectory(directory);Path=System.IO.Path.Combine(directory,"platform.sqlite3");}public string Path{get;}public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}}
    }
}
