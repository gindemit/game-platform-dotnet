using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Sync.Push
{
    public enum SendCycleResult{Idle,Finalized,Retryable,OutcomeUncertain}
    public sealed class OrderedCommandSender
    {
        private readonly ICommandDeliveryStore store;private readonly ICommandRemote remote;private readonly Func<long> now;private readonly SemaphoreSlim single=new SemaphoreSlim(1,1);
        public OrderedCommandSender(ICommandDeliveryStore store,ICommandRemote remote,Func<long> nowMilliseconds){this.store=store??throw new ArgumentNullException(nameof(store));this.remote=remote??throw new ArgumentNullException(nameof(remote));now=nowMilliseconds??throw new ArgumentNullException(nameof(nowMilliseconds));}
        public async Task<SendCycleResult> RunOnceAsync(CancellationToken cancellationToken){await single.WaitAsync(cancellationToken).ConfigureAwait(false);try{var leased=await store.LeaseNextAsync(now(),30_000,cancellationToken).ConfigureAwait(false);if(leased==null)return SendCycleResult.Idle;var command=new RemoteCommand(leased.OperationId,leased.StreamId,leased.Sequence,leased.OperationKind,leased.SchemaVersion,leased.FingerprintVersion,leased.CopyBody(),leased.CopyFingerprint());RemoteResult<RemoteCommandOutcome> sent;try{sent=await remote.SendAsync(command,cancellationToken).ConfigureAwait(false);}catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){return SendCycleResult.OutcomeUncertain;}catch{return SendCycleResult.OutcomeUncertain;}if(!sent.IsSuccess){if(sent.Failure.Kind==RemoteFailureKind.NotSent||sent.Failure.Kind==RemoteFailureKind.Authentication||sent.Failure.Kind==RemoteFailureKind.Authorization||sent.Failure.Kind==RemoteFailureKind.Validation||sent.Failure.Kind==RemoteFailureKind.RateLimited||sent.Failure.Kind==RemoteFailureKind.Dependency){await store.ReleaseAsync(leased.OperationId,leased.Sequence,CancellationToken.None).ConfigureAwait(false);return SendCycleResult.Retryable;}return SendCycleResult.OutcomeUncertain;}var outcome=sent.Value!;await store.FinalizeAsync(leased.OperationId,leased.Sequence,outcome.Status==RemoteCommandStatus.Accepted?CommandTerminalOutcome.Accepted:CommandTerminalOutcome.Rejected,outcome.Result,CancellationToken.None).ConfigureAwait(false);return SendCycleResult.Finalized;}finally{single.Release();}}
    }
}
