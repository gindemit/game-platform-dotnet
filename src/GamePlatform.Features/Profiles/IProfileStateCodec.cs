using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    /// <summary>Codec boundary for the local known-field records and frozen semantic command body.</summary>
    public interface IProfileStateCodec
    {
        byte[] EncodeConfirmed(ProfileConfirmed profile);
        ProfileConfirmed DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        byte[] EncodePending(PendingProfileEdit pending);
        PendingProfileEdit DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodePatchCommand(ProfilePatch patch);
        void ValidateExtensions(ReadOnlySpan<byte> extensions);
    }
}
