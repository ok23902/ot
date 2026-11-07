#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ot
{
    public class Oreg
    {
        public string Name { get; }
        public Type Type => typeof(Dictionary<string, Obj>);

        private Dictionary<string, Obj> objs = new Dictionary<string, Obj>();
        private readonly List<Func<Dictionary<string, Obj>, Dictionary<string, Obj>>> Sets
            = new List<Func<Dictionary<string, Obj>, Dictionary<string, Obj>>>();

        private readonly object SetsLock = new object();
        private readonly object CmtLock = new object();
        private readonly object ValueLock = new object();

        private long logicalTick = 0;

        public Oreg(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        // ============================================================
        // 論理時計
        // ============================================================

        public long NextTick() => Interlocked.Increment(ref logicalTick);

        // ============================================================
        // 追加・削除（遅延）
        // ============================================================

        public void Add(Obj obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.Oreg = this;
            Set(d =>
            {
                var n = new Dictionary<string, Obj>(d);
                n[obj.Id] = obj;
                return n;
            });
        }

        public void Remove(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            Set(d =>
            {
                var n = new Dictionary<string, Obj>(d);
                n.Remove(id);
                return n;
            });
        }

        public void Set(Func<Dictionary<string, Obj>, Dictionary<string, Obj>> lambda)
        {
            if (lambda == null) throw new ArgumentNullException(nameof(lambda));
            lock (SetsLock) Sets.Add(lambda);
        }

        public void Cmt()
        {
            lock (CmtLock)
            {
                List<Func<Dictionary<string, Obj>, Dictionary<string, Obj>>> pending;
                lock (SetsLock)
                {
                    pending = new List<Func<Dictionary<string, Obj>, Dictionary<string, Obj>>>(Sets);
                    Sets.Clear();
                }
                if (pending.Count == 0) return;

                Dictionary<string, Obj> pv;
                lock (ValueLock) pv = objs;
                foreach (var lam in pending) pv = lam(pv);
                lock (ValueLock) objs = pv;
            }
        }

        // ============================================================
        // 取得
        // ============================================================

        public Obj Get(string id)
        {
            if (id == null) return null;
            lock (ValueLock) return objs.TryGetValue(id, out var o) ? o : null;
        }
        public IEnumerable<Obj> GetAll()
        {
            lock (ValueLock) return new List<Obj>(objs.Values);
        }

        // ============================================================
        // Program.cs 層からの一括操作
        // ============================================================

        /// <summary>
        /// 全 Obj（または特定 Obj）に Evt を送る。各 Obj は独立した新規 ChainId で処理する。
        /// </summary>
        public async Task Evt(Query query, Args args, string targetobj = "")
        {
            var targets = SnapshotTargets(targetobj);

            var tasks = new List<Task>();
            foreach (var obj in targets)
            {
                var prop = Propagation.Start(obj.NewChain());
                tasks.Add(obj.Evt(query, args, prop));
            }
            if (tasks.Count > 0) await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 全 Obj（または特定 Obj）から Get する。結果は1つの GetHandle にマージ。
        /// </summary>
        public GetHandle Get(Query query, Propagation prop, string targetobj = "")
        {
            var targets = SnapshotTargets(targetobj);
            var handle = new GetHandle();
            foreach (var obj in targets)
            {
                var h = obj.Get(query, prop);
                foreach (var kv in h.Snapshot())
                    handle.Write(kv.Key, kv.Value);
            }
            return handle;
        }

        private List<Obj> SnapshotTargets(string targetobj)
        {
            lock (ValueLock)
            {
                if (string.IsNullOrEmpty(targetobj))
                    return new List<Obj>(objs.Values);
                if (objs.TryGetValue(targetobj, out var o))
                    return new List<Obj> { o };
                return new List<Obj>();
            }
        }

        public override string ToString()
        {
            lock (ValueLock) return Name + "=[" + string.Join(",", objs.Keys) + "]";
        }
    }
}