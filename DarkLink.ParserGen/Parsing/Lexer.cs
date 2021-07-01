using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal partial class Lexer<TT>
    {
        private readonly bool ignoreUndefined;

        private readonly IReadOnlyDictionary<TT, Rule> rules;

        public Lexer(IReadOnlyDictionary<TT, Rule> rules, bool ignoreUndefined = true)
        {
            this.rules = rules;
            this.ignoreUndefined = ignoreUndefined;
        }

        public IEnumerable<Token<TT>> Lex(string input)
            => new Enumerable(() => new Lexing(this, input));

        public IEnumerable<Token<TT>> Lex(Stream stream)
            => new Enumerable(() => new Lexing(this, stream));

        public IEnumerable<Token<TT>> Lex(TextReader reader)
            => new Enumerable(() => new Lexing(this, reader));

        private class Enumerable : IEnumerable<Token<TT>>
        {
            private readonly Func<IEnumerator<Token<TT>>> enumeratorFunc;

            public Enumerable(Func<IEnumerator<Token<TT>>> enumeratorFunc)
            {
                this.enumeratorFunc = enumeratorFunc;
            }

            public IEnumerator<Token<TT>> GetEnumerator() => enumeratorFunc();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class Lexing : IEnumerator<Token<TT>>
        {
            private readonly Lexer<TT> lexer;

            private readonly TextReader reader;

            private int currentIndex = 0;

            private string? input;

            public Lexing(Lexer<TT> lexer, string input)
                : this(lexer, new StringReader(input)) { }

            public Lexing(Lexer<TT> lexer, Stream stream)
                : this(lexer, new StreamReader(stream)) { }

            public Lexing(Lexer<TT> lexer, TextReader reader)
            {
                this.lexer = lexer;
                this.reader = reader;
                Current = new(null, string.Empty, -1);
            }

            public Token<TT> Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                input = null;
                reader.Dispose(); // really?
            }

            public bool MoveNext()
            {
                input ??= reader.ReadToEnd();
                if (currentIndex == input.Length)
                    return false;

                var (symbol, match) = lexer.rules
                    .Select(kv => (Type: kv.Key, Match: kv.Value.Match(input, currentIndex)))
                    .Where(tuple => tuple.Match.Success && tuple.Match.Length > 0)
                    .OrderBy(tuple => tuple.Match.Index)
                    .FirstOrDefault();

                if (match is null)
                {
                    if (!lexer.ignoreUndefined)
                        Current = new(null, input.Substring(currentIndex), currentIndex);
                    currentIndex = input.Length;
                    return !lexer.ignoreUndefined;
                }

                if (match.Index > currentIndex && !lexer.ignoreUndefined)
                {
                    Current = new(null, input.Substring(currentIndex, match.Index - currentIndex), currentIndex);
                    currentIndex = match.Index;
                    return true;
                }

                Current = new(new(symbol), match.Value, currentIndex);
                currentIndex = match.Index + match.Length;
                return true;
            }

            public void Reset()
            {
                currentIndex = 0;
                Current = new(null, string.Empty, -1);
            }
        }
    }
}