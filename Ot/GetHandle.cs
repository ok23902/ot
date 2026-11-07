#nullable disable
using System;
using System.Collections.Generic;

namespace Ot
{
    public sealed class GetHandle
    {
        private readonly Dictionary<string, Dictionary<string, object>> values
            = new Dictionary<string, Dictionary<string, object>>();
        private readonly object Lock = new object();

        /// <summary>結果を書き込む（上書き）。</summary>
        public void Write(string objId, Dictionary<string, object> v)
        {
            if (objId == null) throw new ArgumentNullException(nameof(objId));
            lock (Lock) values[objId] = v ?? new Dictionary<string, object>();
        }

        /// <summary>1つの Field の値を追加する（同じ ObjID に複数 Field をマージ）。</summary>
        public void Merge(string objId, string fieldName, object value)
        {
            if (objId == null) throw new ArgumentNullException(nameof(objId));
            lock (Lock)
            {
                if (!values.TryGetValue(objId, out var d))
                    values[objId] = d = new Dictionary<string, object>();
                d[fieldName] = value;
            }
        }

        public bool Has(string objId)
        {
            if (objId == null) return false;
            lock (Lock) return values.ContainsKey(objId);
        }

        public Dictionary<string, object> this[string objId]
        {
            get
            {
                if (objId == null) return null;
                lock (Lock) return values.TryGetValue(objId, out var v) ? v : null;
            }
        }

        public Dictionary<string, Dictionary<string, object>> Snapshot()
        {
            lock (Lock)
            {
                var copy = new Dictionary<string, Dictionary<string, object>>(values.Count);
                foreach (var kv in values)
                    copy[kv.Key] = new Dictionary<string, object>(kv.Value);
                return copy;
            }
        }

        public int Count { get { lock (Lock) return values.Count; } }
    }
}