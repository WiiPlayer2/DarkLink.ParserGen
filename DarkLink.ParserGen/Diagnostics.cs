using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
#pragma warning disable RS2008 // Enable analyzer release tracking

    internal static class Diagnostics
    {
        public static readonly DiagnosticDescriptor ConfigParserAlreadySet = new DiagnosticDescriptor(
            "PG01",
            "Config in parser is already set",
            "Config \"{0}\" is already set",
            "Parsing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor ConfigParserInvalidLine = new DiagnosticDescriptor(
            "PG03",
            "Line in parser is not recognized",
            "\"{0}\" is not a valid line",
            "Parsing",
            DiagnosticSeverity.Warning,
            true);

        public static readonly DiagnosticDescriptor ConfigParserNotSet = new DiagnosticDescriptor(
            "PG02",
            "Config in parser is not set",
            "Config \"{0}\" is not set",
            "Parsing",
            DiagnosticSeverity.Error,
            true);
    }

#pragma warning restore RS2008 // Enable analyzer release tracking
}