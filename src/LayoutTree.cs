using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        if (Root is null)
        {
            return false;
        }

        SplitNode? parent = child.Parent;
        if (parent is null)
        {
            // No parent means the child is the root, but we want to ensure there's always a root
            // If the user wants to replace the root, they should use SetRoot.
            return false;
        }

        bool removingLeft = ReferenceEquals(parent.Left, child);
        bool removingRight = ReferenceEquals(parent.Right, child);
        if (!removingLeft && !removingRight)
        {
            return false;
        }

        LayoutNode sibling = removingLeft ? parent.Right : parent.Left;
        SplitNode? grandparent = parent.Parent;
        if (grandparent is null)
        {
            if (!ReferenceEquals(Root, parent))
            {
                return false;
            }

            child.Parent = null;
            sibling.Parent = null;
            parent.Parent = null;
            Root = sibling;
            return true;
        }

        bool parentIsLeft = ReferenceEquals(grandparent.Left, parent);
        bool parentIsRight = ReferenceEquals(grandparent.Right, parent);
        if (!parentIsLeft && !parentIsRight)
        {
            return false;
        }

        child.Parent = null;
        sibling.Parent = null;
        parent.Parent = null;

        if (parentIsLeft)
        {
            grandparent.Left = sibling;
        }
        else
        {
            grandparent.Right = sibling;
        }

        return true;
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
