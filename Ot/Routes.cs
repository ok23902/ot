#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// route 名 → Flds 名のリスト。
    /// Evt / Get は内部で Flds を回し、実体去重する。
    /// </summary>
    public class Routes : IField
    {
        private readonly Obj owner;
        private Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();

        private readonly List<Func<Dictionary<string, List<string>>, Dictionary<string, List<string>>>> Sets
            = new List<Func<Dictionary<string, List<string>>, Dictionary<string, List<string>>>>();
        private readonly object SetsLock = new object();
        private readonly object CmtLock = new object();
        private readonly object ValueLock = new object();

        public string Name { get; }
        public Type Type => typeof(Dictionary<string, List<string>>);
        public string TypeName => Type.Name;
        public object Value { get { lock (ValueLock) return CloneMap(map); } }

        public Routes(string name, Obj owner)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        // ============================================================
        // Set / Cmt
        // ============================================================

        public void Set(Func<Dictionary<string, List<string>>, Dictionary<string, List<string>>> lambda)
        {
            if (lambda == null) throw new ArgumentNullException(nameof(lambda));
            lock (SetsLock) Sets.Add(lambda);
        }

        public void Set(string route, IEnumerable<string> fldsNames)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            var list = new List<string>(fldsNames);
            Set(m => { var n = CloneMap(m); n[route] = list; return n; });
        }

        public void Cmt()
        {
            lock (CmtLock)
            {
                List<Func<Dictionary<string, List<string>>, Dictionary<string, List<string>>>> pending;
                lock (SetsLock)
                {
                    pending = new List<Func<Dictionary<string, List<string>>, Dictionary<string, List<string>>>>(Sets);
                    Sets.Clear();
                }
                if (pending.Count == 0) return;

                Dictionary<string, List<string>> pv;
                lock (ValueLock) pv = map;
                foreach (var lam in pending) pv = lam(pv);
                lock (ValueLock) map = pv;
            }
        }

        public bool HasRoute(string route)
        {
            if (route == null) return false;
            lock (ValueLock) return map.ContainsKey(route);
        }

        // ============================================================
        // Evt
        // ============================================================

        public Task Evt(string route, Query query, Args args, Propagation prop)
        {
            List<string> names;
            lock (ValueLock)
            {
                if (!map.TryGetValue(route, out var list)) return Task.CompletedTask;
                names = new List<string>(list);
            }

            var visited = new HashSet<IField>();
            foreach (var name in names)
            {
                var flds = ResolveFlds(name);
                if (flds == null) continue;
                _ = SafeFire(flds.Evt(query, args, prop, visited));
            }
            return Task.CompletedTask;
        }

        private static async Task SafeFire(Task task)
        {
            try { await task; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[O/T] Evt 例外: {ex}"); }
        }

        // ============================================================
        // Get
        // ============================================================

        public GetHandle Get(string route, Query query, Propagation prop)
        {
            var ctx = new GetContext();
            GetInternal(route, query, prop, ctx);
            return ctx.Handle;
        }

        // 内部: 共有 Context で集約
        internal void GetInternal(string route, Query query, Propagation prop, GetContext ctx)
        {
            List<string> names;
            lock (ValueLock)
            {
                if (!map.TryGetValue(route, out var list)) return;
                names = new List<string>(list);
            }

            foreach (var name in names)
            {
                var flds = ResolveFlds(name);
                if (flds == null) continue;
                flds.Get(query, prop, ctx);
            }
        }

        // ============================================================
        // ヘルパー
        // ============================================================

        private Flds ResolveFlds(string name)
        {
            var field = owner.GetField(name);
            if (field is Flds f) return f;
            if (field is FldsRef fr) return fr.Resolve();
            return null;
        }

        private static Dictionary<string, List<string>> CloneMap(Dictionary<string, List<string>> src)
        {
            var n = new Dictionary<string, List<string>>(src.Count);
            foreach (var kv in src) n[kv.Key] = new List<string>(kv.Value);
            return n;
        }

        public override string ToString()
        {
            lock (ValueLock)
                return Name + "=[" + string.Join(",", map.Keys) + "]";
        }
    }
}