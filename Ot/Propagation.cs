#nullable disable
using System;

namespace Ot
{
    public sealed class Propagation
    {
        public ChainId ChainId { get; }
        public FilterExpr Traverse { get; }
        public int MaxDepth { get; }

        public Propagation(ChainId chainId, FilterExpr traverse = null, int maxDepth = int.MaxValue)
        {
            ChainId = chainId;
            Traverse = traverse;
            MaxDepth = maxDepth;
        }

        public Propagation Descend()
            => new Propagation(ChainId, Traverse, MaxDepth - 1);

        public static Propagation Start(ChainId chainId)
            => new Propagation(chainId, null, int.MaxValue);
    }
}