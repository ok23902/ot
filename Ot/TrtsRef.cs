#nullable disable
using System;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// Trts 実体への参照（エイリアス専用型）。
    /// </summary>
    public class TrtsRef : IField
    {
        private readonly Field<string> targetField;
        private readonly Obj owner;

        public string Name { get; }
        public string Target => targetField.Value;

        public TrtsRef(Obj owner, string name, string target)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            targetField = new Field<string>("target", target ?? "");
        }

        public void Set(string newTarget) => targetField.Set(newTarget ?? "");

        public void Cmt()
        {
            targetField.Cmt();
            Resolve()?.Cmt();
        }

        internal Trts Resolve()
        {
            var target = targetField.Value;
            if (string.IsNullOrEmpty(target)) return null;

            var f = owner.GetField(target);
            if (f is TrtsRef next) return next.Resolve();
            if (f is Trts t) return t;
            return null;
        }

        public Type Type => Resolve()?.Type;
        public string TypeName => Resolve()?.TypeName ?? "";
        public object Value => Resolve()?.Value;

        public void Add(string traitId) => Resolve()?.Add(traitId);
        public void Remove(string traitId) => Resolve()?.Remove(traitId);
        public Trait Get(string traitId) => Resolve()?.Get(traitId);
        public Task Evt(Args args, Propagation prop) => Resolve()?.Evt(args, prop) ?? Task.CompletedTask;

        public override string ToString() => $"TrtsRef({Name})->{Target}";
    }
}