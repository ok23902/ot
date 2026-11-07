#nullable disable
using System;

namespace Ot
{
    /// <summary>
    /// FilterExpr の評価時に使う Field 情報。
    /// </summary>
    public readonly struct FieldContext
    {
        public string Name { get; }
        public Type Type { get; }
        public string TypeName { get; }
        public string OwnerId { get; }

        public FieldContext(IField field, Obj owner)
        {
            Name = field?.Name ?? "";
            Type = field?.Type;
            TypeName = field?.TypeName ?? "";
            OwnerId = owner?.Id ?? "";
        }
    }
}