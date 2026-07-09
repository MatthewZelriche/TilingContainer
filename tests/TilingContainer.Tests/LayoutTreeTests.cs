using System.Collections.Generic;
using Godot;
using Xunit;
using static TilingContainer.Tests.LayoutTestHelpers;

namespace TilingContainer.Tests;

public class LayoutTreeTests
{
    [Fact]
    public void EmptyTree_HasZeroMinimumSize()
    {
        LayoutTree tree = new();
        AssertVec(Vector2.Zero, tree.GetMinimumSize(borderThickness: 5));
    }

    [Fact]
    public void EmptyTree_ApplyLayout_DoesNotInvokeCallback()
    {
        LayoutTree tree = new();
        int calls = 0;

        tree.ApplyLayout((_, _) => calls++, borderThickness: 2, new Rect2(0, 0, 100, 100));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void SetRoot_ThenApplyLayout_LaysOutThroughTheRoot()
    {
        LayoutTree tree = new();
        FakeLeaf leaf = new(10, 10);
        tree.SetRoot(leaf);

        List<(LeafNodeBase Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 100, 80)
        );

        Assert.Single(results);
        AssertRect(new Rect2(0, 0, 100, 80), results.BoundsOf(leaf));
    }

    [Fact]
    public void InsertSplit_RootLeaf_ReplacesRootWithSplit()
    {
        LayoutTree tree = new();
        FakeLeaf original = new(0, 0);
        FakeLeaf inserted = new(0, 0);
        tree.SetRoot(original);

        bool insertedSplit = tree.InsertSplit(
            original,
            inserted,
            SplitAxis.Horizontal,
            InsertPlacement.Before
        );

        Assert.True(insertedSplit);
        Assert.IsType<SplitNode>(tree.Root);

        List<(LeafNodeBase Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 102, 50)
        );

        AssertRect(new Rect2(0, 0, 50, 50), results.BoundsOf(inserted));
        AssertRect(new Rect2(52, 0, 50, 50), results.BoundsOf(original));
    }

    [Fact]
    public void InsertSplit_NestedLeaf_ReplacesParentChild()
    {
        LayoutTree tree = new();
        FakeLeaf left = new(0, 0);
        FakeLeaf target = new(0, 0);
        FakeLeaf inserted = new(0, 0);
        tree.SetRoot(new SplitNode(left, target, SplitAxis.Horizontal));

        bool insertedSplit = tree.InsertSplit(
            target,
            inserted,
            SplitAxis.Vertical,
            InsertPlacement.After
        );

        Assert.True(insertedSplit);

        List<(LeafNodeBase Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 202, 102)
        );

        AssertRect(new Rect2(0, 0, 100, 102), results.BoundsOf(left));
        AssertRect(new Rect2(102, 0, 100, 50), results.BoundsOf(target));
        AssertRect(new Rect2(102, 52, 100, 50), results.BoundsOf(inserted));
    }

    [Fact]
    public void InsertSplit_TargetOutsideTree_ReturnsFalse()
    {
        LayoutTree tree = new();
        tree.SetRoot(new FakeLeaf(0, 0));

        bool insertedSplit = tree.InsertSplit(
            new FakeLeaf(0, 0),
            new FakeLeaf(0, 0),
            SplitAxis.Horizontal,
            InsertPlacement.After
        );

        Assert.False(insertedSplit);
    }

    [Fact]
    public void RemoveLeaf_RootLeaf_ReturnsFalseAndKeepsRoot()
    {
        LayoutTree tree = new();
        FakeLeaf leaf = new(10, 10);
        tree.SetRoot(leaf);

        bool removed = tree.RemoveLeaf(leaf);

        Assert.False(removed);
        Assert.Same(leaf, tree.Root);

        List<(LeafNodeBase Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 100, 80)
        );

        Assert.Single(results);
        AssertRect(new Rect2(0, 0, 100, 80), results.BoundsOf(leaf));
    }

    [Fact]
    public void RemoveLeaf_RootSplitChild_PromotesSiblingToRoot()
    {
        LayoutTree tree = new();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(new SplitNode(left, right, SplitAxis.Horizontal));

        bool removed = tree.RemoveLeaf(left);

        Assert.True(removed);
        Assert.Same(right, tree.Root);

        List<(LeafNodeBase Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 100, 80)
        );

        Assert.Single(results);
        AssertRect(new Rect2(0, 0, 100, 80), results.BoundsOf(right));
    }

    [Fact]
    public void RemoveLeaf_NestedLeaf_CollapsesParentSplit()
    {
        LayoutTree tree = new();
        FakeLeaf left = new(0, 0);
        FakeLeaf nestedTop = new(0, 0);
        FakeLeaf nestedBottom = new(0, 0);
        SplitNode nested = new(nestedTop, nestedBottom, SplitAxis.Vertical);
        tree.SetRoot(new SplitNode(left, nested, SplitAxis.Horizontal));

        bool removed = tree.RemoveLeaf(nestedTop);

        Assert.True(removed);

        List<(LeafNodeBase Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 202, 100)
        );

        Assert.Equal(2, results.Count);
        AssertRect(new Rect2(0, 0, 100, 100), results.BoundsOf(left));
        AssertRect(new Rect2(102, 0, 100, 100), results.BoundsOf(nestedBottom));
    }

    [Fact]
    public void RemoveLeaf_TargetOutsideTree_ReturnsFalse()
    {
        LayoutTree tree = new();
        tree.SetRoot(new FakeLeaf(0, 0));

        bool removed = tree.RemoveLeaf(new FakeLeaf(0, 0));

        Assert.False(removed);
    }

    [Fact]
    public void GetMinimumSize_DelegatesToRoot()
    {
        LayoutTree tree = new();
        tree.SetRoot(
            new SplitNode(new FakeLeaf(30, 20), new FakeLeaf(40, 10), SplitAxis.Horizontal)
        );

        AssertVec(new Vector2(75, 20), tree.GetMinimumSize(borderThickness: 5));
    }
}
