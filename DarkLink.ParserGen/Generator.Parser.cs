using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateParser(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public class Parser<T>
        {{
            private readonly DarkLink.ParserGen.Parsing.Parser<T, NonTerminals, Terminals> parser;

            private readonly Lexer<Terminals> lexer;

            public Parser(IReadOnlyDictionary<Production<NonTerminals>, Func<object[], T>> callbacks)
            {{
                var lexerRules = new Dictionary<Terminals, Lexer<Terminals>.Rule>()
                {{");

            foreach (var token in config.Lexer.Tokens)
            {
                var ruleCode = token.Rule switch
                {
                    RegexRule regexRule => $"new Lexer<Terminals>.RegexRule(new Regex({SymbolDisplay.FormatLiteral(regexRule.Regex, true)}))",
                    LiteralRule literalRule => $"new Lexer<Terminals>.LiteralRule({SymbolDisplay.FormatLiteral(literalRule.Literal, true)})",
                    _ => throw new NotSupportedException(),
                };
                writer.WriteLine($"{{ Terminals.{token.Name}, {ruleCode} }},");
            }

            writer.WriteLine($@"
                }};
                lexer = new(lexerRules);
                parser = new(Grammar, callbacks);
            }}

            public IEnumerable<T> Parse(string input)
                => Parse(new StringReader(input));

            public IEnumerable<T> Parse(Stream stream)
                => Parse(new StreamReader(stream));

            public IEnumerable<T> Parse(TextReader reader)
            {{
                var tokens = lexer.Lex(reader).ToList();
                return parser.Parse(tokens);
            }}
        }}");
        }
    }
}