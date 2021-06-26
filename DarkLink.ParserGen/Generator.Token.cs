using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateToken(TextWriter writer)
        {
            writer.WriteLine($@"
        public record Token(TokenType Type, string Value, int Index);");
        }
    }
}