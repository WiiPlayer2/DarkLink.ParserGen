using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
#pragma warning disable RS2008 // Enable analyzer release tracking

    internal static class Diagnostics
    {
        public static readonly DiagnosticDescriptor ConfigParserAlreadySet = new(
            "PG01",
            "Config in parser is already set",
            "Config \"{0}\" is already set",
            "Parsing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor ConfigParserInvalidLine = new(
            "PG03",
            "Line in parser is not recognized",
            "\"{0}\" is not a valid line",
            "Parsing",
            DiagnosticSeverity.Warning,
            true);

        public static readonly DiagnosticDescriptor ConfigParserNotSet = new(
            "PG02",
            "Config in parser is not set",
            "Config \"{0}\" is not set",
            "Parsing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor FailedToConstructParsingTable = new(
            "PG04",
            "Failed to construct parsing table",
            "Failed to construct parsing table with k = {0}",
            "Preparing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor FailedToOpenFile = new(
            "PG05",
            "Failed to read file",
            "Failed to read file {0}",
            "Parsing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor FailedToParse = new(
            "PG06",
            "Failed to parse file",
            "Failed to parse file {0}",
            "Parsing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor ParserFileInvalid = new(
            "PG07",
            "Parser file is invalid",
            "Parser file {0} is invalid",
            "Parsing",
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor SyntaxError = new(
            "PG08",
            "Syntax error",
            "Expected {0}",
            "Parsing",
            DiagnosticSeverity.Error,
            true);
    }

#pragma warning restore RS2008 // Enable analyzer release tracking
}