#nullable disable
using System;
using System.Threading.Tasks;

namespace Ot
{
    /// <summary>
    /// 処理単位。状態を持たないシングルトン。
    /// </summary>
    public abstract class Trait
    {
        public string Id => GetType().Name;
        public Type Type => GetType();

        /// <summary>この Trait を obj にアタッチできるか。基底は全部可。</summary>
        public virtual bool Accepts(Obj obj) => true;

        /// <summary>
        /// イベント処理。派生クラスで override する。
        /// 実行は Ot アセンブリのみ（protected internal）。
        /// </summary>
        protected internal abstract Task Evt(Args args, Obj self, Propagation prop);
    }

    /// <summary>
    /// アタッチ可能な Obj の型を表明する Trait。
    /// </summary>
    public abstract class Trt<TTarget> : Trait where TTarget : Obj
    {
        public override bool Accepts(Obj obj) => obj is TTarget;
    }
}