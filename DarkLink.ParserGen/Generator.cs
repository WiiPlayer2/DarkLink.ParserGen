using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    [Generator]
    public partial class Generator : ISourceGenerator
    {
        private static readonly Encoding sourceEncoding = new UTF8Encoding(false);

        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var additionalText in context.AdditionalFiles
                .Where(o => string.Equals(Path.GetExtension(o.Path), ".parser", StringComparison.InvariantCultureIgnoreCase)))
            {
                context.CancellationToken.ThrowIfCancellationRequested();

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
using System.Diagnostics;
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
                GenerateSymbolType(writer, config);
                GenerateParser(writer, config, context);

                writer.WriteLine($@"
    }}
}}
");
            });
    }
}