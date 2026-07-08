using System;
using System.Collections.Generic;
using Godot;

internal sealed class LayoutTree
{
    internal LayoutNode? Root { get; set; } = null;

    internal void SetRoot(LayoutNode root)
    {
        // TODO: Need to tear down the old root
        Root = root;
    }

    internal void ApplyLayout(
        Action<LeafNodeBase, Rect2> applyLayoutFunc,
        float borderThickness,
        Rect2 availableSize
    )
    {
        Root?.ApplyLayout(applyLayoutFunc, borderThickness, availableSize);
    }

    public Vector2 GetMinimumSize(float borderThickness) =>
        Root?.GetMinimumSize(borderThickness) ?? Vector2.Zero;
}
