using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkLink.ParserGen.Parsing;

namespace DemoApp
{
    internal static class Program
    {
        private record S();

        private record Pair(string left, S middle, string right) : S;

        private static S empty(object[] args) => new S();

        private static void Main(string[] args)
        {
            var content = @"aabbaa";
            var parser = new Testing.Parser<S>(new()
            {
                { Testing.Productions.S___TO___A__S__A, xSx },
                { Testing.Productions.S___TO___B__S__B, xSx },
                { Testing.Productions.S___TO___, empty },
            });
            var rootNode = parser.Parse(content).ToList();

            Console.WriteLine($"{rootNode}");
        }

        private static S xSx(object[] args) => new Pair(((Token<Testing.Terminals>)args[0]).Value, (S)args[1], ((Token<Testing.Terminals>)args[2]).Value);
    }
}