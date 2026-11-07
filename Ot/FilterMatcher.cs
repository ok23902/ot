#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ot
{
    internal static class FilterMatcher
    {
        // ============================================================
        // Obj スケール
        // ============================================================

        public static bool MatchesObj(FilterExpr expr, Obj obj)
        {
            if (expr == null) return true;
            switch (expr)
            {
                case Atom a: return MatchAtomObj(a, obj);
                case And a: return a.Children.All(c => MatchesObj(c, obj));
                case Or o: return o.Children.Any(c => MatchesObj(c, obj));
                case Not n: return !MatchesObj(n.Child, obj);
                default: return true;
            }
        }

        private static bool MatchAtomObj(Atom atom, Obj obj)
        {
            switch (atom.Subject)
            {
                case Q.S_Id:
                    return CompareEq(atom.Op, obj.Id, atom.Value);

                case Q.S_Name:   // 「その Field を持っているか」
                    return obj.GetField(atom.Value as string) != null;

                case Q.S_OwnerId:
                    return CompareEq(atom.Op, obj.Id, atom.Value);

                case Q.S_TypeName:
                    return obj.Fields.Any(f => f.TypeName == atom.Value as string);

                default:
                    return true;
            }
        }

        // ============================================================
        // Field スケール
        // ============================================================

        public static bool MatchesField(FilterExpr expr, IField field, Obj owner)
        {
            if (expr == null) return true;
            switch (expr)
            {
                case Atom a: return MatchAtomField(a, field, owner);
                case And a: return a.Children.All(c => MatchesField(c, field, owner));
                case Or o: return o.Children.Any(c => MatchesField(c, field, owner));
                case Not n: return !MatchesField(n.Child, field, owner);
                default: return true;
            }
        }

        private static bool MatchAtomField(Atom atom, IField field, Obj owner)
        {
            switch (atom.Subject)
            {
                case Q.S_Name:
                    return CompareEq(atom.Op, field.Name, atom.Value);

                case Q.S_OwnerId:
                    return CompareEq(atom.Op, owner?.Id, atom.Value);

                case Q.S_TypeName:
                    return CompareEq(atom.Op, field.TypeName, atom.Value);

                case Q.S_Id:
                    return CompareEq(atom.Op, owner?.Id, atom.Value);

                case Q.S_Value:   // ← 追加
                    return CompareValue(atom.Op, field.Value, atom.Value);

                default:
                    return true;
            }
        }

        // ============================================================
        // 比較ヘルパー
        // ============================================================

        private static bool CompareEq(string op, string actual, object expected)
        {
            switch (op)
            {
                case Q.O_Eq: return actual == expected as string;
                case Q.O_Ne: return actual != expected as string;
                case Q.O_In:
                    if (expected is string[] arr) return Array.IndexOf(arr, actual) >= 0;
                    if (expected is System.Collections.IEnumerable en)
                        foreach (var x in en) if (actual == x as string) return true;
                    return false;
                default: return true;
            }
        }

        private static bool CompareValue(string op, object actual, object expected)
        {
            if (actual == null) return expected == null;
            if (expected == null) return false;
            try
            {
                switch (op)
                {
                    case Q.O_Eq: return actual.Equals(Convert.ChangeType(expected, actual.GetType()));
                    case Q.O_Ne: return !actual.Equals(Convert.ChangeType(expected, actual.GetType()));
                    case Q.O_Lt: return Convert.ToDouble(actual) < Convert.ToDouble(expected);
                    case Q.O_Le: return Convert.ToDouble(actual) <= Convert.ToDouble(expected);
                    case Q.O_Gt: return Convert.ToDouble(actual) > Convert.ToDouble(expected);
                    case Q.O_Ge: return Convert.ToDouble(actual) >= Convert.ToDouble(expected);
                    default: return true;
                }
            }
            catch
            {
                return true;   // 型変換失敗時は寛容に通す
            }
        }
    }
}