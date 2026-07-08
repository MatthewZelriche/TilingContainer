using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

// TODO: Need a way to add and remove splits/children to the container beyond just the root.

// Note: You should use the Create factory method to create this typically.
public partial class TilingContainer : Container
{
    // Assigned in SetRoot called via the constructor
    private LayoutTree _layoutTree = new();
    private readonly Dictionary<Control, LeafNode> _leafNodesByControl = new();

    // In Pixels
    private float _borderThickness = 1.0f;

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
            MarkLayoutDirty();
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
        if (root.GetParent() is not null)
        {
            throw new InvalidOperationException("Root must not have a parent");
        }

        // TODO: Need to tear down the old root
        LeafNode rootNode = new() { Control = root };
        _layoutTree.SetRoot(rootNode);
        _leafNodesByControl.Clear();
        _leafNodesByControl.Add(root, rootNode);
        AddChild(root);
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

        if (!_leafNodesByControl.TryGetValue(toSplit, out LeafNode? toSplitNode))
        {
            return false;
        }

        if (_leafNodesByControl.ContainsKey(newChild))
        {
            return false;
        }

        LeafNode newChildNode = new() { Control = newChild };
        if (!_layoutTree.InsertSplit(toSplitNode, newChildNode, axis, placement))
        {
            return false;
        }

        _leafNodesByControl.Add(newChild, newChildNode);
        AddChild(newChild);
        MarkLayoutDirty();
        return true;
    }

    public override Vector2 _GetMinimumSize() => _layoutTree.GetMinimumSize(BorderThickness);

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
                    Control leafControl = ((LeafNode)node).Control;
                    Debug.Assert(leafControl.GetParent() == this);
                    this.FitChildInRect(leafControl, bounds);
                },
                BorderThickness,
                new(Vector2.Zero, Size) // Start with the entire available space in the container
            );
        }
    }

    private void MarkLayoutDirty()
    {
        QueueSort();
        UpdateMinimumSize();
    }
}
