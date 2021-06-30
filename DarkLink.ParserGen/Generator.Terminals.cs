using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateTerminals(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public enum Terminals
        {{");

            foreach (var symbol in config.Grammar.Alphabet)
                writer.WriteLine($"{symbol.Value},");

            writer.WriteLine($@"
        }}
");
        }
    }
}