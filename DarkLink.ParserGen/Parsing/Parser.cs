using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal partial class Parser<T>
    {
        private readonly ForestToParseTree forestToParseTree;

        private readonly Parser<T>.EarleyParser parser;

        public Parser(Grammar grammar, Dictionary<Production, Func<object[], T>> callbacks)
        {
            parser = new(grammar);
            forestToParseTree = new(callbacks);
        }

        public IEnumerable<T> Parse(IReadOnlyList<TerminalSymbol> tokens)
        {
            var solutions = parser.Parse(tokens);
            return solutions.SelectMany(node => forestToParseTree.Transform(node));
        }
    }
}