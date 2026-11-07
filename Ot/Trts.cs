#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// Trait の集まり。Value は TraitID のリスト（文字列）。
    /// 実体は Get / Evt のたびに Treg で解決する（二重管理しない）。
    /// </summary>
    public class Trts : Field<IReadOnlyList<string>>
    {
        private readonly Obj owner;
        public new IReadOnlyList<string> Value
        {
            get { return new List<string>(base.Value).AsReadOnly(); }
        }

        public Trts(string name, Obj owner) : base(name, new List<string>())
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        // ============================================================
        // Add / Remove（TraitID の糖衣）
        // ============================================================

        /// <summary>TraitID の追加を予約する。Trait が存在し型が合わなければ例外。</summary>
        public void Add(string traitId)
        {
            if (traitId == null) throw new ArgumentNullException(nameof(traitId));
            var trt = Treg.Get<Trait>(traitId);
            if (trt != null && !trt.Accepts(owner))
                throw new InvalidOperationException(
                    $"Trait '{traitId}' は Obj '{owner.Id}' ({owner.GetType().Name}) にアタッチできません");
            Set(l => { var n = new List<string>(l); n.Add(traitId); return n; });
        }

        public void Remove(string traitId)
        {
            if (traitId == null) throw new ArgumentNullException(nameof(traitId));
            Set(l => { var n = new List<string>(l); n.Remove(traitId); return n; });
        }

        // ============================================================
        // Get（TraitID → 実体）
        // ============================================================

        public Trait Get(string traitId) => Treg.Get<Trait>(traitId);

        // ============================================================
        // Evt（Trait を順に実行）
        // ============================================================

        /// <summary>
        /// この Trts Field 内の全 Trait を実行する。
        /// 存在しない TraitID はスキップ（§2 失敗は無視）。
        /// 型が合わない Trait もスキップ（Add をすり抜けた場合の防御）。
        /// </summary>
        public Task Evt(Args args, Propagation prop)
        {
            foreach (var traitId in Value)
            {
                var trt = Treg.Get<Trait>(traitId);
                if (trt == null) continue;
                if (!trt.Accepts(owner)) continue;
                _ = SafeFire(trt.Evt(args, owner, prop));
            }
            return Task.CompletedTask;
        }

        private static async Task SafeFire(Task task)
        {
            try { await task; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[O/T] Evt 例外: {ex}"); }
        }
    }
}