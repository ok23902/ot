#nullable disable
using System;
using System.Collections.Generic;

namespace Ot
{
    public class Field<T> : IField
    {
        private readonly object CmtLock = new object();
        private readonly object SetsLock = new object();
        private readonly object ValueLock = new object();

        private List<Func<T, T>> Sets = new List<Func<T, T>>();
        private T value;

        public string Name { get; }
        public Type Type => typeof(T);
        public string TypeName => Type.Name;   // ← 追加

        public Field(string name, T initialValue)
        {
            Name = name ?? string.Empty;
            value = initialValue;
        }

        // Field<T>
        public virtual T Value { get { lock (ValueLock) { return value; } } }

        object IField.Value => Value;

        public void Set(Func<T, T> lambda)
        {
            if (lambda == null) throw new ArgumentNullException(nameof(lambda));
            lock (SetsLock) { Sets.Add(lambda); }
        }

        public void Set(T v) => Set(_ => v);

        public void Cmt()
        {
            lock (CmtLock)
            {
                List<Func<T, T>> pending;
                lock (SetsLock)
                {
                    pending = Sets;
                    Sets = new List<Func<T, T>>();
                }

                if (pending.Count == 0) return;

                T pv;
                lock (ValueLock) pv = value;

                foreach (var lam in pending) pv = lam(pv);

                lock (ValueLock) value = pv;
            }
        }

        public override string ToString() => $"{Name}={Value}";
    }
}