using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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

        public Either<T?, IEnumerable<SyntaxError<TT>>> Parse(IReadOnlyList<Token<TT>> tokens, CancellationToken cancellationToken = default)
        {
            var result = parser.Parse(tokens, cancellationToken);
            return result.MapLeft(solution => forestToParseTree.Transform(solution, cancellationToken).GetValueOrDefault());
        }
    }
}