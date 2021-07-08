using DarkLink.ParserGen.Formats;
using DarkLink.ParserGen.Formats.Bnf;
using DarkLink.ParserGen.Formats.Ebnf;
using DarkLink.ParserGen.Formats.Simple;
using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkLink.ParserGen
{
    [Generator]
    public partial class Generator : ISourceGenerator
    {
        private static readonly IParser defaultParser = new SimpleParser();

        private static readonly Dictionary<string, IParser> parsers = new Dictionary<string, IParser>(StringComparer.InvariantCultureIgnoreCase)
        {
            { ".bnf", new BnfParser() },
            { ".ebnf", new EbnfParser() },
        };

        private static readonly Encoding sourceEncoding = new UTF8Encoding(false);

        public void Execute(GeneratorExecutionContext context)
        {
            var parserFiles = context.AdditionalFiles
                .Where(o => string.Equals(Path.GetExtension(o.Path), ".parser", StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            if (parserFiles.Count == 0)
                return;

            foreach (var additionalText in parserFiles)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var secondExtension = Path.GetExtension(Path.GetFileNameWithoutExtension(additionalText.Path));
                var filename = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(additionalText.Path));

                if (!parsers.TryGetValue(secondExtension, out var parser))
                    parser = defaultParser;

                var config = parser.Parse(context, additionalText, filename);
                if (config is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ParserFileInvalid, Location.Create(additionalText.Path, default, default), additionalText.Path));
                    continue;
                }

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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using DarkLink.ParserGen.Parsing;

namespace {config.Type.Namespace}
{{
    internal static partial class {config.Type.Name}
    {{");

                GenerateTerminals(writer, config);
                GenerateNonTerminals(writer, config);
                GenerateProductions(writer, config);
                GenerateParser(writer, config);
                GenerateCst(writer, config);

                writer.WriteLine($@"
    }}
}}
");
            });
    }
}