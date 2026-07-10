using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Godot;

public enum SplitAxis
{
    Horizontal,
    Vertical,
}

public enum InsertPlacement
{
    Before,
    After,
}

internal abstract class LayoutNode<T>
    where T : notnull
{
    internal SplitNode<T>? Parent { get; set; } = null;
}

internal sealed class LeafNode<T> : LayoutNode<T>
    where T : notnull
{
    internal LeafNode(T item)
    {
        Item = item;
    }

    internal T Item { get; }
}

internal sealed class SplitNode<T> : LayoutNode<T>
    where T : notnull
{
    [SetsRequiredMembers]
    internal SplitNode(LayoutNode<T> left, LayoutNode<T> right, SplitAxis axis)
    {
        Left = left;
        Right = right;
        Axis = axis;
    }

    // Safe because the private backing members are always initialized by the field setters, which
    // are called in the constructor.
    private LayoutNode<T> _left = null!;
    private LayoutNode<T> _right = null!;
    public required LayoutNode<T> Left
    {
        get => _left;
        internal set
        {
            Debug.Assert(value is not null && value.Parent is null);
            _left = value;
            value.Parent = this;
        }
    }
    public required LayoutNode<T> Right
    {
        get => _right;
        internal set
        {
            Debug.Assert(value is not null && value.Parent is null);
            _right = value;
            value.Parent = this;
        }
    }
    public required SplitAxis Axis { get; init; }
    public float Ratio = 0.5f; // Default ratio is 50%
    public Rect2 Bounds { get; internal set; } // Full bounds of the split node including children
    public Rect2 BorderRect { get; internal set; } // Visual bounds of the border
}
