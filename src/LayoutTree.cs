using System;
using System.Collections.Generic;
using Godot;

internal sealed class LayoutTree<T>
    where T : notnull
{
    // Computes the minimum size of a given T
    private readonly Func<T, float, Vector2> _getMinimumSize;
    private readonly Dictionary<T, LeafNode<T>> _leafNodes;

    internal LayoutTree(
        Func<T, float, Vector2> getMinimumSize,
        IEqualityComparer<T>? equalityComparer = null
    )
    {
        _getMinimumSize = getMinimumSize;
        _leafNodes = new Dictionary<T, LeafNode<T>>(equalityComparer);
    }

    internal LayoutNode<T>? Root { get; private set; } = null;

    internal IEnumerable<T> Leaves => _leafNodes.Keys;

    internal bool Contains(T leaf) => _leafNodes.ContainsKey(leaf);

    internal void SetRoot(T root)
    {
        LeafNode<T> rootNode = CreateLeaf(root);

        _leafNodes.Clear();
        _leafNodes.Add(root, rootNode);
        Root = rootNode;
    }

    // Given a leaf node newChild, replace toSplit with a new split node that contains
    // both newChild and toSplit as children
    internal bool InsertSplit(T toSplit, T newChild, SplitAxis axis, InsertPlacement placement)
    {
        if (Root is null || !_leafNodes.TryGetValue(toSplit, out LeafNode<T>? toSplitNode))
        {
            return false;
        }

        if (_leafNodes.ContainsKey(newChild))
        {
            return false;
        }

        LeafNode<T> newChildNode = CreateLeaf(newChild);
        if (!InsertSplitNode(toSplitNode, newChildNode, axis, placement))
        {
            return false;
        }

        _leafNodes.Add(newChild, newChildNode);
        return true;
    }

    // Removes child from the tree
    internal bool RemoveLeaf(T child)
    {
        if (Root is null || !_leafNodes.TryGetValue(child, out LeafNode<T>? childNode))
        {
            return false;
        }

        if (!RemoveLeafNode(childNode))
        {
            return false;
        }

        _leafNodes.Remove(child);
        return true;
    }

    // Detaches the child toMove and re-attaches it to a new split node that contains both
    // target and toMove as children.
    internal bool RelocateLeaf(T toMove, T target, SplitAxis axis, InsertPlacement placement)
    {
        if (
            Root is null
            || !_leafNodes.TryGetValue(toMove, out LeafNode<T>? toMoveNode)
            || !_leafNodes.TryGetValue(target, out LeafNode<T>? targetNode)
            || ReferenceEquals(toMoveNode, targetNode)
        )
        {
            return false;
        }

        if (!RemoveLeafNode(toMoveNode))
        {
            return false;
        }

        return InsertSplitNode(targetNode, toMoveNode, axis, placement);
    }

    internal void ApplyLayout(
        Action<T, Rect2> applyLayoutFunc,
        float borderThickness,
        Rect2 availableSize
    )
    {
        if (Root is not null)
        {
            ApplyLayout(Root, applyLayoutFunc, borderThickness, availableSize);
        }
    }

    public Vector2 GetMinimumSize(float borderThickness) =>
        Root is null ? Vector2.Zero : GetMinimumSize(Root, borderThickness);

    internal void ApplyLayout(
        LayoutNode<T> node,
        Action<T, Rect2> applyLayoutFunc,
        float borderThickness,
        Rect2 availableSize
    )
    {
        switch (node)
        {
            case LeafNode<T> leaf:
                applyLayoutFunc(leaf.Item, availableSize);
                break;

            case SplitNode<T> split:
                ApplySplitLayout(split, applyLayoutFunc, borderThickness, availableSize);
                break;

            default:
                throw new InvalidOperationException("Unsupported layout node type.");
        }
    }

    internal Vector2 GetMinimumSize(LayoutNode<T> node, float borderThickness)
    {
        return node switch
        {
            LeafNode<T> leaf => _getMinimumSize(leaf.Item, borderThickness),
            SplitNode<T> split => GetSplitMinimumSize(split, borderThickness),
            _ => throw new InvalidOperationException("Unsupported layout node type."),
        };
    }

    private LeafNode<T> CreateLeaf(T item) => new(item);

    private Vector2 GetSplitMinimumSize(SplitNode<T> split, float borderThickness)
    {
        Vector2 leftMinimum = GetMinimumSize(split.Left, borderThickness);
        Vector2 rightMinimum = GetMinimumSize(split.Right, borderThickness);

        if (split.Axis == SplitAxis.Horizontal)
        {
            return new Vector2(
                leftMinimum.X + rightMinimum.X + borderThickness,
                Mathf.Max(leftMinimum.Y, rightMinimum.Y)
            );
        }

        return new Vector2(
            Mathf.Max(leftMinimum.X, rightMinimum.X),
            leftMinimum.Y + rightMinimum.Y + borderThickness
        );
    }

    private void ApplySplitLayout(
        SplitNode<T> split,
        Action<T, Rect2> layoutFunc,
        float borderThickness,
        Rect2 availableSize
    )
    {
        bool horizontal = split.Axis == SplitAxis.Horizontal;
        float primarySpan = horizontal ? availableSize.Size.X : availableSize.Size.Y;
        float perpendicularSpan = horizontal ? availableSize.Size.Y : availableSize.Size.X;
        // The span of the content area, after subtracting the border thickness.
        float contentSpan = Mathf.Max(0.0f, primarySpan - borderThickness);
        ComputeChildSpans(
            split,
            contentSpan,
            borderThickness,
            out float leftSpan,
            out float rightSpan
        );

        Vector2 pos = availableSize.Position;
        float borderOffset = leftSpan;
        float secondOffset = leftSpan + borderThickness;

        Rect2 leftRect = SplitRect(pos, horizontal, leftSpan, perpendicularSpan);
        split.BorderRect = SplitRect(
            OffsetAlong(pos, horizontal, borderOffset),
            horizontal,
            borderThickness,
            perpendicularSpan
        );
        Rect2 rightRect = SplitRect(
            OffsetAlong(pos, horizontal, secondOffset),
            horizontal,
            rightSpan,
            perpendicularSpan
        );

        ApplyLayout(split.Left, layoutFunc, borderThickness, leftRect);
        ApplyLayout(split.Right, layoutFunc, borderThickness, rightRect);
    }

    private void ComputeChildSpans(
        SplitNode<T> split,
        float contentSpan,
        float borderThickness,
        out float leftSpan,
        out float rightSpan
    )
    {
        if (contentSpan <= 0.0f)
        {
            leftSpan = 0.0f;
            rightSpan = 0.0f;
            return;
        }

        Vector2 leftMinSize = GetMinimumSize(split.Left, borderThickness);
        Vector2 rightMinSize = GetMinimumSize(split.Right, borderThickness);
        float leftPrimaryMinSpan =
            split.Axis == SplitAxis.Horizontal ? leftMinSize.X : leftMinSize.Y;
        float rightPrimaryMinSpan =
            split.Axis == SplitAxis.Horizontal ? rightMinSize.X : rightMinSize.Y;
        float totalMinimum = leftPrimaryMinSpan + rightPrimaryMinSpan;

        if (totalMinimum > contentSpan)
        {
            // If the total minimum is greater than the content span, then we need to shrink the children to fit the content span.
            // TODO: Is this appropriate? We are explicitly ignoring the requested minimum sizes...
            leftSpan = contentSpan * (leftPrimaryMinSpan / totalMinimum);
        }
        else
        {
            leftSpan = Mathf.Clamp(
                contentSpan * split.Ratio,
                leftPrimaryMinSpan,
                contentSpan - rightPrimaryMinSpan
            );
        }

        // The remaining space is assigned to the second child.
        rightSpan = contentSpan - leftSpan;
    }

    private static Rect2 SplitRect(
        Vector2 origin,
        bool horizontal,
        float primarySpan,
        float perpendicularSpan
    )
    {
        return horizontal
            ? new Rect2(origin, new Vector2(primarySpan, perpendicularSpan))
            : new Rect2(origin, new Vector2(perpendicularSpan, primarySpan));
    }

    private static Vector2 OffsetAlong(Vector2 origin, bool horizontal, float offset)
    {
        return horizontal ? origin + new Vector2(offset, 0) : origin + new Vector2(0, offset);
    }

    private bool InsertSplitNode(
        LeafNode<T> toSplit,
        LeafNode<T> newChild,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        if (Root is null || ReferenceEquals(toSplit, newChild))
        {
            return false;
        }

        SplitNode<T>? parent = toSplit.Parent;
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

        LayoutNode<T> left = placement == InsertPlacement.Before ? newChild : toSplit;
        LayoutNode<T> right = placement == InsertPlacement.Before ? toSplit : newChild;

        toSplit.Parent = null;
        SplitNode<T> split = new(left, right, axis);

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

    private bool RemoveLeafNode(LeafNode<T> child)
    {
        if (Root is null)
        {
            return false;
        }

        SplitNode<T>? parent = child.Parent;
        if (parent is null)
        {
            // No parent means the child is the root, but we want to ensure there's always a root.
            // If the user wants to replace the root, they should use SetRoot.
            return false;
        }

        bool removingLeft = ReferenceEquals(parent.Left, child);
        bool removingRight = ReferenceEquals(parent.Right, child);
        if (!removingLeft && !removingRight)
        {
            return false;
        }

        LayoutNode<T> sibling = removingLeft ? parent.Right : parent.Left;
        SplitNode<T>? grandparent = parent.Parent;
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
}
