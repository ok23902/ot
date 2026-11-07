#nullable disable
namespace Ot
{
    public class BaseObj : Obj
    {
        public readonly Flds flds_basic;
        public readonly Routes rts1;

        public BaseObj(string id, string rtr, Oreg oreg)
            : this(id, rtr, "rts1", oreg) { }

        public BaseObj(string id, string rtr, string rtsTarget, Oreg oreg)
            : base(id, rtr, rtsTarget ?? "rts1", oreg)   // ← null なら "rts1"
        {
            flds_basic = new Flds("flds_basic", this);
            rts1 = new Routes("rts1", this);
            RegisterField(flds_basic);
            RegisterField(rts1);
        }
    }
}