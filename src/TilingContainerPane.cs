using System;
using Godot;

public partial class TilingContainerPane<T> : VBoxContainer
    where T : Control
{
    private const string LayoutScenePath =
        "res://addons/TilingContainer/src/TilingContainerPane.tscn";
    private const int SplitLeftId = 0;
    private const int SplitRightId = 1;
    private const int SplitTopId = 2;
    private const int SplitBottomId = 3;

    private Control _menuBar = null!;
    private MarginContainer _contentHost = null!;
    private bool _isDraggingMenuBar = false;

    public TilingContainerPane()
        : this(CreateDefaultContent) { }

    public TilingContainerPane(Func<T> contentFactory)
        : this(contentFactory()) { }

    public TilingContainerPane(T content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
        BuildUi();
    }

    public event Action<TilingContainerPane<T>, SplitAxis, InsertPlacement>? SplitRequested;
    public event Action<TilingContainerPane<T>>? CloseRequested;
    public event Action<TilingContainerPane<T>, Vector2>? DragStarted;
    public event Action<TilingContainerPane<T>, Vector2>? DragMoved;
    public event Action<TilingContainerPane<T>, Vector2>? DragEnded;

    public T Content { get; private set; }

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
                $"{typeof(T).Name} must have a parameterless constructor or TilingContainerPane must be created with a content factory.",
                ex
            );
        }
    }

    private void BuildUi()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        VBoxContainer layout = LoadLayoutScene();
        AddChild(layout);

        _menuBar = layout.GetNode<Control>("MenuBar");
        _menuBar.GuiInput += OnMenuBarGuiInput;

        BindSplitMenu(layout);
        BindButton(layout, "MenuBar/MenuBarContents/CloseButton", Close);

        _contentHost = layout.GetNode<MarginContainer>("ContentHost");

        Content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Content.SizeFlagsVertical = SizeFlags.ExpandFill;
        _contentHost.AddChild(Content);
    }

    private static VBoxContainer LoadLayoutScene()
    {
        PackedScene? scene = GD.Load<PackedScene>(LayoutScenePath);
        if (scene is null)
        {
            throw new InvalidOperationException(
                $"Could not load pane layout scene at {LayoutScenePath}."
            );
        }

        return scene.Instantiate<VBoxContainer>();
    }

    private void BindSplitMenu(Node root)
    {
        MenuButton button = root.GetNode<MenuButton>("MenuBar/MenuBarContents/SplitMenuButton");
        PopupMenu popup = button.GetPopup();
        popup.Clear();
        popup.AddItem("Left", SplitLeftId);
        popup.AddItem("Right", SplitRightId);
        popup.AddItem("Top", SplitTopId);
        popup.AddItem("Bottom", SplitBottomId);
        popup.IdPressed += OnSplitMenuIdPressed;
    }

    private static void BindButton(Node root, NodePath path, Action onPressed)
    {
        Button button = root.GetNode<Button>(path);
        button.Pressed += onPressed;
    }

    private void OnSplitMenuIdPressed(long id)
    {
        switch (id)
        {
            case SplitLeftId:
                Split(SplitAxis.Horizontal, InsertPlacement.Before);
                break;
            case SplitRightId:
                Split(SplitAxis.Horizontal, InsertPlacement.After);
                break;
            case SplitTopId:
                Split(SplitAxis.Vertical, InsertPlacement.Before);
                break;
            case SplitBottomId:
                Split(SplitAxis.Vertical, InsertPlacement.After);
                break;
        }
    }

    private void Split(SplitAxis axis, InsertPlacement placement)
    {
        SplitRequested?.Invoke(this, axis, placement);
    }

    private void Close()
    {
        CloseRequested?.Invoke(this);
    }

    private void OnMenuBarGuiInput(InputEvent @event)
    {
        if (
            @event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
        )
        {
            Vector2 viewportPosition = ToViewportPosition(mouseButton.Position);
            if (mouseButton.Pressed)
            {
                _isDraggingMenuBar = true;
                DragStarted?.Invoke(this, viewportPosition);
                AcceptEvent();
                return;
            }

            if (_isDraggingMenuBar)
            {
                _isDraggingMenuBar = false;
                DragEnded?.Invoke(this, viewportPosition);
                AcceptEvent();
            }

            return;
        }

        if (_isDraggingMenuBar && @event is InputEventMouseMotion mouseMotion)
        {
            DragMoved?.Invoke(this, ToViewportPosition(mouseMotion.Position));
            AcceptEvent();
        }
    }

    private Vector2 ToViewportPosition(Vector2 menuBarPosition)
    {
        return _menuBar.GetGlobalTransformWithCanvas() * menuBarPosition;
    }
}
