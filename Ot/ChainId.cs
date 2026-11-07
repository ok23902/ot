#nullable disable
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Ot.App")]
[assembly: InternalsVisibleTo("Ot.Tests")]
namespace Ot
{
    public readonly struct ChainId : IEquatable<ChainId>
    {
        public readonly string RootId;
        public readonly long CallNumber;

        internal ChainId(string rootId, long callNumber)
        {
            RootId = rootId ?? throw new ArgumentNullException(nameof(rootId));
            CallNumber = callNumber;
        }

        public bool Equals(ChainId other)
            => CallNumber == other.CallNumber
               && string.Equals(RootId, other.RootId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ChainId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RootId, CallNumber);

        public static bool operator ==(ChainId a, ChainId b) => a.Equals(b);
        public static bool operator !=(ChainId a, ChainId b) => !a.Equals(b);

        public override string ToString() => $"{RootId}#{CallNumber}";
    }
}