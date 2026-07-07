using System.Diagnostics;
using Godot;

public enum SplitAxis
{
    Horizontal,
    Vertical,
}

internal abstract class LayoutNode
{
    public abstract Vector2 GetMinimumSize(float borderThickness);

    // Called when the container layout is invalidated (via the NotificationSortChildren notification)
    // to re-compute the layout for all children of this node.
    public abstract void SortChildren(TilingContainer parent, Rect2 availableSize);
}

internal sealed class LeafNode : LayoutNode
{
    public required Control Control { get; init; }

    public override Vector2 GetMinimumSize(float _)
    {
        return Control.GetCombinedMinimumSize();
    }

    public override void SortChildren(TilingContainer parent, Rect2 availableSize)
    {
        Debug.Assert(Control.GetParent() == parent);
        parent.FitChildInRect(Control, availableSize);
    }
}

internal sealed class SplitNode : LayoutNode
{
    public required SplitAxis Axis { get; init; }
    public required LayoutNode Left { get; init; }
    public required LayoutNode Right { get; init; }
    public float Ratio = 0.5f; // Default ratio is 50%
    public Rect2 BorderRect { get; private set; }

    // Gets the minimum size of the split node, including border thickness and both children
    public override Vector2 GetMinimumSize(float borderThickness)
    {
        Vector2 leftMinimum = Left.GetMinimumSize(borderThickness);
        Vector2 rightMinimum = Right.GetMinimumSize(borderThickness);

        if (Axis == SplitAxis.Horizontal)
        {
            return new Vector2(
                leftMinimum.X + rightMinimum.X + borderThickness,
                Mathf.Max(leftMinimum.Y, rightMinimum.Y)
            );
        }
        else
        {
            return new Vector2(
                Mathf.Max(leftMinimum.X, rightMinimum.X),
                leftMinimum.Y + rightMinimum.Y + borderThickness
            );
        }
    }

    public override void SortChildren(TilingContainer parent, Rect2 availableSize)
    {
        float borderThickness = parent.BorderThickness;
        bool horizontal = Axis == SplitAxis.Horizontal;
        float primarySpan = horizontal ? availableSize.Size.X : availableSize.Size.Y;
        float perpendicularSpan = horizontal ? availableSize.Size.Y : availableSize.Size.X;
        // The span of the content area, after subtracting the border thickness
        float contentSpan = Mathf.Max(0.0f, primarySpan - borderThickness);
        ComputeChildSpans(contentSpan, borderThickness, out float leftSpan, out float rightSpan);

        Vector2 pos = availableSize.Position;
        float borderOffset = leftSpan;
        float secondOffset = rightSpan + borderThickness;

        Rect2 leftRect = SplitRect(pos, horizontal, leftSpan, perpendicularSpan);
        BorderRect = SplitRect(
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

        Left.SortChildren(parent, leftRect);
        Right.SortChildren(parent, rightRect);
    }

    private void ComputeChildSpans(
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

        Vector2 leftMinSize = Left.GetMinimumSize(borderThickness);
        Vector2 rightMinSize = Right.GetMinimumSize(borderThickness);
        float leftPrimaryMinSpan = Axis == SplitAxis.Horizontal ? leftMinSize.X : leftMinSize.Y;
        float rightPrimaryMinSpan = Axis == SplitAxis.Horizontal ? rightMinSize.X : rightMinSize.Y;
        float totalMinimum = leftPrimaryMinSpan + rightPrimaryMinSpan;

        if (totalMinimum > contentSpan)
        {
            // If the total minimum is greater than the content span, then we need to shrink the children to fit the content span
            // TODO: Is this appropriate? We are explicitly ignoring the requested minimum sizes...
            leftSpan = contentSpan * (leftPrimaryMinSpan / totalMinimum);
        }
        else
        {
            leftSpan = Mathf.Clamp(
                contentSpan * Ratio,
                leftPrimaryMinSpan,
                contentSpan - rightPrimaryMinSpan
            );
        }

        // The remaining space is assigned to the second child
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
}
