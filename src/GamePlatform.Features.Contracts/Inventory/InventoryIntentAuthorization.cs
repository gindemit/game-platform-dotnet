using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{
    public sealed class InventoryIntentAuthorization
    {
        public InventoryIntentAuthorization(bool permitted, string? operationKind, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (permitted && (string.IsNullOrWhiteSpace(operationKind) || operationKind.Length > 128))
                throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (!permitted && operationKind != null)
                throw new ArgumentException("Denied intents have no operation route.", nameof(operationKind));
            Permitted = permitted;
            OperationKind = operationKind;
            Reason = reason;
        }
        public bool Permitted { get; }
        public string? OperationKind { get; }
        public string Reason { get; }
    }
}
