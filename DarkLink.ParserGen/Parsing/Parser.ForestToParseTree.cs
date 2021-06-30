using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DarkLink.ParserGen.Parsing
{
    partial class Parser<T, TNT, TT>
    {
        private class ForestToParseTree
        {
            private readonly IReadOnlyDictionary<Production<TNT>, Func<object[], T>> callbacks;

            public ForestToParseTree(IReadOnlyDictionary<Production<TNT>, Func<object[], T>> callbacks)
            {
                this.callbacks = callbacks;
            }

            public Option<T> Transform(Node root)
                 => new ForestToParseTreeIntl(callbacks).Transform(root);

            public abstract record ParseNode();

            public record ParseLeaf(object? Value) : ParseNode;

            public record ParseAmbig(IReadOnlyList<ParseNode> Value) : ParseNode;

            public record ParseBranch(IReadOnlyList<ParseNode> Children) : ParseNode;

            private class ForestToParseTreeIntl : ForestTransformer<ParseNode>
            {
                private readonly IReadOnlyDictionary<Production<TNT>, Func<object[], T>> callbacks;

                private Node? cycleNode = null;

                private bool onCycleRetreat = false;

                private HashSet<Node> successfulVisits = new();

                public ForestToParseTreeIntl(IReadOnlyDictionary<Production<TNT>, Func<object[], T>> callbacks)
                {
                    this.callbacks = callbacks;
                }

                public new Option<T> Transform(Node root)
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
                    => base.Transform(root).Map<T>(node => (T)((ParseLeaf)node).Value);

#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                protected override void OnCycle(Node node, IReadOnlyList<Node> path)
                {
                    cycleNode = node;
                    onCycleRetreat = true;
                }

                protected override Option<ParseNode> TransformIntermediateNode(IntermediateNode node, List<ParseNode> data)
                {
                    if (!successfulVisits.Contains(node))
                        return Option.None;

                    CheckCycle(node);
                    successfulVisits.Remove(node);

                    if (data.Count > 1)
                    {
                        throw new NotImplementedException();
                    }
                    return data[0];
                }

                protected override Option<ParseNode> TransformNonTerminalNode(NonTerminalNode node, List<ParseNode> data)
                {
                    if (!successfulVisits.Contains(node))
                        return Option.None;

                    CheckCycle(node);
                    successfulVisits.Remove(node);

                    data = CollapseAmbiguity(data);
                    return CallAmbiguityFunc(node, data);
                }

                protected override Option<ParseNode> TransformPackNode(PackNode node, List<ParseNode> data)
                {
                    CheckCycle(node);
                    if (successfulVisits.Contains(node.Parent))
                        return Option.None;

                    var children = new List<ParseNode>();
                    Debug.Assert(data.Count <= 2);
                    var packData = new PackData(node, data);

                    packData.Left.Match(left =>
                    {
                        if (left is ParseBranch parseBranch)
                            children.AddRange(parseBranch.Children);
                        else
                            children.Add(left);
                    });
                    packData.Right.Match(children.Add);

                    if (node.Parent is IntermediateNode)
                        return new ParseBranch(children);
                    return CallRuleFunc(node, children);
                }

                protected override Option<ParseNode> TransformTerminalNode(TerminalNode node)
                    => new ParseLeaf(node);

                protected override IEnumerable<Node> VisitIntermediateNodeIn(IntermediateNode node)
                {
                    var children = base.VisitIntermediateNodeIn(node);
                    if (onCycleRetreat)
                        return Enumerable.Empty<Node>();
                    return children;
                }

                protected override IEnumerable<Node> VisitNonTerminalNodeIn(NonTerminalNode node)
                {
                    var children = base.VisitNonTerminalNodeIn(node);
                    if (onCycleRetreat)
                        return Enumerable.Empty<Node>();
                    return children;
                }

                protected override IEnumerable<Node> VisitPackNodeIn(PackNode node)
                {
                    onCycleRetreat = false;
                    var children = base.VisitPackNodeIn(node);
                    if (!successfulVisits.Contains(node.Parent))
                        return children;
                    return Enumerable.Empty<Node>();
                }

                protected override void VisitPackNodeOut(PackNode node)
                {
                    base.VisitPackNodeOut(node);
                    if (!onCycleRetreat)
                        successfulVisits.Add(node.Parent);
                }

                private Option<ParseNode> CallAmbiguityFunc(Node node, List<ParseNode> data)
                {
                    if (data.Count > 1)
                        return new ParseAmbig(data);
                    else if (data.Count == 1)
                        return data[0];
                    else
                        return Option.None;
                }

                private Option<ParseNode> CallRuleFunc(PackNode node, List<ParseNode> children)
                {
                    var args = (object[])children
                        .Cast<ParseLeaf>()
                        .Select(o => o.Value is TerminalNode terminalNode ? terminalNode.Label.Token : o.Value)
                        .ToArray();
                    var value = callbacks[node.Production](args);
                    return new ParseLeaf(value);
                }

                private bool CheckCycle(Node node)
                {
                    if (onCycleRetreat)
                    {
                        if (node == cycleNode || successfulVisits.Contains(node))
                        {
                            cycleNode = node;
                            onCycleRetreat = false;
                            return true;
                        }
                        return false;
                    }
                    return true;
                }

                private List<ParseNode> CollapseAmbiguity(List<ParseNode> children)
                {
                    var newChildren = new List<ParseNode>();
                    foreach (var child in children)
                    {
                        if (child is ParseAmbig parseAmbig)
                            newChildren.AddRange(parseAmbig.Value);
                        else
                            newChildren.Add(child);
                    }
                    return newChildren;
                }

                private class PackData
                {
                    public PackData(PackNode node, List<ParseNode> data)
                    {
                        if (data.Count > 0)
                        {
                            if (node.Left is not null)
                            {
                                Left = data[0];
                                if (data.Count > 1)
                                    Right = data[1];
                            }
                            else
                            {
                                Right = data[0];
                            }
                        }
                    }

                    public Option<ParseNode> Left { get; }

                    public Option<ParseNode> Right { get; }
                }
            }
        }
    }
}