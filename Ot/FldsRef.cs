#nullable disable
using System;

namespace Ot
{
    /// <summary>
    /// Flds 実体への参照（エイリアス専用型）。
    /// 実体を持たない。Set(string) だけ自身が持つ（遅延）。
    /// </summary>
    public class FldsRef : IField
    {
        private readonly Field<string> targetField;
        private readonly Obj owner;

        public string Name { get; }
        public string Target => targetField.Value;

        public FldsRef(Obj owner, string name, string target)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            targetField = new Field<string>("target", target ?? "");
        }

        public void Set(string newTarget)
            => targetField.Set(newTarget ?? "");

        public void Cmt()
        {
            targetField.Cmt();
            Resolve()?.Cmt();
        }

        internal Flds Resolve()
        {
            var target = targetField.Value;
            if (string.IsNullOrEmpty(target)) return null;

            var f = owner.GetField(target);
            if (f is FldsRef next) return next.Resolve();
            if (f is Flds fd) return fd;
            return null;
        }

        public Type Type => Resolve()?.Type;
        public string TypeName => Resolve()?.TypeName ?? "";
        public object Value => Resolve()?.Value;

        public override string ToString() => $"FldsRef({Name})->{Target}";
    }
}