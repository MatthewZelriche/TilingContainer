using System;
using Godot;

public partial class TilingPaneContainer<T> : TilingContainer
    where T : Control
{
    private readonly Func<T> _contentFactory;

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
        pane.SplitRequested += OnPaneSplitRequested;
        pane.CloseRequested += OnPaneCloseRequested;
    }

    private void DisconnectPane(TilingContainerPane<T> pane)
    {
        pane.SplitRequested -= OnPaneSplitRequested;
        pane.CloseRequested -= OnPaneCloseRequested;
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
}
