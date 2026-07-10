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
        LayoutTree<FakeLeaf> tree = NewTree();
        AssertVec(Vector2.Zero, tree.GetMinimumSize(borderThickness: 5));
    }

    [Fact]
    public void EmptyTree_ApplyLayout_DoesNotInvokeCallback()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        int calls = 0;

        tree.ApplyLayout((_, _) => calls++, borderThickness: 2, new Rect2(0, 0, 100, 100));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void SetRoot_ThenApplyLayout_LaysOutThroughTheRoot()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf leaf = new(10, 10);
        tree.SetRoot(leaf);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
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
        LayoutTree<FakeLeaf> tree = NewTree();
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
        Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
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
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf target = new(0, 0);
        FakeLeaf inserted = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, target, SplitAxis.Horizontal, InsertPlacement.After));

        bool insertedSplit = tree.InsertSplit(
            target,
            inserted,
            SplitAxis.Vertical,
            InsertPlacement.After
        );

        Assert.True(insertedSplit);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
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
        LayoutTree<FakeLeaf> tree = NewTree();
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
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf leaf = new(10, 10);
        tree.SetRoot(leaf);

        bool removed = tree.RemoveLeaf(leaf);

        Assert.False(removed);
        Assert.True(tree.Contains(leaf));

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
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
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));

        bool removed = tree.RemoveLeaf(left);

        Assert.True(removed);
        Assert.False(tree.Contains(left));
        Assert.True(tree.Contains(right));

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
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
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf nestedTop = new(0, 0);
        FakeLeaf nestedBottom = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, nestedTop, SplitAxis.Horizontal, InsertPlacement.After));
        Assert.True(
            tree.InsertSplit(nestedTop, nestedBottom, SplitAxis.Vertical, InsertPlacement.After)
        );

        bool removed = tree.RemoveLeaf(nestedTop);

        Assert.True(removed);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
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
        LayoutTree<FakeLeaf> tree = NewTree();
        tree.SetRoot(new FakeLeaf(0, 0));

        bool removed = tree.RemoveLeaf(new FakeLeaf(0, 0));

        Assert.False(removed);
    }

    [Fact]
    public void RelocateLeaf_RootSplitChild_PromotesTargetThenSplitsRoot()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));

        bool relocated = tree.RelocateLeaf(left, right, SplitAxis.Vertical, InsertPlacement.After);

        Assert.True(relocated);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 100, 102)
        );

        Assert.Equal(2, results.Count);
        AssertRect(new Rect2(0, 0, 100, 50), results.BoundsOf(right));
        AssertRect(new Rect2(0, 52, 100, 50), results.BoundsOf(left));
    }

    [Fact]
    public void RelocateLeaf_NestedLeaf_CollapsesOldParentAndInsertsBesideTarget()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf nestedTop = new(0, 0);
        FakeLeaf nestedBottom = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, nestedTop, SplitAxis.Horizontal, InsertPlacement.After));
        Assert.True(
            tree.InsertSplit(nestedTop, nestedBottom, SplitAxis.Vertical, InsertPlacement.After)
        );

        bool relocated = tree.RelocateLeaf(
            nestedBottom,
            left,
            SplitAxis.Vertical,
            InsertPlacement.Before
        );

        Assert.True(relocated);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 202, 102)
        );

        Assert.Equal(3, results.Count);
        AssertRect(new Rect2(0, 0, 100, 50), results.BoundsOf(nestedBottom));
        AssertRect(new Rect2(0, 52, 100, 50), results.BoundsOf(left));
        AssertRect(new Rect2(102, 0, 100, 102), results.BoundsOf(nestedTop));
    }

    [Fact]
    public void RelocateLeaf_TargetOutsideTree_ReturnsFalse()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf root = new(0, 0);
        FakeLeaf sibling = new(0, 0);
        tree.SetRoot(root);
        Assert.True(tree.InsertSplit(root, sibling, SplitAxis.Horizontal, InsertPlacement.After));

        bool relocated = tree.RelocateLeaf(
            root,
            new FakeLeaf(0, 0),
            SplitAxis.Vertical,
            InsertPlacement.After
        );

        Assert.False(relocated);

        List<(FakeLeaf Node, Rect2 Bounds)> results = new();
        tree.ApplyLayout(
            (n, b) => results.Add((n, b)),
            borderThickness: 2,
            new Rect2(0, 0, 202, 50)
        );

        Assert.Equal(2, results.Count);
        AssertRect(new Rect2(0, 0, 100, 50), results.BoundsOf(root));
        AssertRect(new Rect2(102, 0, 100, 50), results.BoundsOf(sibling));
    }

    [Fact]
    public void FindLeafAt_PointInsideLeaf_ReturnsLeaf()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 202, 50));

        FakeLeaf? hit = tree.FindLeafAt(new Vector2(150, 25));

        Assert.Same(right, hit);
    }

    [Fact]
    public void FindLeafAt_PointOutsideLeafBounds_ReturnsNull()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 202, 50));

        FakeLeaf? hit = tree.FindLeafAt(new Vector2(101, 25));

        Assert.Null(hit);
    }

    [Fact]
    public void GetInsertPreviewRect_HorizontalSplit_ReturnsRequestedHalfOfTarget()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 202, 50));

        Rect2? preview = tree.GetInsertPreviewRect(
            right,
            SplitAxis.Horizontal,
            InsertPlacement.Before
        );

        Assert.NotNull(preview);
        AssertRect(new Rect2(102, 0, 50, 50), preview.Value);
    }

    [Fact]
    public void GetInsertPreviewRect_VerticalSplit_ReturnsRequestedHalfOfTarget()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf top = new(0, 0);
        FakeLeaf bottom = new(0, 0);
        tree.SetRoot(top);
        Assert.True(tree.InsertSplit(top, bottom, SplitAxis.Vertical, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 50, 202));

        Rect2? preview = tree.GetInsertPreviewRect(
            bottom,
            SplitAxis.Vertical,
            InsertPlacement.After
        );

        Assert.NotNull(preview);
        AssertRect(new Rect2(0, 152, 50, 50), preview.Value);
    }

    [Fact]
    public void FindSplitBorderAt_PointInsideVisibleBorder_ReturnsSplit()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 102, 50));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        SplitNode<FakeLeaf>? hit = tree.FindSplitBorderAt(new Vector2(51, 25), grabWidth: 2);

        Assert.Same(split, hit);
    }

    [Fact]
    public void FindSplitBorderAt_PointInsideExpandedGrabWidth_ReturnsSplit()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 102, 50));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        SplitNode<FakeLeaf>? hit = tree.FindSplitBorderAt(new Vector2(47, 25), grabWidth: 10);

        Assert.Same(split, hit);
    }

    [Fact]
    public void FindSplitBorderAt_PointOutsideExpandedGrabWidth_ReturnsNull()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 102, 50));

        SplitNode<FakeLeaf>? hit = tree.FindSplitBorderAt(new Vector2(45, 25), grabWidth: 10);

        Assert.Null(hit);
    }

    [Fact]
    public void FindSplitBorderAt_VerticalSplit_UsesExpandedGrabHeight()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf top = new(0, 0);
        FakeLeaf bottom = new(0, 0);
        tree.SetRoot(top);
        Assert.True(tree.InsertSplit(top, bottom, SplitAxis.Vertical, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 50, 102));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        SplitNode<FakeLeaf>? hit = tree.FindSplitBorderAt(new Vector2(25, 47), grabWidth: 10);

        Assert.Same(split, hit);
    }

    [Fact]
    public void SetSplitRatioFromPoint_HorizontalSplit_UpdatesRatioFromX()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 102, 50));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        bool updated = tree.SetSplitRatioFromPoint(split, new Vector2(76, 25), borderThickness: 2);

        Assert.True(updated);
        Assert.Equal(0.75f, split.Ratio, precision: 3);
    }

    [Fact]
    public void SetSplitRatioFromPoint_VerticalSplit_UpdatesRatioFromY()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf top = new(0, 0);
        FakeLeaf bottom = new(0, 0);
        tree.SetRoot(top);
        Assert.True(tree.InsertSplit(top, bottom, SplitAxis.Vertical, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 50, 102));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        bool updated = tree.SetSplitRatioFromPoint(split, new Vector2(25, 26), borderThickness: 2);

        Assert.True(updated);
        Assert.Equal(0.25f, split.Ratio, precision: 3);
    }

    [Fact]
    public void SetSplitRatioFromPoint_ClampsToChildMinimums()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(30, 0);
        FakeLeaf right = new(20, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 102, 50));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        bool updated = tree.SetSplitRatioFromPoint(split, new Vector2(5, 25), borderThickness: 2);

        Assert.True(updated);
        Assert.Equal(0.3f, split.Ratio, precision: 3);
    }

    [Fact]
    public void SetSplitRatioFromPoint_OverConstrainedLayout_ReturnsFalse()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(80, 0);
        FakeLeaf right = new(80, 0);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));
        tree.ApplyLayout((_, _) => { }, borderThickness: 2, new Rect2(0, 0, 102, 50));
        SplitNode<FakeLeaf> split = Assert.IsType<SplitNode<FakeLeaf>>(tree.Root);

        bool updated = tree.SetSplitRatioFromPoint(split, new Vector2(80, 25), borderThickness: 2);

        Assert.False(updated);
        Assert.Equal(0.5f, split.Ratio, precision: 3);
    }

    [Fact]
    public void GetMinimumSize_DelegatesToRoot()
    {
        LayoutTree<FakeLeaf> tree = NewTree();
        FakeLeaf left = new(30, 20);
        FakeLeaf right = new(40, 10);
        tree.SetRoot(left);
        Assert.True(tree.InsertSplit(left, right, SplitAxis.Horizontal, InsertPlacement.After));

        AssertVec(new Vector2(75, 20), tree.GetMinimumSize(borderThickness: 5));
    }
}
