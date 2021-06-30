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
    internal class EmbeddedGenerator : ISourceGenerator
    {
        private static readonly Encoding sourceEncoding = new UTF8Encoding(false);

        public void Execute(GeneratorExecutionContext context)
        {
            AddParsingCode(context);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        private void AddParsingCode(GeneratorExecutionContext context)
        {
            var assembly = typeof(Generator).Assembly;
            var files = assembly.GetManifestResourceNames()
                .Where(o => Regex.IsMatch(o, @"DarkLink\.ParserGen\.Parsing\..*\.cs"));
            foreach (var file in files)
            {
                using var stream = assembly.GetManifestResourceStream(file);
                using var reader = new StreamReader(stream, sourceEncoding);
                var content = "#nullable enable\n" + reader.ReadToEnd();
                var sourceText = SourceText.From(content, sourceEncoding);
                var newFilename = Path.GetFileNameWithoutExtension(file) + ".g" + Path.GetExtension(file);
                context.AddSource(newFilename, sourceText);
            }
        }
    }
}