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

            foreach (var rule in config.Parser.Rules)
            {
                var word = rule.Targets.Where(o => !o.IsToken || o.Name != "EMPTY").ToList();
                var name = $"{rule.Name}___TO___{string.Join($"__", word.Select(o => o.Name))}";
                var wordCode = string.Concat(word.Select(t => $", {(t.IsToken ? "G.T(Terminals" : "G.NT(NonTerminals")}.{t.Name})"));
                productions.Add(name);
                writer.WriteLine($"public static Production<NonTerminals> {name} {{ get; }} = G.P(NonTerminals.{rule.Name}{wordCode});");
            }

            writer.WriteLine($@"

            public static ISet<Production<NonTerminals>> GetAll() => new HashSet<Production<NonTerminals>>(new[] {{");

            foreach (var production in productions)
                writer.WriteLine($"{production},");

            writer.WriteLine($@"
                    }}
                );
        }}

        private static Grammar<NonTerminals, Terminals> Grammar {{ get; }} = G.Create<NonTerminals, Terminals>(Productions.GetAll(), NonTerminals.{config.Parser.Start});
");
        }
    }
}