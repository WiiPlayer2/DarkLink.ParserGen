using DarkLink.ParserGen.Parsing;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
    internal class Testing
    {
        public static void Test()
        {
            var grammar = new Grammar(
                new HashSet<NonTerminalSymbol>(new NonTerminalSymbol[]
                {
                    new("S"),
                }),
                new HashSet<TerminalSymbol>(new TerminalSymbol[]
                {
                    new("a"),
                    new("b"),
                }),
                new HashSet<Production>(new Production[]
                {
                    new(new("S"), new Symbol[]{new TerminalSymbol("a"), new NonTerminalSymbol("S"), new TerminalSymbol("a")}),
                    new(new("S"), new Symbol[]{new TerminalSymbol("b"), new NonTerminalSymbol("S"), new TerminalSymbol("b")}),
                    new(new("S"), Array.Empty<Symbol>()),
                }),
                new("S"));

            var terminals = new TerminalSymbol[]
            {
                new("a"),
                new("a"),
                new("b"),
                new("b"),
                new("a"),
                new("a"),
            };

            //var glrParser = new GLR.Parser(grammar);
            //var glrResults = glrParser.Parse(terminals);

            var earleyParser = new Earley.Parser(grammar);
            var earleyResults = earleyParser.Parse(terminals);
        }
    }
}