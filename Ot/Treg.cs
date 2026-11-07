#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ot
{
    public static class Treg
    {
        private static readonly Dictionary<string, Trait> traits = new Dictionary<string, Trait>();
        private static readonly Dictionary<string, Router> routers = new Dictionary<string, Router>();
        private static readonly object Lock = new object();

        // ============================================================
        // Get
        // ============================================================

        public static object Get(string id)
        {
            if (id == null) return null;
            lock (Lock)
            {
                if (traits.TryGetValue(id, out var t)) return t;
                if (routers.TryGetValue(id, out var r)) return r;
                return null;
            }
        }

        public static T Get<T>(string id) where T : class => Get(id) as T;

        // ============================================================
        // 登録
        // ============================================================

        public static void RegisterTrait(string id, Trait t)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (t == null) throw new ArgumentNullException(nameof(t));
            lock (Lock)
            {
                if (traits.ContainsKey(id)) throw new InvalidOperationException($"TraitID 重複: '{id}'");
                if (routers.ContainsKey(id)) throw new InvalidOperationException($"ID 衝突(Trait/Router): '{id}'");
                traits[id] = t;
            }
        }

        public static void RegisterRouter(string id, Router r)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (r == null) throw new ArgumentNullException(nameof(r));
            lock (Lock)
            {
                if (routers.ContainsKey(id)) throw new InvalidOperationException($"RouterID 重複: '{id}'");
                if (traits.ContainsKey(id)) throw new InvalidOperationException($"ID 衝突(Router/Trait): '{id}'");
                routers[id] = r;
            }
        }

        // ============================================================
        // Load（Reflection 一括登録）
        // ============================================================

        /// <summary>
        /// ロード済みアセンブリを走査し、Trait / Router 派生型を一括登録する。
        /// - Trait はパラメータなしコンストラクタで生成
        /// - Router は静的 Instance フィールドがあればそれを使う（BaseRouter など）
        ///   なければパラメータなしコンストラクタで生成
        /// </summary>
        public static void Load()
        {
            var traitType = typeof(Trait);
            var routerType = typeof(Router);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type == null) continue;
                    if (type.IsAbstract) continue;
                    if (type.IsGenericTypeDefinition) continue;

                    if (traitType.IsAssignableFrom(type))
                    {
                        TryRegisterTrait(type);
                    }
                    else if (routerType.IsAssignableFrom(type))
                    {
                        TryRegisterRouter(type);
                    }
                }
            }
        }

        private static void TryRegisterTrait(Type type)
        {
            var id = type.Name;
            lock (Lock)
            {
                if (traits.ContainsKey(id) || routers.ContainsKey(id)) return;   // 既登録はスキップ
            }
            try
            {
                var instance = (Trait)Activator.CreateInstance(type, nonPublic: true);
                RegisterTrait(id, instance);
            }
            catch { /* インスタンス化できない型はスキップ */ }
        }

        private static void TryRegisterRouter(Type type)
        {
            var id = type.Name;
            lock (Lock)
            {
                if (routers.ContainsKey(id) || traits.ContainsKey(id)) return;
            }
            try
            {
                // 静的 Instance フィールドを優先
                var instanceField = type.GetField("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (instanceField != null && typeof(Router).IsAssignableFrom(instanceField.FieldType))
                {
                    var instance = (Router)instanceField.GetValue(null);
                    if (instance != null)
                    {
                        RegisterRouter(id, instance);
                        return;
                    }
                }
                var r = (Router)Activator.CreateInstance(type, nonPublic: true);
                RegisterRouter(id, r);
            }
            catch { /* スキップ */ }
        }

        // ============================================================
        // Reset / Domain Reload 対策
        // ============================================================

        public static void Reset()
        {
            lock (Lock) { traits.Clear(); routers.Clear(); }
        }

#if UNITY_5_3_OR_NEWER
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnUnityDomainReload() { Reset(); }
#endif
    }
}