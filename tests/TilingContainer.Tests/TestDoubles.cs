using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;

namespace TilingContainer.Tests;

// A leaf whose minimum size is supplied directly, with no dependency on a Godot Control.
// This is the whole point of LeafNodeBase existing: production uses LeafNode (backed by a
// real Control), tests substitute this.
internal sealed class FakeLeaf : LeafNodeBase
{
    private readonly Vector2 _minSize;

    public FakeLeaf(Vector2 minSize) => _minSize = minSize;

    public FakeLeaf(float x, float y) => _minSize = new Vector2(x, y);

    public override Vector2 GetMinimumSize(float borderThickness) => _minSize;
}

internal static class LayoutTestHelpers
{
    private const float Tolerance = 0.001f;

    // Runs a layout pass and returns the (leaf, bounds) assignments in traversal order.
    public static List<(LeafNodeBase Node, Rect2 Bounds)> Collect(
        LayoutNode root,
        float borderThickness,
        Rect2 available
    )
    {
        List<(LeafNodeBase, Rect2)> results = new();
        root.ApplyLayout((node, bounds) => results.Add((node, bounds)), borderThickness, available);
        return results;
    }

    public static Rect2 BoundsOf(
        this List<(LeafNodeBase Node, Rect2 Bounds)> results,
        LeafNodeBase leaf
    ) => results.Single(r => ReferenceEquals(r.Node, leaf)).Bounds;

    public static void AssertVec(Vector2 expected, Vector2 actual)
    {
        Assert.True(
            expected.IsEqualApprox(actual) || expected.DistanceTo(actual) <= Tolerance,
            $"Expected vector {expected}, got {actual}"
        );
    }

    public static void AssertRect(Rect2 expected, Rect2 actual)
    {
        bool ok =
            System.Math.Abs(expected.Position.X - actual.Position.X) <= Tolerance
            && System.Math.Abs(expected.Position.Y - actual.Position.Y) <= Tolerance
            && System.Math.Abs(expected.Size.X - actual.Size.X) <= Tolerance
            && System.Math.Abs(expected.Size.Y - actual.Size.Y) <= Tolerance;
        Assert.True(ok, $"Expected rect {expected}, got {actual}");
    }
}
