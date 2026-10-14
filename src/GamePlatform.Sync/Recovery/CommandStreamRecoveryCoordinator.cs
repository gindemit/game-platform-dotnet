#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Recovery;
using JournalRequest = GamePlatform.Storage.Abstractions.Recovery.CommandStreamRecoveryRequest;
using RemoteRequest = GamePlatform.Backend.Contracts.Remote.CommandStreamRecoveryRequest;

namespace GamePlatform.Sync.Recovery
{
    public enum CommandStreamRecoveryOutcome
    {
        BootstrapComplete, DamageNotEstablished, UnresolvedCommands, RemoteFailure,
        OutcomeUncertain, BootstrapRequired, Retired
    }

    /// <summary>
    /// Account lifetime owned by the host. Quiescence drains gameplay admission, push, pull
    /// and late callbacks before returning true. New recovery requires verified damage;
    /// journal resume must fence writers even when the directory already names the successor.
    /// </summary>
    public interface ICommandStreamRecoveryHost
    {
        bool IsCurrentAccount
        {
            get;
        }
        Task<bool> QuiesceAsync(ClientStreamId priorStreamId, CommandStreamRecoveryReason reason, bool resume, CancellationToken cancellationToken);
        /// <summary>
        /// Opens successor storage without copying sequenced commands and performs genuine
        /// complete private bootstrap. The prior database/evidence remains quarantined.
        /// True requires durable identity, allocator, projections and checkpoint convergence;
        /// no gameplay admission occurs until this coordinator returns BootstrapComplete.
        /// </summary>
        Task<bool> BootstrapSuccessorAsync(ClientStreamId successorStreamId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Explicit account-owned recovery; construction starts no IO. The owner awaits StopAsync
    /// before replacing this lifetime. Neither the journal nor a recovery response asserts Ready.
    /// </summary>
    public sealed class CommandStreamRecoveryCoordinator
    {
        private readonly AccountDirectoryEntry account;
        private readonly ICommandStreamRecoveryRemote remote;
        private readonly ICommandReceiptRemote receipts;
        private readonly ICommandStreamRecoveryJournal journal;
        private readonly IRecoveryCommandStore commands;
        private readonly ICommandStreamRecoveryHost host;
        private readonly Func<Guid> newUuidV7;
        private readonly SemaphoreSlim single = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private int stopped;

        public CommandStreamRecoveryCoordinator(AccountDirectoryEntry issuedAccount,
            ICommandStreamRecoveryRemote remote, ICommandReceiptRemote receipts,
            ICommandStreamRecoveryJournal journal, IRecoveryCommandStore commands,
            ICommandStreamRecoveryHost host, Func<Guid> newUuidV7)
        {
            account = issuedAccount ?? throw new ArgumentNullException(nameof(issuedAccount));
            if (!account.HasIssuedAccount || account.MembershipStatus != "active")
                throw new ArgumentException("An active issued account is required.", nameof(issuedAccount));
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.newUuidV7 = newUuidV7 ?? throw new ArgumentNullException(nameof(newUuidV7));
        }

        public Task<CommandStreamRecoveryOutcome> RecoverAsync(CommandStreamRecoveryReason reason, CancellationToken cancellationToken) =>
            RunAsync(reason, false, cancellationToken);

        /// <summary>Resumes the retained immutable request or requalifies its genuine bootstrap; never starts a new rotation.</summary>
        public Task<CommandStreamRecoveryOutcome> ResumeAsync(CancellationToken cancellationToken) =>
            RunAsync(CommandStreamRecoveryReason.LocalDatabaseRecreated, true, cancellationToken);

        private async Task<CommandStreamRecoveryOutcome> RunAsync(CommandStreamRecoveryReason reason, bool resumeOnly, CancellationToken cancellationToken)
        {
            var wireReason = Reason(reason);
            if (!Current)
                return CommandStreamRecoveryOutcome.Retired;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            var ct = linked.Token;
            try
            {
                await single.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!Current) { return CommandStreamRecoveryOutcome.Retired; }
            try
            {
                CheckCurrent(ct);
                var record = await journal.LoadAsync(ct).ConfigureAwait(false);
                CheckCurrent(ct);
                if (record != null)
                    RequireOwner(record.Request);
                if (resumeOnly && record == null)
                    return CommandStreamRecoveryOutcome.DamageNotEstablished;
                if (!resumeOnly && record?.Phase == CommandStreamRecoveryPhase.BootstrapComplete)
                {
                    // A new recovery requires a new lifetime bound to the durably adopted directory entry.
                    if (account.StreamId != record.Request.SuccessorStreamId)
                        return CommandStreamRecoveryOutcome.BootstrapRequired;
                    record = null;
                }
                // A retained completed journal resumes bootstrap; it cannot prove the host's DB is intact.
                var prior = record?.Request.PriorStreamId ?? account.StreamId;
                var retainedReason = record == null ? reason : ParseReason(record.Request.Reason);
                var quiesceStream = record != null && record.Phase >= CommandStreamRecoveryPhase.Adopted ? record.Request.SuccessorStreamId : prior;
                if (!await host.QuiesceAsync(quiesceStream, retainedReason, record != null, ct).ConfigureAwait(false))
                    return CommandStreamRecoveryOutcome.DamageNotEstablished;
                CheckCurrent(ct);

                if (record?.Phase == CommandStreamRecoveryPhase.Expired)
                {
                    reason = retainedReason;
                    wireReason = Reason(reason);
                    record = null;
                }

                if (record == null || record.Phase == CommandStreamRecoveryPhase.Prepared)
                {
                    var unresolved = await commands.SuspendAndReadAsync(prior, ct).ConfigureAwait(false);
                    var missingReceipt = false;
                    foreach (var command in unresolved)
                    {
                        CheckCurrent(ct);
                        if (command.StreamId != prior || command.InstallationId != account.InstallationId)
                            throw new InvalidOperationException("Retained commands do not belong to this recovery lifetime.");
                        var lookup = await receipts.LookupAsync(new RemoteCommand(command.OperationId, command.StreamId,
                            command.InstallationId, command.Sequence, command.OperationKind, command.SchemaVersion,
                            command.FingerprintVersion, command.ClientCreatedAt, command.CopyBody(), command.CopyFingerprint()), ct).ConfigureAwait(false);
                        CheckCurrent(ct);
                        if (!lookup.IsSuccess)
                            return CommandStreamRecoveryOutcome.RemoteFailure;
                        if (!lookup.Value!.Found)
                        {
                            missingReceipt = true;
                            continue;
                        }
                        await commands.RecordReceiptAsync(command,
                            lookup.Value.Outcome!.Status == RemoteCommandStatus.Accepted ? CommandTerminalOutcome.Accepted : CommandTerminalOutcome.Rejected,
                            lookup.Value.Outcome.Result, ct).ConfigureAwait(false);
                    }
                    CheckCurrent(ct);
                    if (missingReceipt || !await commands.IsSafeToRetireAsync(prior, ct).ConfigureAwait(false))
                        return CommandStreamRecoveryOutcome.UnresolvedCommands;

                    if (record == null)
                    {
                        var inspected = await remote.InspectAsync(prior, ct).ConfigureAwait(false);
                        CheckCurrent(ct);
                        if (!inspected.IsSuccess)
                            return CommandStreamRecoveryOutcome.RemoteFailure;
                        var state = inspected.Value!;
                        if (state.StreamId != prior || state.InstallationId != account.InstallationId || state.State != CommandStreamState.Active)
                            return CommandStreamRecoveryOutcome.RemoteFailure;
                        var successor = new ClientStreamId(newUuidV7());
                        var recoveryId = newUuidV7();
                        // Validate before persisting using the same portable request invariants as transport.
                        _ = new RemoteRequest(prior, successor, recoveryId, reason, state.InspectionToken, state.FinalizedThrough);
                        CheckCurrent(ct);
                        record = await journal.PrepareAsync(new JournalRequest(account.Principal, account.AppId,
                            account.AccountId!.Value, account.InstallationId, prior, successor, recoveryId,
                            wireReason, state.InspectionToken, state.FinalizedThrough), ct).ConfigureAwait(false);
                    }

                    CheckCurrent(ct);
                    var request = record.Request;
                    // No expiry check or reinspection here: recorded backend retries precede expiry/state checks.
                    var recovered = await remote.RecoverAsync(new RemoteRequest(request.PriorStreamId, request.SuccessorStreamId,
                        request.RecoveryId, ParseReason(request.Reason), request.InspectionToken, request.ExpectedFinalizedThrough), ct).ConfigureAwait(false);
                    CheckCurrent(ct);
                    if (!recovered.IsSuccess && recovered.Failure.Kind == RemoteFailureKind.InspectionExpired)
                    {
                        await journal.MarkExpiredAsync(request.RecoveryId, ct).ConfigureAwait(false);
                        CheckCurrent(ct);
                        return CommandStreamRecoveryOutcome.RemoteFailure;
                    }
                    if (!recovered.IsSuccess)
                        return recovered.Failure.Kind == RemoteFailureKind.OutcomeUncertain || recovered.Failure.Kind == RemoteFailureKind.Protocol
                            ? CommandStreamRecoveryOutcome.OutcomeUncertain : CommandStreamRecoveryOutcome.RemoteFailure;
                    var result = recovered.Value!;
                    if (result.PriorStreamId != request.PriorStreamId || result.NewStreamId != request.SuccessorStreamId ||
                        result.RecoveryId != request.RecoveryId || result.PriorFinalizedThrough != request.ExpectedFinalizedThrough || result.NextSequence != 1)
                        return CommandStreamRecoveryOutcome.OutcomeUncertain;
                    record = await journal.RecordRemoteCommitAsync(request.RecoveryId, result.RecoveryId, result.PriorStreamId, result.NewStreamId,
                        result.PriorFinalizedThrough, result.NextSequence, ct).ConfigureAwait(false);
                }

                CheckCurrent(ct);
                if (record.Phase == CommandStreamRecoveryPhase.RemoteCommitted)
                    record = await journal.AdoptAsync(record.Request.RecoveryId, ct).ConfigureAwait(false);
                CheckCurrent(ct);
                if (!await host.BootstrapSuccessorAsync(record.Request.SuccessorStreamId, ct).ConfigureAwait(false))
                    return CommandStreamRecoveryOutcome.BootstrapRequired;
                CheckCurrent(ct);
                await journal.MarkBootstrapCompleteAsync(record.Request.RecoveryId, ct).ConfigureAwait(false);
                CheckCurrent(ct);
                return CommandStreamRecoveryOutcome.BootstrapComplete;
            }
            catch (OperationCanceledException) when (!Current) { return CommandStreamRecoveryOutcome.Retired; }
            finally { single.Release(); }
        }

        /// <summary>Fences immediately, cancels late work, then waits for the bounded host/transport drain.</summary>
        public async Task StopAsync()
        {
            Interlocked.Exchange(ref stopped, 1);
            lifetime.Cancel();
            await single.WaitAsync().ConfigureAwait(false);
            single.Release();
        }

        private bool Current => Volatile.Read(ref stopped) == 0 && host.IsCurrentAccount;
        private void CheckCurrent(CancellationToken ct)
        {
            if (!Current)
                throw new OperationCanceledException(lifetime.Token);
            ct.ThrowIfCancellationRequested();
        }
        private void RequireOwner(JournalRequest request)
        {
            if (!request.Principal.Equals(account.Principal) || request.AppId != account.AppId ||
                request.AccountId != account.AccountId!.Value || request.InstallationId != account.InstallationId)
                throw new InvalidOperationException("The recovery journal belongs to another account lifetime.");
        }
        private static string Reason(CommandStreamRecoveryReason reason) => reason switch
        {
            CommandStreamRecoveryReason.LocalDatabaseRecreated => "local_database_recreated",
            CommandStreamRecoveryReason.BackupRestored => "backup_restored",
            CommandStreamRecoveryReason.StreamIdentityConflict => "stream_identity_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };
        private static CommandStreamRecoveryReason ParseReason(string reason) => reason switch
        {
            "local_database_recreated" => CommandStreamRecoveryReason.LocalDatabaseRecreated,
            "backup_restored" => CommandStreamRecoveryReason.BackupRestored,
            "stream_identity_conflict" => CommandStreamRecoveryReason.StreamIdentityConflict,
            _ => throw new ArgumentException("The retained recovery reason is invalid.")
        };
    }
}
