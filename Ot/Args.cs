#nullable disable
using System;
using System.Collections.Generic;

namespace Ot
{
    public sealed class Args
    {
        private readonly Dictionary<string, object> items;

        public Args()
        {
            items = new Dictionary<string, object>();
        }

        public Args(Dictionary<string, object> src)
        {
            items = src == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(src);
        }

        public object this[string key]
            => items.TryGetValue(key, out var v) ? v : null;

        public bool Has(string key) => items.ContainsKey(key);

        public IEnumerable<KeyValuePair<string, object>> All => items;

        public override string ToString()
            => "{" + string.Join(",", items.Keys) + "}";
    }
}