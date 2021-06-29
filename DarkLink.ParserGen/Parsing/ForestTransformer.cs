using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DarkLink.ParserGen.Parsing
{
    internal static partial class Earley
    {
        private abstract class ForestTransformer<T> : ForestVisitor
        {
            private record ResultNode() : Node;

            private readonly Dictionary<Node, List<T>> data = new();

            private readonly Stack<Node> nodeStack = new();

            public Option<T> Transform(Node root)
            {
                var resultNode = new ResultNode();
                nodeStack.Push(resultNode);
                data[resultNode] = new();
                Visit(root);
                Debug.Assert(data[resultNode].Count <= 1);
                if (data[resultNode].Count > 0)
                    return data[resultNode][0];
                return Option.None;
            }

            protected abstract Option<T> TransformIntermediateNode(IntermediateNode node, List<T> data);

            protected abstract Option<T> TransformNonTerminalNode(NonTerminalNode node, List<T> data);

            protected abstract Option<T> TransformPackNode(PackNode node, List<T> data);

            protected abstract Option<T> TransformTerminalNode(TerminalNode node);

            protected override IEnumerable<Node> VisitIntermediateNodeIn(IntermediateNode node)
            {
                nodeStack.Push(node);
                data[node] = new();
                return node.Children;
            }

            protected override void VisitIntermediateNodeOut(IntermediateNode node)
            {
                nodeStack.Pop();
                TransformIntermediateNode(node, data[node]).Match(data[nodeStack.Peek()].Add);
                data.Remove(node);
            }

            protected override IEnumerable<Node> VisitNonTerminalNodeIn(NonTerminalNode node)
            {
                nodeStack.Push(node);
                data[node] = new();
                return node.Children;
            }

            protected override void VisitNonTerminalNodeOut(NonTerminalNode node)
            {
                nodeStack.Pop();
                TransformNonTerminalNode(node, data[node]).Match(data[nodeStack.Peek()].Add);
                data.Remove(node);
            }

            protected override IEnumerable<Node> VisitPackNodeIn(PackNode node)
            {
                nodeStack.Push(node);
                data[node] = new();
                return node.Children;
            }

            protected override void VisitPackNodeOut(PackNode node)
            {
                nodeStack.Pop();
                TransformPackNode(node, data[node]).Match(data[nodeStack.Peek()].Add);
                data.Remove(node);
            }

            protected override void VisitTerminalNode(TerminalNode node)
            {
                TransformTerminalNode(node).Match(data[nodeStack.Peek()].Add);
            }
        }
    }
}