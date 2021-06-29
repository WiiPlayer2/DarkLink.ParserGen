using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal abstract record Symbol(string Name);

    internal record TerminalSymbol(string Name) : Symbol(Name)
    {
        public override string ToString()
            => $"#{Name}";
    }

    internal record NonTerminalSymbol(string Name) : Symbol(Name)
    {
        public override string ToString()
            => Name;
    }

    internal record DerivedNonTerminalSymbol(NonTerminalSymbol Base) : NonTerminalSymbol(Base.Name)
    {
        public override string ToString()
            => $"{Name}'";
    }

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

    internal record Production(NonTerminalSymbol Left, Word Right)
    {
        public override string ToString()
            => $"{Left} -> {(Right.IsEmpty ? "ε" : string.Join(" ", Right.Symbols))}";
    }

    internal record Grammar(ISet<NonTerminalSymbol> Variables, ISet<TerminalSymbol> Alphabet, ISet<Production> Productions, NonTerminalSymbol Start);
}