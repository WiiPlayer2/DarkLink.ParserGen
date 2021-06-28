using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal class Set<TItem, TSet> : ICollection<TItem>
        where TSet : ISet<TItem>, new()
    {
        private readonly TSet baseSet;

        public Set()
        {
            baseSet = new TSet();
        }

        public int Count => baseSet.Count;

        public bool IsEmpty => Count == 0;

        public bool IsReadOnly => baseSet.IsReadOnly;

        public static Set<TItem, TSet> operator |(Set<TItem, TSet> set1, Set<TItem, TSet> set2)
        {
            var ret = set1.Clone();
            ret.baseSet.UnionWith(set2);
            return ret;
        }

        public bool Add(TItem item) => baseSet.Add(item);

        void ICollection<TItem>.Add(TItem item) => Add(item);

        public bool AddRange(IEnumerable<TItem> items) => items.Aggregate(false, (acc, curr) => Add(curr) || acc);

        public void Clear() => baseSet.Clear();

        public Set<TItem, TSet> Clone()
        {
            var clone = new Set<TItem, TSet>();
            clone.baseSet.UnionWith(baseSet);
            return clone;
        }

        public bool Contains(TItem item) => baseSet.Contains(item);

        public void CopyTo(TItem[] array, int arrayIndex) => baseSet.CopyTo(array, arrayIndex);

        public IEnumerator<TItem> GetEnumerator() => baseSet.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TItem Remove() => Remove(_ => true);

        public TItem Remove(Func<TItem, bool> predicate)
        {
            var first = this.First(predicate);
            Remove(first);
            return first;
        }

        public bool Remove(TItem item) => baseSet.Remove(item);
    }

    internal class Set<T> : Set<T, HashSet<T>>
    {
        public new Set<T> Clone()
        {
            var clone = new Set<T>();
            clone.AddRange(this);
            return clone;
        }
    }
}