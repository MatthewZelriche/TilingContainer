using Godot;
using Xunit;
using static TilingContainer.Tests.LayoutTestHelpers;

namespace TilingContainer.Tests;

public class ApplyLayoutTests
{
    [Fact]
    public void SingleLeaf_FillsEntireAvailableRect()
    {
        FakeLeaf leaf = new(10, 10);

        var results = Collect(leaf, borderThickness: 3, new Rect2(5, 7, 100, 50));

        Assert.Single(results);
        AssertRect(new Rect2(5, 7, 100, 50), results.BoundsOf(leaf));
    }

    [Fact]
    public void HorizontalSplit_EqualHalves_PlacesBothPanes()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);

        // width 102, border 2 -> content 100, 50/50 -> 50 each
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 102, 50));

        AssertRect(new Rect2(0, 0, 50, 50), results.BoundsOf(left));
        AssertRect(new Rect2(52, 0, 50, 50), results.BoundsOf(right));
    }

    [Fact]
    public void HorizontalSplit_RespectsRatio_ForPaneSizes()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);
        split.Ratio = 0.25f;

        // content 100, ratio 0.25 -> left 25, right 75
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 102, 50));

        Assert.Equal(25f, results.BoundsOf(left).Size.X, precision: 3);
        Assert.Equal(75f, results.BoundsOf(right).Size.X, precision: 3);
    }

    [Fact]
    public void HorizontalSplit_NonEqualRatio_PlacesRightPaneImmediatelyAfterBorder()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);
        split.Ratio = 0.25f;

        // content 100, ratio 0.25 -> left 25, border 2 -> right starts at 27
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 102, 50));

        AssertRect(new Rect2(0, 0, 25, 50), results.BoundsOf(left));
        AssertRect(new Rect2(27, 0, 75, 50), results.BoundsOf(right));
    }

    [Fact]
    public void HorizontalSplit_PanesTileTheFullWidthWithoutGapOrOverlap()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);
        split.Ratio = 0.3f;

        var results = Collect(split, borderThickness: 4, new Rect2(0, 0, 200, 50));

        Rect2 l = results.BoundsOf(left);
        Rect2 r = results.BoundsOf(right);

        // left starts at origin, right ends at the container edge, and the gap between
        // them equals exactly one border thickness.
        Assert.Equal(0f, l.Position.X, precision: 3);
        Assert.Equal(200f, r.Position.X + r.Size.X, precision: 3);
        Assert.Equal(4f, r.Position.X - (l.Position.X + l.Size.X), precision: 3);
    }

    [Fact]
    public void HorizontalSplit_ClampsToLeftMinimum()
    {
        FakeLeaf left = new(80, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);
        split.Ratio = 0.5f;

        // content 100, ratio 0.5 -> 50, but left minimum 80 wins
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 102, 50));

        Assert.Equal(80f, results.BoundsOf(left).Size.X, precision: 3);
        Assert.Equal(20f, results.BoundsOf(right).Size.X, precision: 3);
    }

    [Fact]
    public void HorizontalSplit_ClampsToRightMinimum()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(80, 0);
        var split = Split(left, right, SplitAxis.Horizontal);
        split.Ratio = 0.5f;

        // content 100, ratio 0.5 -> 50, but right minimum 80 caps left at 20
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 102, 50));

        Assert.Equal(20f, results.BoundsOf(left).Size.X, precision: 3);
        Assert.Equal(80f, results.BoundsOf(right).Size.X, precision: 3);
    }

    [Fact]
    public void HorizontalSplit_MinimumOverflow_ShrinksChildrenProportionally()
    {
        FakeLeaf left = new(40, 0);
        FakeLeaf right = new(20, 0);
        var split = Split(left, right, SplitAxis.Horizontal);

        // content 50, total minimum 60 > 50 -> left = 50 * (40/60) = 33.333, right = 16.667
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 52, 50));

        Assert.Equal(50f * (40f / 60f), results.BoundsOf(left).Size.X, precision: 3);
        Assert.Equal(50f * (20f / 60f), results.BoundsOf(right).Size.X, precision: 3);
    }

    [Fact]
    public void VerticalSplit_RespectsRatio_ForPaneSizes()
    {
        FakeLeaf top = new(0, 0);
        FakeLeaf bottom = new(0, 0);
        var split = Split(top, bottom, SplitAxis.Vertical);
        split.Ratio = 0.25f;

        // content 100, ratio 0.25 -> top 25, bottom 75, full width 50
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 50, 102));

        Assert.Equal(25f, results.BoundsOf(top).Size.Y, precision: 3);
        Assert.Equal(75f, results.BoundsOf(bottom).Size.Y, precision: 3);
        Assert.Equal(50f, results.BoundsOf(top).Size.X, precision: 3);
        Assert.Equal(50f, results.BoundsOf(bottom).Size.X, precision: 3);
    }

    [Fact]
    public void VerticalSplit_NonEqualRatio_PlacesBottomPaneImmediatelyAfterBorder()
    {
        FakeLeaf top = new(0, 0);
        FakeLeaf bottom = new(0, 0);
        var split = Split(top, bottom, SplitAxis.Vertical);
        split.Ratio = 0.25f;

        // content 100, ratio 0.25 -> top 25, border 2 -> bottom starts at y = 27
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 50, 102));

        AssertRect(new Rect2(0, 0, 50, 25), results.BoundsOf(top));
        AssertRect(new Rect2(0, 27, 50, 75), results.BoundsOf(bottom));
    }

    [Fact]
    public void HorizontalSplit_BorderRectSitsBetweenPanes()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);
        split.Ratio = 0.25f;

        // BorderRect is populated as a side effect of ApplyLayout.
        Collect(split, borderThickness: 2, new Rect2(0, 0, 102, 50));

        // left span 25 -> border occupies [25, 27] across the full height
        AssertRect(new Rect2(25, 0, 2, 50), split.BorderRect);
    }

    [Fact]
    public void NestedSplits_ProduceOneAssignmentPerLeaf()
    {
        FakeLeaf a = new(0, 0);
        FakeLeaf b = new(0, 0);
        FakeLeaf c = new(0, 0);
        var inner = Split(b, c, SplitAxis.Vertical);
        var outer = Split(Leaf(a), inner, SplitAxis.Horizontal);

        var results = Collect(outer, borderThickness: 2, new Rect2(0, 0, 200, 100));

        Assert.Equal(3, results.Count);
        // every leaf received a placement
        _ = results.BoundsOf(a);
        _ = results.BoundsOf(b);
        _ = results.BoundsOf(c);
    }

    [Fact]
    public void ContentSmallerThanBorder_ProducesZeroSizedPanes()
    {
        FakeLeaf left = new(0, 0);
        FakeLeaf right = new(0, 0);
        var split = Split(left, right, SplitAxis.Horizontal);

        // width equals the border, so there is no content span to distribute
        var results = Collect(split, borderThickness: 2, new Rect2(0, 0, 2, 50));

        Assert.Equal(0f, results.BoundsOf(left).Size.X, precision: 3);
        Assert.Equal(0f, results.BoundsOf(right).Size.X, precision: 3);
    }
}
