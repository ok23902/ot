#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// Obj への外部参照。Evt / Get のみ公開（書き込みの経路は構造的に不可能）。
    /// </summary>
    public interface IObjRef
    {
        string Id { get; }
        Task Evt(Query query, Args args, Propagation prop);
        GetHandle Get(Query query, Propagation prop);
    }

    internal sealed class ObjRef : IObjRef
    {
        private readonly Obj target;

        internal ObjRef(Obj target)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public string Id => target.Id;
        public Task Evt(Query query, Args args, Propagation prop) => target.Evt(query, args, prop);
        public GetHandle Get(Query query, Propagation prop) => target.Get(query, prop);
    }

    /// <summary>
    /// Obj の集まり。Value は ObjID のリスト（文字列）。
    /// Evt は fire-and-forget。Get で参照（IObjRef）を返す。
    /// </summary>
    public class Objs : Field<IReadOnlyList<string>>
    {
        private readonly Obj owner;

        public Objs(string name, Obj owner) : base(name, new List<string>())
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        // ============================================================
        // Value（C1: 露出防止。読み取り専用のコピーを返す）
        // ============================================================

        public override IReadOnlyList<string> Value
        {
            get { return new List<string>(base.Value).AsReadOnly(); }
        }

        // ============================================================
        // Add / Remove（ObjID の糖衣）
        // ============================================================

        public void Add(string objId)
        {
            if (objId == null) throw new ArgumentNullException(nameof(objId));
            Set(l => { var n = new List<string>(l); n.Add(objId); return n; });
        }

        public void Remove(string objId)
        {
            if (objId == null) throw new ArgumentNullException(nameof(objId));
            Set(l => { var n = new List<string>(l); n.Remove(objId); return n; });
        }

        // ============================================================
        // Get（ObjID → 参照）
        // ============================================================

        /// <summary>ObjID から参照を取得する。存在しなければ null。</summary>
        public IObjRef Get(string objId)
        {
            if (objId == null) return null;
            var obj = owner.Oreg != null ? owner.Oreg.Get(objId) : null;
            if (obj == null) return null;
            return new ObjRef(obj);
        }

        // ============================================================
        // Evt（B4: fire-and-forget で子 Obj に配布）
        // ============================================================

        /// <summary>
        /// この Objs Field 内の全 Obj に Evt を配る。
        /// 各子は Obj.Evt で去重 + Router 委譲する。
        /// 子の完了は待たない（fire-and-forget）。例外はログに流す。
        /// </summary>
        public Task Evt(Query query, Args args, Propagation prop)
        {
            foreach (var objId in base.Value)
            {
                var obj = owner.Oreg != null ? owner.Oreg.Get(objId) : null;
                if (obj == null) continue;
                _ = SafeFire(obj.Evt(query, args, prop));
            }
            return Task.CompletedTask;
        }

        // ============================================================
        // Get（共有 Context で子に再帰）
        // ============================================================

        /// <summary>公開: 各子のデータを集約して返す。</summary>
        public GetHandle Get(Query query, Propagation prop)
        {
            var ctx = new GetContext();
            GetInternal(query, prop, ctx);
            return ctx.Handle;
        }

        /// <summary>内部: 共有 Context で各子に再帰。</summary>
        internal void GetInternal(Query query, Propagation prop, GetContext ctx)
        {
            foreach (var objId in base.Value)
            {
                var obj = owner.Oreg != null ? owner.Oreg.Get(objId) : null;
                if (obj == null) continue;
                obj.GetInternal(query, prop, ctx);
            }
        }

        // ============================================================
        // ヘルパー
        // ============================================================

        private static async Task SafeFire(Task task)
        {
            try { await task; }
            catch (Exception ex) { Debug.WriteLine($"[O/T] Objs.Evt 例外: {ex}"); }
        }

        public override string ToString()
            => Name + "=[" + string.Join(",", base.Value) + "]";
    }
}