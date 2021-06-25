using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DarkLink.ParserGen
{
    internal static class ConfigParser
    {
        private static Regex modifierRegex = new(@"#modifier (?<modifier>public|internal)");

        private static Regex namespaceRegex = new(@"#namespace (?<namespace>\w+)");

        private static Regex tokenRegex = new(@"(?<type>\w+)\s*=\s*/(?<regex>[^/]+)/");

        public static Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText)
        {
            var sourceText = additionalText.GetText(context.CancellationToken);
            if (sourceText is null)
            {
                return null;
            }

            string? @namespace = null;
            string? modifier = null;
            var tokens = new List<(string Type, string Regex)>();

            foreach (var line in sourceText.Lines)
            {
                var lineText = line.ToString();

                Match? match;
                if ((match = namespaceRegex.Match(lineText)).Success)
                {
                    if (@namespace is not null)
                        return null;

                    @namespace = match.Groups["namespace"].Value;
                    continue;
                }

                if ((match = modifierRegex.Match(lineText)).Success)
                {
                    if (modifier is not null)
                        return null;

                    modifier = match.Groups["modifier"].Value;
                }

                if ((match = tokenRegex.Match(lineText)).Success)
                {
                    tokens.Add((match.Groups["type"].Value, match.Groups["regex"].Value));
                }
            }

            if (@namespace is null)
                return null;

            var name = Path.GetFileNameWithoutExtension(additionalText.Path);
            return new(
                new(@namespace, name, modifier ?? string.Empty),
                new(tokens.Select(tuple => new TokenInfo(tuple.Type, tuple.Regex)).ToList()));
        }
    }
}