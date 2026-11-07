#nullable disable
using System;

namespace Ot
{
    public interface IField
    {
        string Name { get; }
        Type Type { get; }
        string TypeName { get; }   // ← 追加（= Type.Name）
        object Value { get; }
        void Cmt();
    }
}