#nullable disable
using System;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// Routes 実体への参照（エイリアス専用型）。Obj.Rts の型。
    /// </summary>
    public class RoutesRef : IField
    {
        private readonly Field<string> targetField;
        private readonly Obj owner;

        public string Name { get; }
        public string Target => targetField.Value;

        public RoutesRef(Obj owner, string name, string target)
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

        internal Routes Resolve()
        {
            var target = targetField.Value;
            if (string.IsNullOrEmpty(target)) return null;

            var f = owner.GetField(target);
            if (f is RoutesRef next) return next.Resolve();
            if (f is Routes r) return r;
            return null;
        }

        public Type Type => Resolve()?.Type;
        public string TypeName => Resolve()?.TypeName ?? "";
        public object Value => Resolve()?.Value;

        public bool HasRoute(string route) => Resolve()?.HasRoute(route) ?? false;
        public Task Evt(string route, Query query, Args args, Propagation prop)
            => Resolve()?.Evt(route, query, args, prop) ?? Task.CompletedTask;
        public GetHandle Get(string route, Query query, Propagation prop)
            => Resolve()?.Get(route, query, prop) ?? new GetHandle();

        public override string ToString() => $"RoutesRef({Name})->{Target}";
    }
}