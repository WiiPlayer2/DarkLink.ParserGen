using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal struct Option
    {
        public static OptionNone None { get; }

        public static Option<T> Some<T>(T value) => value;
    }

    internal struct Option<T> : IEnumerable<T>
    {
        private readonly bool isSome;

        private readonly T value;

        public Option(T value)
        {
            isSome = true;
            this.value = value;
        }

        public static Option<T> None { get; }

        public static implicit operator Option<T>(OptionNone none) => None;

        public static implicit operator Option<T>(T value) => new Option<T>(value);

        public IEnumerator<T> GetEnumerator()
            => Match(v => new[] { v }, Enumerable.Empty<T>).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T? GetValueOrDefault()
            => Match<T?>(_ => _, () => default);

        public Option<TResult> Map<TResult>(Func<T, TResult> map)
                                    => Match<Option<TResult>>(v => map(v), () => Option.None);

        public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
                    => isSome ? onSome(value) : onNone.Invoke();

        public void Match(Action<T> onSome, Action? onNone = null)
        {
            if (isSome)
                onSome(value);
            else
                onNone?.Invoke();
        }
    }

    internal struct OptionNone { }

    internal static class Extensions
    {
        public static bool IsEmpty<T>(this IEnumerable<T> sequence)
            => !sequence.Any();

        public static T Remove<T>(this ICollection<T> collection, Func<T, bool> predicate)
        {
            var item = collection.First(predicate);
            collection.Remove(item);
            return item;
        }

        public static T Remove<T>(this ICollection<T> collection)
            => collection.Remove(_ => true);

        public static ISet<T> ToSet<T>(this IEnumerable<T> sequence)
            => new HashSet<T>(sequence);

        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> sequence)
                    => sequence.Where(o => o is not null).Cast<T>();
    }

    internal static class G
    {
        public static Grammar<TNT, TT> Create<TNT, TT>(ISet<Production<TNT>> productions, TNT Start)
            where TNT : Enum
            where TT : Enum
        {
            var variables = new HashSet<NonTerminalSymbol<TNT>>(Enum.GetValues(typeof(TNT)).Cast<TNT>().Select(o => new NonTerminalSymbol<TNT>(o)));
            var alphabet = new HashSet<TerminalSymbol<TT>>(Enum.GetValues(typeof(TT)).Cast<TT>().Select(o => new TerminalSymbol<TT>(o)));
            return new Grammar<TNT, TT>(variables, alphabet, productions, new NonTerminalSymbol<TNT>(Start));
        }

        public static NonTerminalSymbol<T> NT<T>(T value) => new NonTerminalSymbol<T>(value);

        public static Production<TNT> P<TNT>(TNT left, params Symbol[] right)
            => new Production<TNT>(NT(left), new Word(right));

        public static TerminalSymbol<T> T<T>(T value) => new TerminalSymbol<T>(value);
    }

    internal static class P
    {
        public static Func<object[], TResult> Delegate<TResult>(Delegate @delegate)
            => args => (TResult)@delegate.DynamicInvoke(args);
    }

    internal class AstBuilder<T, TNT>
    {
        private readonly Dictionary<Production<TNT>, Func<object[], T>> callbacks = new();

        public IReadOnlyDictionary<Production<TNT>, Func<object[], T>> Callbacks => callbacks;

#pragma warning disable CS8603 // Possible null reference return.

        protected T IGNORE(object[] args) => default;

#pragma warning restore CS8603 // Possible null reference return.

        protected Func<object[], T> MAP(string methodName, params int[] indices)
        {
            var methodInfo = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            return args =>
            {
                var newArgs = new object[indices.Length];
                for (var i = 0; i < newArgs.Length; i++)
                    newArgs[i] = args[indices[i]];
                return (T)methodInfo.Invoke(this, newArgs);
            };
        }

        protected T PASS(object[] args) => (T)args[0];

        protected void R(Production<TNT> production, string methodName)
        {
            var methodInfo = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            callbacks.Add(production, args => (T)methodInfo.Invoke(this, args));
        }

        protected void R(Production<TNT> production, Func<object[], T> func)
            => callbacks.Add(production, func);

        protected Func<object[], T> SELECT(int index) => args => (T)args[index];
    }

    [DebuggerDisplay("Count = {Count,nq}")]
    internal class OrderedSet<T> : ICollection<T>
    {
        private readonly List<T> list = new();

        public int Count => list.Count;

        public bool IsReadOnly { get; } = false;

        public bool Add(T item)
        {
            if (list.Contains(item))
                return false;
            list.Add(item);
            return true;
        }

        void ICollection<T>.Add(T item) => Add(item);

        public void Clear() => list.Clear();

        public bool Contains(T item) => list.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Remove(T item) => list.Remove(item);

        private class Enumerator : IEnumerator<T>
        {
            private readonly OrderedSet<T> set;

            private int currentIndex = -1;

            public Enumerator(OrderedSet<T> set)
            {
                this.set = set;
            }

            public T Current => set.list[currentIndex];

#pragma warning disable CS8603 // Possible null reference return.

            object IEnumerator.Current => Current;

#pragma warning restore CS8603 // Possible null reference return.

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                currentIndex++;
                return currentIndex < set.list.Count;
            }

            public void Reset()
            {
                currentIndex = -1;
            }
        }
    }
}