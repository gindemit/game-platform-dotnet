using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{
    public enum CommandStreamRecoveryReason
    {
        LocalDatabaseRecreated,
        BackupRestored,
        StreamIdentityConflict
    }
}
