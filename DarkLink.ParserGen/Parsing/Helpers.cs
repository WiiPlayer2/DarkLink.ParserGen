﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public static TerminalSymbol<T> T<T>(T value) => new TerminalSymbol<T>(value);
    }
}