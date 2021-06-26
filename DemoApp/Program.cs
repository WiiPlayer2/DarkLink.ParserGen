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
            var content = @"id+id";
            var parser = new Testing.Parser();
            var rootNode = parser.Parse(content);

            Console.WriteLine($"{rootNode}");
        }
    }
}