using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateTokenType(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public enum TokenType
        {{");

            foreach (var tokenInfo in config.Lexer.Tokens)
                writer.WriteLine($@"
            {tokenInfo.Name},");

            writer.WriteLine($@"
            END,
            EMPTY,
            UNDEFINED,
        }}
");
        }
    }
}