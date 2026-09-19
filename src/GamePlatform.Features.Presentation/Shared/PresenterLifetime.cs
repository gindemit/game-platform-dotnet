using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Presentation.Shared
{
    public sealed class PresenterLifetime : IDisposable
    {
        private readonly object borrowedService;
        private IDisposable? subscription;
        private IDisposable? ownedViewLease;
        private bool disposed;

        public PresenterLifetime(ScopedOwnerContext owner, object borrowedService)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            Owner = owner;
            this.borrowedService = borrowedService ?? throw new ArgumentNullException(nameof(borrowedService));
        }

        public ScopedOwnerContext Owner { get; }
        public object BorrowedService => borrowedService;
        public bool IsBound => subscription != null;

        public void Bind(IDisposable newSubscription, IDisposable newOwnedViewLease)
        {
            if (newSubscription == null) throw new ArgumentNullException(nameof(newSubscription));
            if (newOwnedViewLease == null) throw new ArgumentNullException(nameof(newOwnedViewLease));
            if (disposed) throw new ObjectDisposedException(nameof(PresenterLifetime));
            Unbind();
            subscription = newSubscription;
            ownedViewLease = newOwnedViewLease;
        }

        public bool TryPublish(ScopedOwnerContext callbackOwner, Action publish)
        {
            if (publish == null) throw new ArgumentNullException(nameof(publish));
            if (disposed || subscription == null || callbackOwner != Owner) return false;
            publish();
            return true;
        }

        public void Unbind()
        {
            var oldSubscription = subscription;
            var oldLease = ownedViewLease;
            subscription = null;
            ownedViewLease = null;
            oldSubscription?.Dispose();
            oldLease?.Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unbind();
        }
    }

    public readonly struct ResultPresentation
    {
        public ResultPresentation(GamePlatform.Navigation.CompletionPresentationState state, string diagnosticCode)
        {
            if (string.IsNullOrWhiteSpace(diagnosticCode) || diagnosticCode.Length > 64) throw new ArgumentOutOfRangeException(nameof(diagnosticCode));
            State = state;
            DiagnosticCode = diagnosticCode;
        }
        public GamePlatform.Navigation.CompletionPresentationState State { get; }
        public string DiagnosticCode { get; }
        public bool CanAdvance => State == GamePlatform.Navigation.CompletionPresentationState.Confirmed;
        public bool CanRetrySave => State == GamePlatform.Navigation.CompletionPresentationState.SaveFailed;
    }
}
