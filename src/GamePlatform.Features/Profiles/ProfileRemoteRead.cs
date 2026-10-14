using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    public sealed class ProfileRemoteRead
    {
        public ProfileRemoteRead(ScopedOwnerContext owner, ProfileConfirmed profile)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            Owner = owner;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }
        public ScopedOwnerContext Owner { get; }
        public ProfileConfirmed Profile { get; }
    }
}
