using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    /// <summary>Feature-owned remote read port. An adapter must bind it to the frozen getProfile operation.</summary>
    public interface IProfileRemote
    {
        Task<ProfileRemoteRead> ReadAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
    }
}
