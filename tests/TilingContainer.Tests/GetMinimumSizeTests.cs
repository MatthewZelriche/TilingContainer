using Godot;
using Xunit;
using static TilingContainer.Tests.LayoutTestHelpers;

namespace TilingContainer.Tests;

public class GetMinimumSizeTests
{
    [Fact]
    public void Leaf_ReturnsItsOwnMinimum()
    {
        FakeLeaf leaf = new(30, 20);
        AssertVec(new Vector2(30, 20), NewTree().GetMinimumSize(Leaf(leaf), borderThickness: 5));
    }

    [Fact]
    public void HorizontalSplit_SumsPrimaryAxis_AndTakesMaxPerpendicular_PlusBorder()
    {
        var split = Split(new FakeLeaf(30, 20), new FakeLeaf(40, 10), SplitAxis.Horizontal);

        // X: 30 + 40 + border(5) = 75 ; Y: max(20, 10) = 20
        AssertVec(new Vector2(75, 20), NewTree().GetMinimumSize(split, borderThickness: 5));
    }

    [Fact]
    public void VerticalSplit_SumsPrimaryAxis_AndTakesMaxPerpendicular_PlusBorder()
    {
        var split = Split(new FakeLeaf(30, 20), new FakeLeaf(40, 10), SplitAxis.Vertical);

        // X: max(30, 40) = 40 ; Y: 20 + 10 + border(5) = 35
        AssertVec(new Vector2(40, 35), NewTree().GetMinimumSize(split, borderThickness: 5));
    }

    [Fact]
    public void NestedSplits_AggregateRecursively()
    {
        // Outer horizontal: [ A(10,10) | vertical( B(20,5) / C(20,5) ) ]
        var inner = Split(new FakeLeaf(20, 5), new FakeLeaf(20, 5), SplitAxis.Vertical);
        var outer = Split(Leaf(new FakeLeaf(10, 10)), inner, SplitAxis.Horizontal);

        // inner: (max(20,20)=20, 5+5+4=14)
        // outer: (10 + 20 + 4 = 34, max(10, 14) = 14)
        AssertVec(new Vector2(34, 14), NewTree().GetMinimumSize(outer, borderThickness: 4));
    }
}
