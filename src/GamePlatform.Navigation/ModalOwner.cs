using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation
{

    public readonly struct ModalOwner : IEquatable<ModalOwner>
    {
        internal ModalOwner(long value)
        {
            Value = value;
        }
        internal long Value { get; }
        public bool Equals(ModalOwner other) => Value != 0 && Value == other.Value;
        public override bool Equals(object? obj) => obj is ModalOwner other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }
}
