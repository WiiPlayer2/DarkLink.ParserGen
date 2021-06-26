using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DemoApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var content = @"the bat eats a cat";
            var tokens = Testing.Lexer.Lex(content).ToList();
            var parser = new Testing.Parser();
            var rootNode = parser.Parse(tokens);

            Console.WriteLine($"{rootNode}");
        }
    }
}