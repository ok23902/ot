#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// Field 名の集合。再利用可能な「まとまり」。
    /// route マップを持たない（Routes が持つ）。
    /// </summary>
    public class Flds : IField
    {
        private readonly Obj owner;
        private HashSet<string> fields = new HashSet<string>();

        private readonly List<Func<HashSet<string>, HashSet<string>>> Sets
            = new List<Func<HashSet<string>, HashSet<string>>>();
        private readonly object SetsLock = new object();
        private readonly object CmtLock = new object();
        private readonly object ValueLock = new object();

        public string Name { get; }
        public Type Type => typeof(HashSet<string>);
        public string TypeName => Type.Name;
        public object Value { get { lock (ValueLock) return new HashSet<string>(fields); } }

        public Flds(string name, Obj owner)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        // ============================================================
        // Set / Cmt
        // ============================================================

        public void Set(Func<HashSet<string>, HashSet<string>> lambda)
        {
            if (lambda == null) throw new ArgumentNullException(nameof(lambda));
            lock (SetsLock) Sets.Add(lambda);
        }

        public void Set(IEnumerable<string> fieldNames)
        {
            var set = new HashSet<string>(fieldNames);
            Set(_ => set);
        }

        public void Add(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            Set(s => { var n = new HashSet<string>(s); n.Add(fieldName); return n; });
        }

        public void Remove(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            Set(s => { var n = new HashSet<string>(s); n.Remove(fieldName); return n; });
        }

        public void Cmt()
        {
            lock (CmtLock)
            {
                List<Func<HashSet<string>, HashSet<string>>> pending;
                lock (SetsLock)
                {
                    pending = new List<Func<HashSet<string>, HashSet<string>>>(Sets);
                    Sets.Clear();
                }
                if (pending.Count == 0) return;

                HashSet<string> pv;
                lock (ValueLock) pv = fields;
                foreach (var lam in pending) pv = lam(pv);
                lock (ValueLock) fields = pv;
            }
        }

        public bool HasField(string name)
        {
            if (name == null) return false;
            lock (ValueLock) return fields.Contains(name);
        }

        public IReadOnlyCollection<string> Fields
        {
            get { lock (ValueLock) return new List<string>(fields).AsReadOnly(); }
        }

        // ============================================================
        // Evt（自分の Field を実行）
        // ============================================================

        /// <summary>
        /// 自分の Field を実行する（Trts 型 → Trts.Evt、Objs 型 → Objs.Evt）。
        /// visited は Routes から渡される実体去重セット。
        /// </summary>
        public Task Evt(Query query, Args args, Propagation prop, HashSet<IField> visited)
        {
            List<string> snapshot;
            lock (ValueLock) snapshot = new List<string>(fields);

            foreach (var fieldName in snapshot)
            {
                var field = owner.GetField(fieldName);
                if (field == null) continue;

                var resolved = ResolveField(field);
                if (resolved == null) continue;
                if (!visited.Add(resolved)) continue;

                switch (resolved)
                {
                    case Trts trts:
                        _ = SafeFire(trts.Evt(args, prop));
                        break;
                    case Objs objs:
                        _ = SafeFire(objs.Evt(query, args, prop));
                        break;
                }
            }
            return Task.CompletedTask;
        }

        private static async Task SafeFire(Task task)
        {
            try { await task; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[O/T] Evt 例外: {ex}"); }
        }

        // ============================================================
        // Get（自分の一般 Field の値を返す。Objs の traverse は Phase 4）
        // ============================================================

        public void Get(Query query, Propagation prop, GetContext ctx)
        {
            List<string> snapshot;
            lock (ValueLock) snapshot = new List<string>(fields);

            foreach (var fieldName in snapshot)
            {
                var field = owner.GetField(fieldName);
                if (field == null) continue;

                var resolved = ResolveField(field);
                if (resolved == null) continue;
                if (!ctx.VisitedFields.Add(resolved)) continue;

                // Trts: 値を持たないので skip
                if (resolved is Trts) continue;

                // Objs: traverse を評価して再帰
                if (resolved is Objs objs)
                {
                    if (prop.Traverse == null) continue;
                    var childProp = prop.Descend();
                    if (childProp.MaxDepth <= 0) continue;
                    foreach (var objId in objs.Value)
                    {
                        var childObj = owner.Oreg?.Get(objId);
                        if (childObj == null) continue;
                        if (!FilterMatcher.MatchesObj(prop.Traverse, childObj)) continue;
                        childObj.GetInternal(query, childProp, ctx);
                    }
                    continue;
                }

                // 一般 Field: フィルタして値を追加
                if (!FilterMatcher.MatchesField(query?.Where, resolved, owner)) continue;
                ctx.Handle.Merge(owner.Id, fieldName, resolved.Value);
            }
        }

        // ============================================================
        // ヘルパー: Ref → 実体
        // ============================================================

        private static IField ResolveField(IField field)
        {
            switch (field)
            {
                case FldsRef fr: return fr.Resolve();
                case ObjsRef orf: return orf.Resolve();
                case TrtsRef trf: return trf.Resolve();
                default: return field;
            }
        }

        public override string ToString()
        {
            lock (ValueLock)
                return Name + "=[" + string.Join(",", fields) + "]";
        }
    }
}