#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// Objs 実体への参照（エイリアス専用型）。
    /// </summary>
    public class ObjsRef : IField
    {
        private readonly Field<string> targetField;
        private readonly Obj owner;

        public string Name { get; }
        public string Target => targetField.Value;

        public ObjsRef(Obj owner, string name, string target)
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

        internal Objs Resolve()
        {
            var target = targetField.Value;
            if (string.IsNullOrEmpty(target)) return null;

            var f = owner.GetField(target);
            if (f is ObjsRef next) return next.Resolve();
            if (f is Objs o) return o;
            return null;
        }

        public Type Type => Resolve()?.Type;
        public string TypeName => Resolve()?.TypeName ?? "";
        public object Value => Resolve()?.Value;

        // 委譲
        public void Add(string objId) => Resolve()?.Add(objId);
        public void Remove(string objId) => Resolve()?.Remove(objId);
        public IObjRef Get(string objId) => Resolve()?.Get(objId);
        public Task Evt(Query query, Args args, Propagation prop) => Resolve()?.Evt(query, args, prop) ?? Task.CompletedTask;
        public GetHandle Get(Query query, Propagation prop) => Resolve()?.Get(query, prop) ?? new GetHandle();

        public override string ToString() => $"ObjsRef({Name})->{Target}";
    }
}