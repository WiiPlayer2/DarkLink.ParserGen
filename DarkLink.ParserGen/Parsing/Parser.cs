using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal partial class Parser<T, TNT, TT>
    {
        private readonly ForestToParseTree forestToParseTree;

        private readonly Parser<T, TNT, TT>.EarleyParser parser;

        public Parser(Grammar<TNT, TT> grammar, IReadOnlyDictionary<Production<TNT>, Func<object[], T>> callbacks)
        {
            parser = new(grammar);
            forestToParseTree = new(callbacks);
        }

        public T? Parse(IReadOnlyList<Token<TT>> tokens)
        {
            var solution = parser.Parse(tokens);
            return solution is null
                ? default
                : forestToParseTree.Transform(solution).GetValueOrDefault();
        }
    }
}