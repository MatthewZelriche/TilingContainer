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
        Root.Parent = null;
    }

    // Given a leaf node newChild, replace toSplit with a new split node that contains
    // both newChild and toSplit as children
    internal bool InsertSplit(
        LeafNodeBase toSplit,
        LeafNodeBase newChild,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        if (Root is null || ReferenceEquals(toSplit, newChild))
        {
            return false;
        }

        SplitNode? parent = toSplit.Parent;
        if (parent is null)
        {
            if (!ReferenceEquals(Root, toSplit))
            {
                return false;
            }
        }
        else if (!ReferenceEquals(parent.Left, toSplit) && !ReferenceEquals(parent.Right, toSplit))
        {
            return false;
        }

        if (newChild.Parent is not null || ReferenceEquals(Root, newChild))
        {
            return false;
        }

        LayoutNode left = placement == InsertPlacement.Before ? newChild : toSplit;
        LayoutNode right = placement == InsertPlacement.Before ? toSplit : newChild;

        toSplit.Parent = null;
        SplitNode split = new(left, right, axis);

        if (parent is null)
        {
            Root = split;
        }
        else if (ReferenceEquals(parent.Left, toSplit))
        {
            parent.Left = split;
        }
        else if (ReferenceEquals(parent.Right, toSplit))
        {
            parent.Right = split;
        }
        else
        {
            return false;
        }

        return true;
    }

    // Removes child from the tree
    internal bool RemoveLeaf(LeafNodeBase child)
    {
        throw new NotImplementedException();
    }

    // Detaches the child toMove and re-attaches it to a new split node that contains both
    // target and toMove as children.
    internal bool RelocateLeaf(
        LeafNodeBase toMove,
        LeafNodeBase target,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        throw new NotImplementedException();
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
