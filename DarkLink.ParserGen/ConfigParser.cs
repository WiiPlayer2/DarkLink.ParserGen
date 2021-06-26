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

        private static Regex ruleRegex = new(@"(?<rule>\w+)\s*->(\s*(?<target>\#?\w+))+\s*;");

        private static Regex startRegex = new(@"#start (?<start>\w+)");

        private static Regex tokenRegex = new(@"(?<type>\w+)\s*=\s*(/(?<regex>[^/]+)/|""(?<literal>[^""]+)""|'(?<literal>[^']+)')");

        public static Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText)
        {
            var sourceText = additionalText.GetText(context.CancellationToken);
            if (sourceText is null)
            {
                return null;
            }

            string? @namespace = null;
            string? modifier = null;
            string? start = null;
            var tokens = new List<(string Type, TokenRule Rule)>();
            var rules = new List<(string Name, ParserRuleTarget[] Targets)>();

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

                if ((match = startRegex.Match(lineText)).Success)
                {
                    if (start is not null)
                        return null;

                    start = match.Groups["start"].Value;
                }

                if ((match = tokenRegex.Match(lineText)).Success)
                {
                    var type = match.Groups["type"].Value;
                    TokenRule? rule;
                    if (match.Groups["regex"].Success)
                        rule = new RegexRule(match.Groups["regex"].Value);
                    else if (match.Groups["literal"].Success)
                        rule = new LiteralRule(match.Groups["literal"].Value);
                    else
                        return null;

                    tokens.Add((type, rule));
                }

                if ((match = ruleRegex.Match(lineText)).Success)
                {
                    var rule = match.Groups["rule"].Value;
                    var targets = match.Groups["target"].Captures
                        .Cast<Capture>()
                        .Select(o =>
                        {
                            if (o.Value.StartsWith("#"))
                                return new ParserRuleTarget(o.Value.Substring(1), true);
                            return new ParserRuleTarget(o.Value, false);
                        })
                        .ToArray();
                    rules.Add((rule, targets));
                }
            }

            if (@namespace is null)
                return null;

            if (start is null)
                return null;

            var name = Path.GetFileNameWithoutExtension(additionalText.Path);
            return new(
                new(@namespace, name, modifier ?? string.Empty),
                new(tokens.Select(tuple => new TokenInfo(tuple.Type, tuple.Rule)).ToList()),
                new(
                    start,
                    rules
                        .ToLookup(o => o.Name)
                        .Select(grouping => new ParserRule(grouping.Key, grouping
                            .Select(tuple => (IReadOnlyList<ParserRuleTarget>)tuple.Targets.ToList())
                            .ToList()))
                        .ToList()));
        }
    }
}