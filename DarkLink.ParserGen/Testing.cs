using DarkLink.ParserGen.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    internal class Testing
    {
        public static void Test()
        {
            var grammar1 = new Grammar(
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
            var terminals1 = new TerminalSymbol[]
            {
                new("a"),
                new("a"),
                new("b"),
                new("b"),
                new("a"),
                new("a"),
            };

            var grammar2 = new Grammar(
                new HashSet<NonTerminalSymbol>(new NonTerminalSymbol[]
                {
                    new("S"),
                    new("NP"),
                    new("VP"),
                    new("PP"),
                }),
                new HashSet<TerminalSymbol>(new TerminalSymbol[]
                {
                    new("n"),
                    new("d"),
                    new("p"),
                    new("v"),
                }),
                new HashSet<Production>(new Production[]
                {
                    new(new("S"), new Symbol[]{ new NonTerminalSymbol("NP"), new NonTerminalSymbol("VP") }),
                    new(new("NP"), new Symbol[]{ new TerminalSymbol("n") }),
                    new(new("NP"), new Symbol[]{ new TerminalSymbol("d"), new TerminalSymbol("n") }),
                    new(new("NP"), new Symbol[]{ new NonTerminalSymbol("NP"), new NonTerminalSymbol("PP") }),
                    new(new("VP"), new Symbol[]{ new NonTerminalSymbol("VP"), new NonTerminalSymbol("PP") }),
                    new(new("VP"), new Symbol[]{ new TerminalSymbol("v"), new NonTerminalSymbol("NP") }),
                    new(new("PP"), new Symbol[]{ new TerminalSymbol("p"), new NonTerminalSymbol("NP") }),
                }),
                new("S"));
            var terminals2 = new TerminalSymbol[]
            {
                new("n"),
                new("v"),
                new("d"),
                new("n"),
                new("p"),
                new("d"),
                new("n"),
            };

            //var glrParser = new GLR.Parser(grammar);
            //var glrResults = glrParser.Parse(terminals);

            //var earley2Parser = new Earley.Parser(grammar1);
            //var earley2Results = earley2Parser.Parse(terminals1);

            //var forestToTree = new Earley.ForestToParseTree<object>(new());
            //var result = forestToTree.Transform(earley2Results.First());

            var earley = new Parser<object, string, string>(grammar1, new());
            var result = earley.Parse(terminals1);
        }
    }
}