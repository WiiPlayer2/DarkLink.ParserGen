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
            var grammar1 = new Grammar<string, string>(
                new HashSet<NonTerminalSymbol<string>>(new NonTerminalSymbol<string>[]
                {
                    new("S"),
                }),
                new HashSet<TerminalSymbol<string>>(new TerminalSymbol<string>[]
                {
                    new("a"),
                    new("b"),
                }),
                new HashSet<Production<string>>(new Production<string>[]
                {
                    new(new("S"), new Symbol[]{new TerminalSymbol<string>("a"), new NonTerminalSymbol<string>("S"), new TerminalSymbol<string>("a")}),
                    new(new("S"), new Symbol[]{new TerminalSymbol<string>("b"), new NonTerminalSymbol<string>("S"), new TerminalSymbol<string>("b")}),
                    new(new("S"), Array.Empty<Symbol>()),
                }),
                new("S"));
            var terminals1 = new Token<string>[]
            {
                new(new("a"), "a", 0),
                new(new("a"), "a", 1),
                new(new("b"), "b", 2),
                new(new("b"), "b", 3),
                new(new("a"), "a", 4),
                new(new("a"), "a", 5),
            };

            var grammar2 = new Grammar<string, string>(
                new HashSet<NonTerminalSymbol<string>>(new NonTerminalSymbol<string>[]
                {
                    new("S"),
                    new("NP"),
                    new("VP"),
                    new("PP"),
                }),
                new HashSet<TerminalSymbol<string>>(new TerminalSymbol<string>[]
                {
                    new("n"),
                    new("d"),
                    new("p"),
                    new("v"),
                }),
                new HashSet<Production<string>>(new Production<string>[]
                {
                    new(new("S"), new Symbol[]{ new NonTerminalSymbol<string>("NP"), new NonTerminalSymbol<string>("VP") }),
                    new(new("NP"), new Symbol[]{ new TerminalSymbol<string>("n") }),
                    new(new("NP"), new Symbol[]{ new TerminalSymbol<string>("d"), new TerminalSymbol<string>("n") }),
                    new(new("NP"), new Symbol[]{ new NonTerminalSymbol<string>("NP"), new NonTerminalSymbol<string>("PP") }),
                    new(new("VP"), new Symbol[]{ new NonTerminalSymbol<string>("VP"), new NonTerminalSymbol<string>("PP") }),
                    new(new("VP"), new Symbol[]{ new TerminalSymbol<string>("v"), new NonTerminalSymbol<string>("NP") }),
                    new(new("PP"), new Symbol[]{ new TerminalSymbol<string>("p"), new NonTerminalSymbol<string>("NP") }),
                }),
                new("S"));
            var terminals2 = new Token<string>[]
            {
                new(new("n"), "Marie", 0),
                new(new("v"), "eats", 0),
                new(new("d"), "the", 0),
                new(new("n"), "banana", 0),
                new(new("p"), "of", 0),
                new(new("d"), "the", 0),
                new(new("n"), "house", 0),
            };

            //var glrParser = new GLR.Parser(grammar);
            //var glrResults = glrParser.Parse(terminals);

            //var earley2Parser = new Earley.Parser(grammar1);
            //var earley2Results = earley2Parser.Parse(terminals1);

            //var forestToTree = new Earley.ForestToParseTree<object>(new());
            //var result = forestToTree.Transform(earley2Results.First());

            var earley = new Parser<object, string, string>(grammar1, new());
            var result = earley.Parse(terminals1).ToList();
        }
    }
}