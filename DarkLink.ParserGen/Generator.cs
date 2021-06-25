using Microsoft.CodeAnalysis;
using System;

namespace DarkLink.ParserGen
{
    [Generator]
    public class Generator : ISourceGenerator, ISyntaxReceiver
    {
        public void Execute(GeneratorExecutionContext context)
        {
            // Generate source code
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => this);

            // Initialize generator
        }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Collect relevant syntax nodes
        }
    }
}