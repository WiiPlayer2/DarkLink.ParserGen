using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateLexer(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public class Lexer : IEnumerator<Token>
        {{
            private record RuleMatch(bool Success, int Index, string Value)
            {{
                public int Length => Value.Length;
            }}

            private abstract class Rule
            {{
                public abstract RuleMatch Match(string input, int startAt);
            }}

            private class RegexRule : Rule
            {{
                private readonly Regex regex;

                public RegexRule(Regex regex)
                {{
                    this.regex = regex;
                }}

                public override RuleMatch Match(string input, int startAt)
                {{
                    var match = regex.Match(input, startAt);
                    return new(match.Success, match.Index, match.Value);
                }}
            }}

            private class LiteralRule : Rule
            {{
                private readonly string literal;

                public LiteralRule(string literal)
                {{
                    this.literal = literal;
                }}

                public override RuleMatch Match(string input, int startAt)
                {{
                    var index = input.IndexOf(literal, startAt);

                    if (index == -1)
                        return new(false, index, string.Empty);
                    else
                        return new(true, index, literal);
                }}
            }}

            private readonly TextReader reader;

            private static readonly Dictionary<TokenType, Rule> tokenRules = new();

            private string? input;

            private int currentIndex;

            static Lexer()
            {{");
            foreach (var token in config.Lexer.Tokens)
            {
                writer.Write($"tokenRules.Add(TokenType.{token.Name}, ");
                switch (token.Rule)
                {
                    case RegexRule regexRule:
                        writer.Write($"new RegexRule(new({Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(regexRule.Regex, true)}))");
                        break;

                    case LiteralRule literalRule:
                        writer.Write($"new LiteralRule(new(\"{literalRule.Literal}\"))");
                        break;
                }

                writer.WriteLine(");");
            }
            writer.WriteLine($@"
            }}

            public Lexer(string input)
                : this(new StringReader(input)) {{ }}

            public Lexer(Stream stream)
                : this(new StreamReader(stream)) {{ }}

            public Lexer(TextReader reader)
            {{
                this.reader = reader;
                Current = new(TokenType.UNDEFINED, string.Empty, -1);
            }}

            public bool MoveNext()
            {{
                input ??= reader.ReadToEnd();
                if (currentIndex == input.Length)
                    return false;

                var (type, match) = tokenRules
                    .Select(kv => (Type: kv.Key, Match: kv.Value.Match(input, currentIndex)))
                    .Where(tuple => tuple.Match.Success && tuple.Match.Length > 0)
                    .OrderBy(tuple => tuple.Match.Index)
                    .FirstOrDefault();

                if (match is null)
                {{
                    Current = new(TokenType.UNDEFINED, input.Substring(currentIndex), currentIndex);
                    currentIndex = input.Length;
                    return true;
                }}

                if (match.Index > currentIndex)
                {{
                    Current = new(TokenType.UNDEFINED, input.Substring(currentIndex, match.Index - currentIndex), currentIndex);
                    currentIndex = match.Index;
                    return true;
                }}

                Current = new(type, match.Value, currentIndex);
                currentIndex += match.Length;
                return true;
            }}

            public Token Current {{ get; private set; }}

            object IEnumerator.Current => Current;

            public void Reset()
            {{
                currentIndex = 0;
            }}

            public void Dispose()
            {{
                input = null;
                reader.Dispose();
            }}

            public static IEnumerable<Token> Lex(string input)
                => Lex(new StringReader(input));

            public static IEnumerable<Token> Lex(Stream stream)
                => Lex(new StreamReader(stream));

            public static IEnumerable<Token> Lex(TextReader reader)
                => new Enumerable(() => new Lexer(reader));

            private class Enumerable : IEnumerable<Token>
            {{
                private readonly Func<IEnumerator<Token>> enumeratorFunc;

                public Enumerable(Func<IEnumerator<Token>> enumeratorFunc)
                {{
                    this.enumeratorFunc = enumeratorFunc;
                }}

                public IEnumerator<Token> GetEnumerator() => enumeratorFunc();

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }}
        }}");
        }
    }
}