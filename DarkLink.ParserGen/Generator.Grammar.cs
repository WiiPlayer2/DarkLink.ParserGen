using DarkLink.ParserGen.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateProductions(TextWriter writer, Config config)
        {
            var productions = new List<string>();

            writer.WriteLine($@"
        public static class Productions
        {{");

            foreach (var production in config.Grammar.Productions)
            {
                var name = GetProductionName(production);
                var symbolCodes = production.Right.Symbols.Select(o => o switch
                {
                    NonTerminalSymbol<string> nts => $"G.NT(NonTerminals.{nts.Value})",
                    TerminalSymbol<string> ts => $"G.T(Terminals.{ts.Value})",
                    _ => throw new NotSupportedException(),
                });
                var wordCode = string.Concat(symbolCodes.Select(o => $", {o}"));
                productions.Add(name);
                writer.WriteLine($"public static Production<NonTerminals> {name} {{ get; }} = G.P(NonTerminals.{production.Left.Value}{wordCode});");
            }

            writer.WriteLine($@"

            public static ISet<Production<NonTerminals>> GetAll() => new HashSet<Production<NonTerminals>>(new[] {{");

            foreach (var production in productions)
                writer.WriteLine($"{production},");

            writer.WriteLine($@"
                    }}
                );
        }}

        private static Grammar<NonTerminals, Terminals> Grammar {{ get; }} = G.Create<NonTerminals, Terminals>(Productions.GetAll(), NonTerminals.{config.Grammar.Start.Value});
");
        }

        private string GetProductionName(Production<string> production)
        {
            var symbolNames = production.Right.Symbols.Select(o => o switch
            {
                NonTerminalSymbol<string> nts => nts.Value,
                TerminalSymbol<string> ts => ts.Value,
                _ => throw new NotImplementedException(),
            });
            return $"{production.Left.Value}___TO___{string.Join($"__", symbolNames)}";
        }
    }
}