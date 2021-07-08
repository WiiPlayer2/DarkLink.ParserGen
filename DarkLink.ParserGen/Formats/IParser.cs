using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Formats
{
    internal interface IParser
    {
        Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText, string className);
    }
}