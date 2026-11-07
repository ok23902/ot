#nullable disable
using System;
using System.Threading.Tasks;

namespace Ot
{
    public abstract class Router
    {
        public string Id => GetType().Name;
        public Type Type => GetType();

        public abstract int MaxChains { get; }

        public abstract Task Evt(Query query, Args args, Propagation prop, Obj self);
        public abstract void Get(Query query, Propagation prop, Obj self, GetContext ctx);
        public abstract void Cmt(Obj self);
        public abstract void Del(Obj self);
    }

    public sealed class BaseRouter : Router
    {
        public static readonly BaseRouter Instance = new();
        private BaseRouter() { }

        public override int MaxChains => 5;

        public override async Task Evt(Query query, Args args, Propagation prop, Obj self)
        {
            if (!FilterMatcher.MatchesObj(query?.Where, self)) return;
            var rts = self.Rts?.Resolve();
            if (rts == null) return;
            await rts.Evt("Default", query, args, prop);
        }

        // BaseRouter
        public override void Get(Query query, Propagation prop, Obj self, GetContext ctx)
        {
            if (!FilterMatcher.MatchesObj(query?.Where, self)) return;
            var rts = self.Rts?.Resolve();
            if (rts == null) return;
            rts.GetInternal("Default", query, prop, ctx);
        }

        public override void Cmt(Obj self)
        {
            foreach (var f in self.Fields) f.Cmt();
        }

        public override void Del(Obj self)
        {
            if (self.Oreg == null)
                throw new InvalidOperationException($"Obj '{self.Id}' は Oreg に登録されていません");
            self.Oreg.Remove(self.Id);
        }
    }
}