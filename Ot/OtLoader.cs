#nullable disable
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ot
{
    /// <summary>
    /// JSON から Obj を構築する。
    /// </summary>
    public static class OtLoader
    {
        // ============================================================
        // 公開 API
        // ============================================================

        /// <summary>
        /// 1つの Obj を JSON から構築して、既存の Oreg に追加する。
        /// 追加後に Oreg.Cmt() を呼ぶ。
        /// </summary>
        public static void AddByJson(string jsonText, Oreg oreg)
        {
            if (jsonText == null) throw new ArgumentNullException(nameof(jsonText));
            if (oreg == null) throw new ArgumentNullException(nameof(oreg));

            var obj = ParseObj(jsonText, oreg);
            oreg.Add(obj);
            oreg.Cmt();
            CleanupDanglingIds(oreg, new[] { obj });
        }

        /// <summary>
        /// マニフェスト（{ "Objs": ["ema.json", ...] }）を読み、参照先を辿って
        /// 全 Obj を1つの Oreg に読み込む。
        /// </summary>
        public static Oreg InitByJson(string manifestText, Func<string, string> readText, string oregName = "world")
        {
            if (manifestText == null) throw new ArgumentNullException(nameof(manifestText));
            if (readText == null) throw new ArgumentNullException(nameof(readText));

            var manifest = JObject.Parse(manifestText);
            var objs = manifest["Objs"] as JArray;
            if (objs == null)
                throw new InvalidOperationException("マニフェストに 'Objs' 配列がありません");

            var oreg = new Oreg(oregName);
            foreach (var token in objs)
            {
                var path = (string)token;
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException("'Objs' に空のパスがあります");
                var jsonText = readText(path);
                if (jsonText == null)
                    throw new InvalidOperationException($"ファイル '{path}' を読み込めません");
                var obj = ParseObj(jsonText, oreg);
                oreg.Add(obj);
            }
            oreg.Cmt();
            CleanupDanglingIds(oreg, oreg.GetAll());
            return oreg;
        }

        // ============================================================
        // パース
        // ============================================================

        private static Obj ParseObj(string jsonText, Oreg oreg)
        {
            var root = JObject.Parse(jsonText);

            // ---- Id（必須）----
            var id = (string)root["Id"];
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("'Id' は必須です");

            // ---- ObjType（任意、既定 BaseObj）----
            var objTypeName = (string)root["ObjType"];
            var type = objTypeName == null
                ? typeof(BaseObj)
                : ResolveType(objTypeName)
                  ?? throw new InvalidOperationException($"ObjType が見つかりません: '{objTypeName}'");

            if (!typeof(Obj).IsAssignableFrom(type))
                throw new InvalidOperationException($"{type.Name} は Obj を継承していません");

            // ---- Rtr / Rts（任意）----
            var rtr = (string)root["Rtr"] ?? "BaseRouter";
            var rts = (string)root["Rts"];   // null なら具象側の既定

            // ---- Obj 生成 ----
            var obj = CreateObj(type, id, rtr, rts, oreg);

            // ---- 各プロパティを分類 ----
            var fldsDefs = new Dictionary<string, JArray>();   // Flds 定義（配列 of string）
            var routesDefs = new Dictionary<string, JObject>();  // Routes 定義（route → string[]）
            var fieldValues = new Dictionary<string, JToken>();  // C# フィールドの値

            foreach (var prop in root.Properties())
            {
                if (prop.Name == "Id" || prop.Name == "ObjType" ||
                    prop.Name == "Rtr" || prop.Name == "Rts") continue;

                var value = prop.Value;
                var field = type.GetField(prop.Name, BindingFlags.Public | BindingFlags.Instance);

                // ---- C# フィールドがない → Flds か Routes の定義（型名でなく JSON の形で判定）----
                if (field == null)
                {
                    if (value.Type == JTokenType.Array)
                    {
                        fldsDefs[prop.Name] = (JArray)value;
                        continue;
                    }
                    if (value.Type == JTokenType.Object)
                    {
                        routesDefs[prop.Name] = (JObject)value;
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"フィールド '{prop.Name}' が {type.Name} に存在しません");
                }

                // ---- C# フィールドがある → 型で分類 ----
                var fieldInstance = field.GetValue(obj);

                if (fieldInstance is Routes && value.Type == JTokenType.Object)
                {
                    routesDefs[prop.Name] = (JObject)value;
                    continue;
                }

                if (fieldInstance is Flds && value.Type == JTokenType.Array)
                {
                    fldsDefs[prop.Name] = (JArray)value;
                    continue;
                }

                fieldValues[prop.Name] = value;
            }

            // ---- 1. 実体フィールド（Objs / Trts / Flds / Routes / Field<T> / Ref）を設定 ----
            foreach (var kv in fieldValues)
            {
                var field = type.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                var fieldInstance = field.GetValue(obj);
                if (fieldInstance == null)
                    throw new InvalidOperationException($"フィールド '{kv.Key}' が null です");
                ApplyFieldValue(obj, fieldInstance, kv.Value, kv.Key);
            }

            // ---- 2. Flds 定義を適用 ----
            foreach (var kv in fldsDefs)
            {
                var field = type.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"Flds フィールド '{kv.Key}' が {type.Name} に存在しません");
                var flds = field.GetValue(obj) as Flds
                    ?? throw new InvalidOperationException($"'{kv.Key}' は Flds 型ではありません");

                var names = kv.Value.Select(t => (string)t).ToList();
                foreach (var n in names)
                {
                    if (type.GetField(n, BindingFlags.Public | BindingFlags.Instance) == null)
                        throw new InvalidOperationException(
                            $"Flds '{kv.Key}' の '{n}' が {type.Name} に存在しません");
                }
                flds.Set(names);
                flds.Cmt();
            }

            // ---- 3. Routes 定義を適用 ----
            foreach (var kv in routesDefs)
            {
                var field = type.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"Routes フィールド '{kv.Key}' が {type.Name} に存在しません");
                var routes = field.GetValue(obj) as Routes
                    ?? throw new InvalidOperationException($"'{kv.Key}' は Routes 型ではありません");

                foreach (var routeProp in kv.Value.Properties())
                {
                    var fldsNames = ((JArray)routeProp.Value).Select(t => (string)t).ToList();
                    foreach (var fn in fldsNames)
                    {
                        if (type.GetField(fn, BindingFlags.Public | BindingFlags.Instance) == null)
                            throw new InvalidOperationException(
                                $"Routes '{kv.Key}.{routeProp.Name}' の '{fn}' が {type.Name} に存在しません");
                    }
                    routes.Set(routeProp.Name, fldsNames);
                }
                routes.Cmt();
            }

            // ---- 4. Ref の解決検証 ----
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = field.GetValue(obj);
                switch (val)
                {
                    case RoutesRef rtsRef: _ = rtsRef.Resolve(); break;
                    case FldsRef fldsRef: _ = fldsRef.Resolve(); break;
                    case ObjsRef objsRef: _ = objsRef.Resolve(); break;
                    case TrtsRef trtsRef: _ = trtsRef.Resolve(); break;
                }
            }
            // ---- 5. Rtr の存在検証 ----
            if (Treg.Get<Router>((string)root["Rtr"] ?? "BaseRouter") == null)
                throw new InvalidOperationException(
                    $"Router '{(string)root["Rtr"] ?? "BaseRouter"}' が Treg に登録されていません");

            return obj;
        }

        // ============================================================
        // フィールドの値適用
        // ============================================================

        private static void ApplyFieldValue(Obj obj, object fieldInstance, JToken value, string fieldName)
        {
            switch (fieldInstance)
            {
                // ---- Ref（target 名を文字列で）----
                case RoutesRef rr: rr.Set((string)value); return;
                case FldsRef fr: fr.Set((string)value); return;
                case ObjsRef or: or.Set((string)value); return;
                case TrtsRef tr: tr.Set((string)value); return;

                // ---- Objs（文字列配列）----
                case Objs objs:
                    foreach (var t in (JArray)value) objs.Add((string)t);
                    objs.Cmt();
                    return;

                // ---- Trts（文字列配列）----
                case Trts trts:
                    foreach (var t in (JArray)value) trts.Add((string)t);
                    trts.Cmt();
                    return;

                // ---- Flds（文字列配列）→ ここには来ない（fldsDefs に分岐済み）----
                case Flds _:
                    throw new InvalidOperationException($"Flds '{fieldName}' はここでは処理されません");

                // ---- Routes（JObject）→ ここには来ない（routesDefs に分岐済み）----
                case Routes _:
                    throw new InvalidOperationException($"Routes '{fieldName}' はここでは処理されません");

                // ---- Field<T> ----
                default:
                    if (fieldInstance is IField && IsGenericFieldType(fieldInstance.GetType()))
                    {
                        SetGenericField(fieldInstance, value, fieldName);
                        return;
                    }
                    throw new InvalidOperationException(
                        $"フィールド '{fieldName}' の型 {fieldInstance.GetType().Name} は未対応です");
            }
        }

        private static void SetGenericField(object field, JToken value, string fieldName)
        {
            var ft = field.GetType();
            var inner = ft.GetGenericArguments()[0];

            object parsed;
            try
            {
                parsed = value.ToObject(inner);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"フィールド '{fieldName}' のパースに失敗: {ex.Message}");
            }

            var setM = ft.GetMethod("Set", new[] { inner })
                ?? throw new InvalidOperationException($"'{fieldName}' に Set({inner.Name}) がありません");
            setM.Invoke(field, new[] { parsed });
            ft.GetMethod("Cmt").Invoke(field, null);
        }

        private static bool IsGenericFieldType(Type t)
            => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Field<>);

        // ============================================================
        // ヘルパー
        // ============================================================

        private static Obj CreateObj(Type type, string id, string rtr, string rts, Oreg oreg)
        {
            var ctor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(Oreg) },
                null);

            if (ctor == null)
                throw new InvalidOperationException(
                    $"{type.Name} にコンストラクタ (string, string, string, Oreg) がありません");

            try
            {
                return (Obj)ctor.Invoke(new object[] { id, rtr, rts, oreg });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static Type ResolveType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // 1. 完全修飾名
            var t = Type.GetType(name);
            if (t != null) return t;

            // 2. 全アセンブリで完全修飾名 / Ot. 付き
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(name);
                if (t != null) return t;

                t = asm.GetType("Ot." + name);
                if (t != null) return t;
            }

            // 3. 全アセンブリの全型から、シンプル名で一致する Obj 派生型を探す
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                foreach (var type in types)
                {
                    if (type == null) continue;
                    if (type.Name == name && typeof(Obj).IsAssignableFrom(type))
                        return type;
                }
            }

            return null;
        }
        private static void CleanupDanglingIds(Oreg oreg, IEnumerable<Obj> objs)
        {
            foreach (var obj in objs)
            {
                foreach (var field in obj.Fields)
                {
                    if (field is Objs objsField)
                    {
                        var ids = new List<string>(objsField.Value);
                        foreach (var id in ids)
                        {
                            if (oreg.Get(id) == null)
                                objsField.Remove(id);
                        }
                        objsField.Cmt();
                    }
                }
            }
        }
    }
}