#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ot
{
    public abstract class FilterExpr
    {
        protected FilterExpr() { }
    }

    public sealed class Atom : FilterExpr
    {
        public string Subject { get; private set; }
        public string Op { get; private set; }
        public object Value { get; private set; }

        private Atom() { }

        public Atom(string subject, string op, object value)
        {
            Subject = subject ?? throw new ArgumentNullException(nameof(subject));
            Op = op ?? throw new ArgumentNullException(nameof(op));
            Value = value;
        }
    }

    public sealed class And : FilterExpr
    {
        public List<FilterExpr> Children { get; private set; } = new();
        private And() { }
        public And(IEnumerable<FilterExpr> children) { Children = children.ToList(); }
    }

    public sealed class Or : FilterExpr
    {
        public List<FilterExpr> Children { get; private set; } = new();
        private Or() { }
        public Or(IEnumerable<FilterExpr> children) { Children = children.ToList(); }
    }

    public sealed class Not : FilterExpr
    {
        public FilterExpr Child { get; private set; }
        private Not() { }
        public Not(FilterExpr child) { Child = child ?? throw new ArgumentNullException(nameof(child)); }
    }

    public static class Q
    {
        // ---- Subject 定数 ----
        public const string S_Name = "f.Name";
        public const string S_Value = "f.Value";
        public const string S_Type = "f.Type";
        public const string S_TypeName = "f.TypeName";
        public const string S_OwnerId = "f.OwnerId";
        public const string S_Routes = "f.Routes";
        public const string S_Id = "o.Id";

        // ---- Op 定数（単一値）----
        public const string O_Eq = "==";
        public const string O_Ne = "!=";
        public const string O_Lt = "<";
        public const string O_Le = "<=";
        public const string O_Gt = ">";
        public const string O_Ge = ">=";
        public const string O_In = "in";
        public const string O_Contains = "contains";
        public const string O_Starts = "startsWith";
        public const string O_Ends = "endsWith";

        // ---- Op 定数（集合）----
        public const string O_Intersects = "intersects";
        public const string O_SupersetOf = "supersetOf";
        public const string O_Disjoint = "disjoint";

        // ---- Field 名 ----
        public static FilterExpr Name(string n) => new Atom(S_Name, O_Eq, n);
        public static FilterExpr Names(params string[] ns) => new Atom(S_Name, O_In, ns);

        // ---- Obj の Id ----
        public static FilterExpr Id(string id) => new Atom(S_Id, O_Eq, id);
        public static FilterExpr Ids(params string[] ids) => new Atom(S_Id, O_In, ids);

        // ---- 所有 Obj ----
        public static FilterExpr Owner(string ownerId) => new Atom(S_OwnerId, O_Eq, ownerId);

        // ---- 型 ----
        public static FilterExpr Type<T>() => new Atom(S_TypeName, O_Eq, typeof(T).Name);
        public static FilterExpr TypeName(string tn) => new Atom(S_TypeName, O_Eq, tn);

        // ---- route 所属 ----
        public static FilterExpr InRoute(string route) => new Atom(S_Routes, O_Contains, route);
        public static FilterExpr InAnyRoute(params string[] routes) => new Atom(S_Routes, O_Intersects, routes);
        public static FilterExpr InAllRoutes(params string[] routes) => new Atom(S_Routes, O_SupersetOf, routes);
        public static FilterExpr NotInRoute(string route) => new Atom(S_Routes, O_Disjoint, new[] { route });

        // ---- 論理 ----
        public static FilterExpr And(params FilterExpr[] xs) => new And(xs);
        public static FilterExpr Or(params FilterExpr[] xs) => new Or(xs);
        public static FilterExpr Not(FilterExpr x) => new Not(x);
        public static FilterExpr Val(string op, object v) => new Atom(S_Value, op, v);

        // ---- 定数 ----
        // 空の And = true（vacuous truth）、空の Or = false
        public static FilterExpr True { get; } = new And(Array.Empty<FilterExpr>());
        public static FilterExpr False { get; } = new Or(Array.Empty<FilterExpr>());
    }
}