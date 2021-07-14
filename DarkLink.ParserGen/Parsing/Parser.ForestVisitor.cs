using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DarkLink.ParserGen.Parsing
{
    partial class Parser<T, TNT, TT>
    {
        private abstract class ForestVisitor
        {
            private readonly bool onlyVisitOnce;

            public ForestVisitor(bool onlyVisitOnce = false)
            {
                this.onlyVisitOnce = onlyVisitOnce;
            }

            public void Visit(Node node, CancellationToken cancellationToken)
            {
                Visit(node, new(), new(), new(), cancellationToken);
            }

            protected virtual void OnCycle(Node node, IReadOnlyList<Node> path)
            {
            }

            protected abstract IEnumerable<Node> VisitIntermediateNodeIn(IntermediateNode node);

            protected virtual void VisitIntermediateNodeOut(IntermediateNode node)
            {
            }

            protected abstract IEnumerable<Node> VisitNonTerminalNodeIn(NonTerminalNode node);

            protected virtual void VisitNonTerminalNodeOut(NonTerminalNode node)
            {
            }

            protected abstract IEnumerable<Node> VisitPackNodeIn(PackNode node);

            protected virtual void VisitPackNodeOut(PackNode node)
            {
            }

            protected virtual void VisitTerminalNode(TerminalNode node)
            {
            }

            private void Visit(Node node, HashSet<Node> visited, HashSet<Node> visiting, Stack<Node> path, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (node is TerminalNode terminalNode)
                {
                    VisitTerminalNode(terminalNode);
                    return;
                }

                if (onlyVisitOnce && visited.Contains(node))
                    return;

                if (visiting.Contains(node))
                {
                    OnCycle(node, path.ToList());
                    return;
                }

                visiting.Add(node);
                path.Push(node);

                var nextNodes = node switch
                {
                    IntermediateNode intermediateNode => VisitIntermediateNodeIn(intermediateNode),
                    NonTerminalNode nonTerminalNode => VisitNonTerminalNodeIn(nonTerminalNode),
                    PackNode packNode => VisitPackNodeIn(packNode),
                    _ => Enumerable.Empty<Node>(),
                };

                foreach (var nextNode in nextNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Visit(nextNode, visited, visiting, path, cancellationToken);
                }

                switch (node)
                {
                    case IntermediateNode intermediateNode:
                        VisitIntermediateNodeOut(intermediateNode);
                        break;

                    case NonTerminalNode nonTerminalNode:
                        VisitNonTerminalNodeOut(nonTerminalNode);
                        break;

                    case PackNode packNode:
                        VisitPackNodeOut(packNode);
                        break;
                }

                path.Pop();
                visiting.Remove(node);
                visited.Add(node);
            }
        }
    }
}