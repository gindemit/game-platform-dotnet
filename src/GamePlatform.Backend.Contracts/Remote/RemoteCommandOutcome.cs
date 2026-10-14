using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{
    public sealed class RemoteCommandOutcome
    {
        private readonly byte[]? result; public RemoteCommandOutcome(RemoteCommandStatus status, byte[]? result)
        {
            if ((result?.Length ?? 0) > 65_536)
                throw new ArgumentOutOfRangeException(nameof(result));
            Status = status;
            this.result = result == null ? null : (byte[])result.Clone();
        }
        public RemoteCommandStatus Status { get; }
        public byte[]? Result => result == null ? null : (byte[])result.Clone();
    }
}
