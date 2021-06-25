using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        private static readonly Encoding sourceEncoding = new UTF8Encoding(false);

        public void Execute(GeneratorExecutionContext context)
        {
            var config = new Config(
                new("Testing", "Test", string.Empty),
                new(new TokenInfo[]
                {
                    new("Whitespace", "\\w"),
                    new("Hello", "hello"),
                    new("World", "world"),
                }));

            Generate(context, config);
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
        {
            var baseName = $"{config.Type.Namespace}.{config.Type.Name}";
            GenerateTokenType(context, baseName, config);
            GenerateToken(context, baseName, config);
            GenerateLexer(context, baseName, config);
        }

        private void GenerateLexer(GeneratorExecutionContext context, string baseName, Config config)
            => AddSource(context, $"{baseName}Lexer.g.cs", writer =>
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
    {config.Type.Modifier} class {config.Type.Name}Lexer : IEnumerator<{config.Type.Name}Token>
    {{
        private readonly TextReader reader;

        private static readonly Dictionary<{config.Type.Name}TokenType, Regex> tokenRules = new();

        private string? input;

        private int currentIndex;

        static {config.Type.Name}Lexer()
        {{");
                foreach (var token in config.Lexer.Tokens)
                {
                    writer.WriteLine($"tokenRules.Add({config.Type.Name}TokenType.{token.Name}, new Regex({Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(token.Regex, true)}));");
                }
                writer.WriteLine($@"
        }}

        public {config.Type.Name}Lexer(TextReader reader)
        {{
            this.reader = reader;
            Current = new({config.Type.Name}TokenType.UNKNOWN, string.Empty, -1);
        }}

        public bool MoveNext()
        {{
            input ??= reader.ReadToEnd();
            if (currentIndex == input.Length)
                return false;

            var (type, match) = tokenRules
                .Select(kv => (Type: kv.Key, Match: kv.Value.Match(input, currentIndex)))
                .FirstOrDefault(tuple => tuple.Match is not null);

            if (match is null)
            {{
                Current = new({config.Type.Name}TokenType.UNKNOWN, input.Substring(currentIndex), currentIndex);
                currentIndex = input.Length;
                return true;
            }}

            if (match.Index > currentIndex)
            {{
                Current = new({config.Type.Name}TokenType.UNKNOWN, input.Substring(currentIndex, match.Index - currentIndex), currentIndex);
                currentIndex = match.Index;
                return true;
            }}

            Current = new(type, match.Value, currentIndex);
            currentIndex += match.Length;
            return true;
        }}

        public {config.Type.Name}Token Current {{ get; private set; }}

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
    }}
}}
");
            });

        private void GenerateToken(GeneratorExecutionContext context, string baseName, Config config)
            => AddSource(context, $"{baseName}Token.g.cs", writer =>
            {
                writer.WriteLine("#nullable enable");
                writer.WriteLine($"namespace {config.Type.Namespace} {{");
                writer.WriteLine($"{config.Type.Modifier} record {config.Type.Name}Token({config.Type.Name}TokenType Type, string Value, int Index);");
                writer.WriteLine("}");
            });

        private void GenerateTokenType(GeneratorExecutionContext context, string baseName, Config config)
            => AddSource(context, $"{baseName}TokenType.g.cs", writer =>
            {
                writer.WriteLine($"namespace {config.Type.Namespace} {{");
                writer.WriteLine($"{config.Type.Modifier} enum {config.Type.Name}TokenType {{");
                writer.WriteLine("UNKNOWN,");
                foreach (var tokenInfo in config.Lexer.Tokens)
                    writer.WriteLine($"{tokenInfo.Name},");
                writer.WriteLine("}}");
            });
    }
}