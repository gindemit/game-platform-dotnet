using System;
using System.Collections.Generic;
using System.Linq;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeProofScanResult
    {
        public NativeProofScanResult(IEnumerable<NativeProof> proofs, int unresolvedCount)
        {
            if (unresolvedCount < 0 || unresolvedCount > 4096)
                throw new ArgumentOutOfRangeException(nameof(unresolvedCount));
            Proofs = Array.AsReadOnly((proofs ?? throw new ArgumentNullException(nameof(proofs))).ToArray());
            if (Proofs.Count > 4096 || Proofs.Any(proof => proof == null))
                throw new ArgumentException("Native scan exceeds its bound.", nameof(proofs));
            UnresolvedCount = unresolvedCount;
        }
        public IReadOnlyList<NativeProof> Proofs { get; }
        public int UnresolvedCount { get; }
    }
}
