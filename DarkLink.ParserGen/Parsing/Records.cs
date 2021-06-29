using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal abstract record Symbol();

    internal record TerminalSymbol<T>(T Value) : Symbol
    {
        public override string ToString()
            => $"#{Value}";
    }

    internal record TerminalSymbol(string Value) : TerminalSymbol<string>(Value);

    internal record NonTerminalSymbol<T>(T Value) : Symbol
    {
        public override string ToString()
            => $"{Value}";
    }

    internal record NonTerminalSymbol(string Value) : NonTerminalSymbol<string>(Value);

    internal class Word
    {
        public Word(params Symbol[] symbols)
        {
            Symbols = symbols;
        }

        public static Word Empty { get; } = new Word();

        public bool IsEmpty => Length == 0;

        public int Length => Symbols.Count;

        public IReadOnlyList<Symbol> Symbols { get; }

        public Symbol this[int index] => Symbols[index];

        public static implicit operator Word(Symbol[] symbols)
            => new Word(symbols.ToArray());

        public override bool Equals(object obj)
        {
            if (obj is Word other)
                return Symbols.SequenceEqual(other.Symbols);
            return false;
        }

        public override int GetHashCode()
            => Symbols.Aggregate(0, (acc, cur) => acc ^ cur.GetHashCode());

        public override string ToString()
            => string.Join(", ", Symbols);
    }

    internal record Production<TNT>(NonTerminalSymbol<TNT> Left, Word Right)
    {
        public override string ToString()
            => $"{Left} -> {(Right.IsEmpty ? "ε" : string.Join(" ", Right.Symbols))}";
    }

    internal record Production : Production<string>
    {
        public Production(NonTerminalSymbol Left, Word Right)
            : base(Left, Right) { }
    }

    internal record Grammar<TNT, TT>(
        ISet<NonTerminalSymbol<TNT>> Variables,
        ISet<TerminalSymbol<TT>> Alphabet,
        ISet<Production<TNT>> Productions,
        NonTerminalSymbol<TNT> Start);

    internal record Grammar : Grammar<string, string>
    {
        public Grammar(
            ISet<NonTerminalSymbol> Variables,
            ISet<TerminalSymbol> Alphabet,
            ISet<Production> Productions,
            NonTerminalSymbol Start)
            : base(
                  new HashSet<NonTerminalSymbol<string>>(Variables),
                  new HashSet<TerminalSymbol<string>>(Alphabet),
                  new HashSet<Production<string>>(Productions),
                  Start)
        { }
    }
}