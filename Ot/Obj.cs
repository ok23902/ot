#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ot
{
    public abstract class Obj
    {
        // ============================================================
        // 必須フィールド
        // ============================================================

        public string Id { get; }
        public Field<string> Rtr { get; }
        public RoutesRef Rts { get; }          // ← Phase 3 で追加
        internal Oreg Oreg { get; set; }

        // ============================================================
        // Field の保持
        // ============================================================

        private readonly List<IField> fields = new List<IField>();
        private readonly Dictionary<string, IField> fieldsByName = new Dictionary<string, IField>();
        internal IReadOnlyList<IField> Fields => fields;

        // ← 4引数に変更
        protected Obj(string id, string routerId, string rtsTarget, Oreg oreg)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Rtr = new Field<string>("Rtr", routerId ?? "BaseRouter");
            Rts = new RoutesRef(this, "Rts", rtsTarget);
            Oreg = oreg;
            RegisterField(Rtr);
            RegisterField(Rts);
        }

        internal void RegisterField(IField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (fieldsByName.ContainsKey(field.Name))
                throw new InvalidOperationException($"Field '{field.Name}' が重複登録されています");
            fields.Add(field);
            if (!string.IsNullOrEmpty(field.Name))
                fieldsByName[field.Name] = field;
        }

        internal IField GetField(string name)
        {
            if (name == null) return null;
            return fieldsByName.TryGetValue(name, out var f) ? f : null;
        }

        // ============================================================
        // ChainId の去重
        // ============================================================

        private sealed class ChainSlot
        {
            public List<long> CallNumbers = new List<long>();
            public long LastUsedTick;
        }

        private readonly Dictionary<string, ChainSlot> ChainsByRoot
            = new Dictionary<string, ChainSlot>();
        private readonly object ChainsLock = new object();

        private long callNumberCounter = DateTime.UtcNow.Ticks;
        private long insertCount = 0;
        private const int TtlTicks = 1000;

        public ChainId NewChain()
            => new ChainId(Id, Interlocked.Increment(ref callNumberCounter));

        internal bool TryAcquireChain(ChainId chainId, int maxChains)
        {
            if (maxChains <= 0) return false;

            lock (ChainsLock)
            {
                if (!ChainsByRoot.TryGetValue(chainId.RootId, out var slot))
                    slot = new ChainSlot();

                if (slot.CallNumbers.Contains(chainId.CallNumber)) return false;

                if (slot.CallNumbers.Count >= maxChains
                    && chainId.CallNumber < slot.CallNumbers[0])
                    return false;

                slot.CallNumbers.Add(chainId.CallNumber);
                slot.CallNumbers.Sort();
                while (slot.CallNumbers.Count > maxChains)
                    slot.CallNumbers.RemoveAt(0);

                var tick = Oreg != null ? Oreg.NextTick() : 0;
                slot.LastUsedTick = tick;
                ChainsByRoot[chainId.RootId] = slot;

                if (++insertCount % 100 == 0)
                {
                    var stale = new List<string>();
                    foreach (var kv in ChainsByRoot)
                        if (tick - kv.Value.LastUsedTick > TtlTicks)
                            stale.Add(kv.Key);
                    foreach (var k in stale) ChainsByRoot.Remove(k);
                }

                return true;
            }
        }

        // ============================================================
        // 外部 API（Router に委譲）
        // ============================================================

        public Task Evt(Query query, Args args, Propagation prop)
        {
            var router = GetRouter();
            if (!TryAcquireChain(prop.ChainId, router.MaxChains)) return Task.CompletedTask;
            return router.Evt(query, args, prop, this);
        }

        /// <summary>自分の Field を読む。結果は GetHandle に集約。</summary>
        public GetHandle Get(Query query, Propagation prop)
        {
            var ctx = new GetContext();
            GetInternal(query, prop, ctx);
            return ctx.Handle;
        }

        /// <summary>伝播中の到達。共有 Context で集約。</summary>
        internal void GetInternal(Query query, Propagation prop, GetContext ctx)
        {
            if (prop.MaxDepth <= 0) return;
            if (!ctx.VisitedObjs.Add(Id)) return;    // 循環防止
            GetRouter().Get(query, prop, this, ctx);
        }

        public void Del() => GetRouter().Del(this);
        public void Cmt() => GetRouter().Cmt(this);

        internal Router GetRouter()
        {
            var r = Treg.Get<Router>(Rtr.Value);
            if (r == null)
                throw new InvalidOperationException(
                    $"Router '{Rtr.Value}' が Treg に登録されていません");
            return r;
        }
    }
}