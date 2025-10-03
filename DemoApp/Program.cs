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
        private static void Main(string[] args)
        {
            var content = @"a          b";
            var root = Testing.Cst.Parse(content);
            Console.WriteLine($"{root}");
        }
    }
}