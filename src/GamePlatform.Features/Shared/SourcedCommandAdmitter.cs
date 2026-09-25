#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Shared
{
    /// <summary>
    /// Admits one immutable command per business source, reusing the operation ID retained for that source.
    /// </summary>
    internal sealed class SourcedCommandAdmitter : IDisposable
    {
        private readonly ScopedOwnerContext owner;
        private readonly ClientStreamId streamId;
        private readonly string featureNamespace;
        private readonly IDurableFeatureStateStore state;
        private readonly IAtomicCommandStore commands;
        private readonly UuidV7Generator ids;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);

        internal SourcedCommandAdmitter(ScopedOwnerContext owner, ClientStreamId streamId, string featureNamespace, IDurableFeatureStateStore state,
            IAtomicCommandStore commands, UuidV7Generator ids)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            this.owner = owner;
            this.streamId = streamId;
            this.featureNamespace = featureNamespace;
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
        }

        internal async Task<CommandAdmission> AdmitAsync(string businessSource, string operationKind, byte[] body, CancellationToken cancellationToken)
        {
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var retained = await state.ReadAsync(owner, featureNamespace, businessSource, cancellationToken).ConfigureAwait(false);
                var operationId = new OperationId(retained == null ? ids.NewId() : new Guid(retained.CopyPayload()));
                var draft = new CommandDraft(owner.Owner, operationId, streamId, operationKind, 1, 1, body);
                return await commands.CommitAsync(businessSource, draft, (transaction, localRevision) =>
                    state.Upsert(transaction, new DurableFeatureMutation(owner, featureNamespace, businessSource, localRevision, 0,
                        operationId.Value.ToByteArray(), Array.Empty<byte>())), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                mutation.Release();
            }
        }

        public void Dispose() => mutation.Dispose();
    }
}
