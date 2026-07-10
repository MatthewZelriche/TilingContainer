using System;
using Godot;

public partial class TilingPaneContainer<T> : TilingContainer
    where T : Control
{
    private readonly Func<T> _contentFactory;
    private TilingContainerPane<T>? _draggedPane = null;
    private TilingContainerPane<T>? _dropTargetPane = null;
    private SplitAxis _dropAxis = SplitAxis.Horizontal;
    private InsertPlacement _dropPlacement = InsertPlacement.After;
    private Rect2? _dropPreviewRect = null;
    private DropPreviewOverlay? _dropPreviewOverlay = null;
    private readonly Color _dropPreviewColor = new(0.25f, 0.55f, 1.0f, 0.28f);

    public TilingPaneContainer()
        : this(CreateDefaultContent) { }

    public TilingPaneContainer(Func<T> contentFactory)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);
        _contentFactory = contentFactory;
    }

    public TilingContainerPane<T> CreatePane()
    {
        TilingContainerPane<T> pane = new(_contentFactory());
        ConnectPane(pane);
        return pane;
    }

    public void SetRootPane(TilingContainerPane<T> pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        ConnectPane(pane);
        SetRoot(pane);
    }

    public bool InsertPaneSplit(
        TilingContainerPane<T> toSplit,
        TilingContainerPane<T> newPane,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        ArgumentNullException.ThrowIfNull(toSplit);
        ArgumentNullException.ThrowIfNull(newPane);

        ConnectPane(toSplit);
        ConnectPane(newPane);
        if (!InsertSplit(toSplit, newPane, axis, placement))
        {
            DisconnectPane(newPane);
            return false;
        }

        return true;
    }

    private static T CreateDefaultContent()
    {
        try
        {
            return Activator.CreateInstance<T>()
                ?? throw new InvalidOperationException($"Could not create {typeof(T).Name}.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{typeof(T).Name} must have a parameterless constructor or TilingPaneContainer must be created with a content factory.",
                ex
            );
        }
    }

    private void ConnectPane(TilingContainerPane<T> pane)
    {
        // Unregister existing handlers for idempotency
        pane.SplitRequested -= OnPaneSplitRequested;
        pane.CloseRequested -= OnPaneCloseRequested;
        pane.DragStarted -= OnPaneDragStarted;
        pane.DragMoved -= OnPaneDragMoved;
        pane.DragEnded -= OnPaneDragEnded;
        pane.SplitRequested += OnPaneSplitRequested;
        pane.CloseRequested += OnPaneCloseRequested;
        pane.DragStarted += OnPaneDragStarted;
        pane.DragMoved += OnPaneDragMoved;
        pane.DragEnded += OnPaneDragEnded;
    }

    private void DisconnectPane(TilingContainerPane<T> pane)
    {
        pane.SplitRequested -= OnPaneSplitRequested;
        pane.CloseRequested -= OnPaneCloseRequested;
        pane.DragStarted -= OnPaneDragStarted;
        pane.DragMoved -= OnPaneDragMoved;
        pane.DragEnded -= OnPaneDragEnded;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what == NotificationSortChildren && _dropPreviewOverlay is not null)
        {
            FitChildInRect(_dropPreviewOverlay, new Rect2(Vector2.Zero, Size));
            MoveChild(_dropPreviewOverlay, GetChildCount() - 1);
        }
    }

    private void OnPaneSplitRequested(
        TilingContainerPane<T> pane,
        SplitAxis axis,
        InsertPlacement placement
    )
    {
        TilingContainerPane<T> newPane = CreatePane();
        if (!InsertSplit(pane, newPane, axis, placement))
        {
            DisconnectPane(newPane);
            newPane.QueueFree();
        }
    }

    private void OnPaneCloseRequested(TilingContainerPane<T> pane)
    {
        if (RemoveLeaf(pane))
        {
            DisconnectPane(pane);
            pane.QueueFree();
        }
    }

    private void OnPaneDragStarted(TilingContainerPane<T> pane, Vector2 viewportPosition)
    {
        _draggedPane = pane;
        UpdateDropPreview(viewportPosition);
    }

    private void OnPaneDragMoved(TilingContainerPane<T> pane, Vector2 viewportPosition)
    {
        if (!ReferenceEquals(_draggedPane, pane))
        {
            return;
        }

        UpdateDropPreview(viewportPosition);
    }

    private void OnPaneDragEnded(TilingContainerPane<T> pane, Vector2 viewportPosition)
    {
        if (!ReferenceEquals(_draggedPane, pane))
        {
            return;
        }

        UpdateDropPreview(viewportPosition);
        TilingContainerPane<T>? target = _dropTargetPane;
        SplitAxis axis = _dropAxis;
        InsertPlacement placement = _dropPlacement;
        ClearDropPreview();

        if (target is not null && !ReferenceEquals(target, pane))
        {
            RelocateLeaf(pane, target, axis, placement);
        }
    }

    private void UpdateDropPreview(Vector2 viewportPosition)
    {
        if (_draggedPane is null)
        {
            ClearDropPreview();
            return;
        }

        Vector2 localPosition = ToLocalPosition(viewportPosition);
        Control? target = FindLeafAt(localPosition);
        if (
            target is not TilingContainerPane<T> targetPane
            || ReferenceEquals(targetPane, _draggedPane)
        )
        {
            _dropTargetPane = null;
            SetDropPreview(null);
            return;
        }

        if (
            !TryGetDropPlacement(
                targetPane,
                localPosition,
                out SplitAxis axis,
                out InsertPlacement placement
            )
        )
        {
            _dropTargetPane = null;
            SetDropPreview(null);
            return;
        }

        _dropTargetPane = targetPane;
        _dropAxis = axis;
        _dropPlacement = placement;
        SetDropPreview(GetInsertPreviewRect(targetPane, axis, placement));
    }

    private bool TryGetDropPlacement(
        TilingContainerPane<T> targetPane,
        Vector2 localPosition,
        out SplitAxis axis,
        out InsertPlacement placement
    )
    {
        Rect2? bounds = GetLeafBounds(targetPane);
        if (bounds is null)
        {
            axis = SplitAxis.Horizontal;
            placement = InsertPlacement.After;
            return false;
        }

        Rect2 rect = bounds.Value;
        float left = Mathf.Abs(localPosition.X - rect.Position.X);
        float right = Mathf.Abs(localPosition.X - (rect.Position.X + rect.Size.X));
        float top = Mathf.Abs(localPosition.Y - rect.Position.Y);
        float bottom = Mathf.Abs(localPosition.Y - (rect.Position.Y + rect.Size.Y));
        float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));

        if (Mathf.IsEqualApprox(min, left))
        {
            axis = SplitAxis.Horizontal;
            placement = InsertPlacement.Before;
        }
        else if (Mathf.IsEqualApprox(min, right))
        {
            axis = SplitAxis.Horizontal;
            placement = InsertPlacement.After;
        }
        else if (Mathf.IsEqualApprox(min, top))
        {
            axis = SplitAxis.Vertical;
            placement = InsertPlacement.Before;
        }
        else
        {
            axis = SplitAxis.Vertical;
            placement = InsertPlacement.After;
        }

        return true;
    }

    private void ClearDropPreview()
    {
        _draggedPane = null;
        _dropTargetPane = null;
        SetDropPreview(null);
    }

    private void SetDropPreview(Rect2? previewRect)
    {
        _dropPreviewRect = previewRect;
        if (_dropPreviewOverlay is null && previewRect is null)
        {
            return;
        }

        EnsureDropPreviewOverlay().SetPreview(previewRect, _dropPreviewColor);
    }

    private DropPreviewOverlay EnsureDropPreviewOverlay()
    {
        if (_dropPreviewOverlay is null)
        {
            _dropPreviewOverlay = new DropPreviewOverlay
            {
                Name = "DropPreviewOverlay",
                MouseFilter = MouseFilterEnum.Ignore,
                ZAsRelative = false,
                ZIndex = 4096,
            };
            AddChild(_dropPreviewOverlay);
        }
        else if (_dropPreviewOverlay.GetParent() is null)
        {
            AddChild(_dropPreviewOverlay);
        }

        FitChildInRect(_dropPreviewOverlay, new Rect2(Vector2.Zero, Size));
        MoveChild(_dropPreviewOverlay, GetChildCount() - 1);
        return _dropPreviewOverlay;
    }

    private sealed partial class DropPreviewOverlay : Control
    {
        private Rect2? _previewRect = null;
        private Color _previewColor = Colors.Transparent;

        public void SetPreview(Rect2? previewRect, Color previewColor)
        {
            _previewRect = previewRect;
            _previewColor = previewColor;
            Visible = previewRect is not null;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_previewRect is Rect2 rect)
            {
                DrawRect(rect, _previewColor);
            }
        }
    }
}
