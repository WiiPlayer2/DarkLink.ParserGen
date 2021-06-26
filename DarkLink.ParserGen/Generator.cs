using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        private static readonly Encoding sourceEncoding = new UTF8Encoding(false);

        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var additionalText in context.AdditionalFiles
                .Where(o => string.Equals(Path.GetExtension(o.Path), ".parser", StringComparison.InvariantCultureIgnoreCase)))
            {
                var config = ConfigParser.Parse(context, additionalText);
                if (config is null)
                    continue;

                Generate(context, config);
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        private void AddSource(GeneratorExecutionContext context, string hintName, Action<StringWriter> write)
        {
            using var writer = new StringWriter();
            write(writer);
            var sourceText = SourceText.From(writer.ToString(), sourceEncoding);
            context.AddSource(hintName, sourceText);
        }

        private void Generate(GeneratorExecutionContext context, Config config)
            => AddSource(context, $"{config.Type.Namespace}.{config.Type.Name}.g.cs", writer =>
            {
                writer.WriteLine($@"
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace {config.Type.Namespace}
{{
    {config.Type.Modifier} static class {config.Type.Name}
    {{");

                GenerateTokenType(writer, config);
                GenerateToken(writer);
                GenerateLexer(writer, config);

                writer.WriteLine($@"
    }}
}}
");
            });

        private void GenerateLexer(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public class Lexer : IEnumerator<Token>
        {{
            private readonly TextReader reader;

            private static readonly Dictionary<TokenType, Regex> tokenRules = new();

            private string? input;

            private int currentIndex;

            static Lexer()
            {{");
            foreach (var token in config.Lexer.Tokens)
            {
                writer.WriteLine($"tokenRules.Add(TokenType.{token.Name}, new Regex({Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(token.Regex, true)}));");
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

        private void GenerateToken(TextWriter writer)
        {
            writer.WriteLine($@"
        public record Token(TokenType Type, string Value, int Index);");
        }

        private void GenerateTokenType(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public enum TokenType
        {{");

            foreach (var tokenInfo in config.Lexer.Tokens)
                writer.WriteLine($@"
            {tokenInfo.Name},");

            writer.WriteLine($@"
            UNDEFINED,
        }}
");
        }
    }
}