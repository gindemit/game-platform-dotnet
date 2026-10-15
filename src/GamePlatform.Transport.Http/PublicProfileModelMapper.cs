using System;
using D = GamePlatform.Features.Profiles;
using W = GamePlatform.Wire.Contracts.Profiles;

namespace GamePlatform.Transport.Http
{
    internal static class PublicProfileModelMapper
    {
        internal static W.PublicProfileResponse ToWire(D.PublicProfileResponse x) => new W.PublicProfileResponse(x.AppId, x.PlayerId, x.ServerTimeMilliseconds,
            x.Profile == null ? null : new W.PublicProfile(x.Profile.DisplayName, x.Profile.AvatarKey, x.Profile.Locale, x.Profile.Revision),
            x.Profile?.Team == null ? null : new W.PublicProfileTeam(x.Profile.Team.TeamId, x.Profile.Team.Name, x.Profile.Team.BadgeId));

        internal static D.PublicProfileResponse ToDomain(W.PublicProfileResponse x)
        {
            if (x.Profile == null && x.Team != null)
                throw new ArgumentException("A team requires a profile.");
            return new D.PublicProfileResponse(x.AppId, x.PlayerId, x.ServerTime, x.Profile == null ? null : new D.PublicProfile(x.Profile.DisplayName, x.Profile.AvatarKey, x.Profile.Locale, x.Profile.Revision,
                x.Team == null ? null : new D.PublicProfileTeam(x.Team.TeamId, x.Team.Name, x.Team.BadgeId)));
        }
    }
}
