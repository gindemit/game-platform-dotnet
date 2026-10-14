using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Presentation.Shared
{

    public readonly struct ResultPresentation
    {
        public ResultPresentation(GamePlatform.Navigation.CompletionPresentationState state, string diagnosticCode)
        {
            if (string.IsNullOrWhiteSpace(diagnosticCode) || diagnosticCode.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(diagnosticCode));
            State = state;
            DiagnosticCode = diagnosticCode;
        }
        public GamePlatform.Navigation.CompletionPresentationState State { get; }
        public string DiagnosticCode { get; }
        public bool CanAdvance => State == GamePlatform.Navigation.CompletionPresentationState.Confirmed;
        public bool CanRetrySave => State == GamePlatform.Navigation.CompletionPresentationState.SaveFailed;
    }
}
