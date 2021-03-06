using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DarkLink.ParserGen.Formats.Simple
{
    internal class SimpleParser : IParser
    {
        private static Regex kRegex = new(@"#k (?<k>\d+)");

        private static Regex modifierRegex = new(@"#modifier (?<modifier>public|internal)");

        private static Regex namespaceRegex = new(@"#namespace (?<namespace>\w+)");

        private static Regex ruleRegex = new(@"(?<rule>\w+)\s*->(\s*(?<target>\#?\w+))+\s*;");

        private static Regex startRegex = new(@"#start (?<start>\w+)");

        private static Regex tokenRegex = new(@"(?<type>\w+)\s*=\s*(/(?<regex>[^/]+)/|""(?<literal>[^""]+)""|'(?<literal>[^']+)')");

        public Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText, string className)
        {
            var sourceText = additionalText.GetText(context.CancellationToken);
            if (sourceText is null)
            {
                return null;
            }

            string? @namespace = null;
            string? modifier = null;
            string? start = null;
            int? k = null;
            var tokens = new List<(string Type, TokenRule Rule)>();
            var rules = new List<(string Name, Symbol[] Targets)>();

            foreach (var line in sourceText.Lines)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var lineText = line.ToString();
                var location = Location.Create(additionalText.Path, line.Span, new(new(line.LineNumber, 0), new(line.LineNumber, line.Span.Length)));

                Match? match;
                if ((match = namespaceRegex.Match(lineText)).Success)
                {
                    if (@namespace is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserAlreadySet, location, "namespace"));
                        return null;
                    }

                    @namespace = match.Groups["namespace"].Value;
                    continue;
                }
                else if ((match = modifierRegex.Match(lineText)).Success)
                {
                    if (modifier is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserAlreadySet, location, "modifier"));
                        return null;
                    }

                    modifier = match.Groups["modifier"].Value;
                }
                else if ((match = startRegex.Match(lineText)).Success)
                {
                    if (start is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserAlreadySet, location, "start"));
                        return null;
                    }

                    start = match.Groups["start"].Value;
                }
                else if ((match = kRegex.Match(lineText)).Success)
                {
                    if (k is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserAlreadySet, location, "k"));
                        return null;
                    }

                    k = int.Parse(match.Groups["k"].Value);
                }
                else if ((match = tokenRegex.Match(lineText)).Success)
                {
                    var type = match.Groups["type"].Value;
                    TokenRule? rule;
                    if (match.Groups["regex"].Success)
                        rule = new RegexRule(match.Groups["regex"].Value);
                    else if (match.Groups["literal"].Success)
                        rule = new LiteralRule(match.Groups["literal"].Value);
                    else
                        throw new NotSupportedException();

                    tokens.Add((type, rule));
                }
                else if ((match = ruleRegex.Match(lineText)).Success)
                {
                    var rule = match.Groups["rule"].Value;
                    var targets = match.Groups["target"].Captures
                        .Cast<Capture>()
                        .Select<Capture, Symbol>(o =>
                        {
                            if (o.Value.StartsWith("#"))
                                return G.T(o.Value.Substring(1));
                            return G.NT(o.Value);
                        })
                        .ToArray();
                    rules.Add((rule, targets));
                }

                if (!match.Success && !string.IsNullOrWhiteSpace(lineText))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserInvalidLine, location, lineText));
                }
            }

            if (@namespace is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserNotSet, Location.Create(additionalText.Path, default, default), "namespace"));
                return null;
            }

            if (start is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConfigParserNotSet, Location.Create(additionalText.Path, default, default), "start"));
                return null;
            }

            return new(
                new(@namespace, className, modifier ?? string.Empty),
                new(tokens.Select(tuple => new TokenInfo(tuple.Type, tuple.Rule)).ToList()),
                new(
                    rules.Select(o => G.NT(o.Name)).ToSet(),
                    tokens.Select(o => G.T(o.Type)).ToSet(),
                    rules.Select(o => G.P(o.Name, o.Targets)).ToSet(),
                    G.NT(start)));
        }
    }
}