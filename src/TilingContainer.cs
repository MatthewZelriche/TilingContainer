using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

// Note: You should use the Create factory method to create this typically.
public partial class TilingContainer : Container
{
    private readonly LayoutTree<Control> _layoutTree = new(
        (control, _) => control.GetCombinedMinimumSize(),
        ReferenceEqualityComparer.Instance
    );

    // In Pixels
    private float _borderThickness = 1.0f;
    private float _borderGrabWidth = 12.0f;
    private Color _borderColor = new(0.18f, 0.18f, 0.18f, 1.0f);
    private SplitNode<Control>? _draggedSplit = null;
    private bool _isDraggingBorder = false;

    [Export(PropertyHint.Range, "1,64,1.0,or_greater")]
    public float BorderThickness
    {
        get => _borderThickness;
        set
        {
            value = Mathf.Max(1.0f, value);
            if (Mathf.IsEqualApprox(_borderThickness, value))
                return;
            _borderThickness = value;
            _borderGrabWidth = Mathf.Max(_borderGrabWidth, _borderThickness);
            MarkLayoutDirty();
        }
    }

    [Export(PropertyHint.Range, "1,64,1.0,or_greater")]
    public float BorderGrabWidth
    {
        get => Mathf.Max(Mathf.Max(1.0f, _borderThickness), _borderGrabWidth);
        set => _borderGrabWidth = Mathf.Max(Mathf.Max(1.0f, _borderThickness), value);
    }

    [Export]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            QueueRedraw();
        }
    }

    public static TilingContainer Create(Control root)
    {
        TilingContainer tilingContainer = new();
        tilingContainer.SetRoot(root);
        return tilingContainer;
    }

    // Sets the content of the root node, resetting the entire layout if a previous root was set.
    public void SetRoot(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        Node? currentParent = root.GetParent();
        bool rootIsCurrentLeaf = _layoutTree.Contains(root);
        if (currentParent is not null && (!rootIsCurrentLeaf || currentParent != this))
        {
            throw new InvalidOperationException("Root must not have a parent");
        }

        // Clear all old leaf controls BEFORE we tear down the old tree
        foreach (Control oldLeaf in _layoutTree.Leaves)
        {
            RemoveChild(oldLeaf);
        }

        _layoutTree.SetRoot(root);
        AddChild(root);
        MarkLayoutDirty();
    }

    public bool InsertSplit(
        Control toSplit,
        Control newChild,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        ArgumentNullException.ThrowIfNull(toSplit);
        ArgumentNullException.ThrowIfNull(newChild);

        if (newChild.GetParent() is not null)
        {
            throw new InvalidOperationException("New child must not have a parent");
        }

        if (!_layoutTree.InsertSplit(toSplit, newChild, axis, placement))
        {
            return false;
        }

        AddChild(newChild);
        MarkLayoutDirty();
        return true;
    }

    public bool RemoveLeaf(Control child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_layoutTree.RemoveLeaf(child))
        {
            return false;
        }

        RemoveChild(child);
        MarkLayoutDirty();
        return true;
    }

    public bool RelocateLeaf(
        Control toMove,
        Control target,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(target);

        if (ReferenceEquals(toMove, target))
        {
            return false;
        }

        if (!_layoutTree.RelocateLeaf(toMove, target, axis, placement))
        {
            return false;
        }

        MarkLayoutDirty();
        return true;
    }

    public override Vector2 _GetMinimumSize() => _layoutTree.GetMinimumSize(BorderThickness);

    // Use _Input instead of _GuiInput so we can intercept mouse events before they reach children
    public override void _Input(InputEvent @event)
    {
        if (
            @event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
        )
        {
            HandleBorderMouseButton(mouseButton.Pressed, ToLocalPosition(mouseButton.Position));
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleBorderMouseMotion(ToLocalPosition(mouseMotion.Position));
        }
    }

    // Overridden to draw the custom border lines for the split nodes.
    public override void _Draw()
    {
        if (_layoutTree.Root is null || BorderThickness <= 0.0f || BorderColor.A <= 0.0f)
        {
            return;
        }

        DrawBorders(_layoutTree.Root);
    }

    public override void _Notification(int what)
    {
        if (_layoutTree.Root is null)
        {
            return;
        }

        // Container has changed, recursively re-organize the layout
        if (what == NotificationSortChildren)
        {
            _layoutTree.ApplyLayout(
                (node, bounds) =>
                {
                    Debug.Assert(node.GetParent() == this);
                    this.FitChildInRect(node, bounds);
                },
                BorderThickness,
                new(Vector2.Zero, Size) // Start with the entire available space in the container
            );
            QueueRedraw();
        }
    }

    private void DrawBorders(LayoutNode<Control> node)
    {
        if (node is not SplitNode<Control> split)
        {
            return;
        }

        DrawRect(split.BorderRect, BorderColor);
        DrawBorders(split.Left);
        DrawBorders(split.Right);
    }

    private void HandleBorderMouseButton(bool pressed, Vector2 position)
    {
        if (pressed)
        {
            SplitNode<Control>? split = _layoutTree.FindSplitBorderAt(position, BorderGrabWidth);
            if (split is null)
            {
                return;
            }

            _draggedSplit = split;
            _isDraggingBorder = true;
            UpdateDraggedBorder(position);
            AcceptEvent();
            return;
        }

        if (_isDraggingBorder)
        {
            _draggedSplit = null;
            _isDraggingBorder = false;
            AcceptEvent();
        }
    }

    private void HandleBorderMouseMotion(Vector2 position)
    {
        if (!_isDraggingBorder || _draggedSplit is null)
        {
            return;
        }

        UpdateDraggedBorder(position);
        AcceptEvent();
    }

    private void UpdateDraggedBorder(Vector2 position)
    {
        if (_draggedSplit is null)
        {
            return;
        }

        if (_layoutTree.SetSplitRatioFromPoint(_draggedSplit, position, BorderThickness))
        {
            MarkLayoutDirty();
        }
    }

    private Vector2 ToLocalPosition(Vector2 viewportPosition)
    {
        return GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
    }

    private void MarkLayoutDirty()
    {
        QueueSort();
        UpdateMinimumSize();
    }
}
