using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkLink.ParserGen.Parsing;
using TestingNS;

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
            var parser = new TestingC.Parser<S>(new()
            {
                { TestingC.Productions.START___TO___S, args => (S)args[0] },
                { TestingC.Productions.S___TO____a__S___a, },
                { TestingC.Productions.S___TO___S_62__S__S_62, },
                { TestingC.Productions.S___TO___, empty },
            });
            var rootNode = parser.Parse(content).ToList();

            Console.WriteLine($"{rootNode}");
        }

        private class AST : AstBuilder<S, TestingC.NonTerminals>
        {
            public AST()
            {
                R(TestingC.Productions.START___TO___S, PASS);
                R(TestingC.Productions.S___TO___, DUMP);
                R(TestingC.Productions.S___TO____a__S___a, args => new Pair(args[0]))
            }
        }
    }
}