using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateNonTerminals(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public enum NonTerminals
        {{");

            foreach (var symbol in config.Parser.Rules.Select(o => o.Name).Distinct())
                writer.WriteLine($@"
            {symbol},");

            writer.WriteLine($@"
        }}
");
        }
    }
}