#if TOOLS
using Godot;

[Tool]
public partial class TilingContainerPlugin : EditorPlugin
{
    public override void _EnterTree()
    {
        // Initialization of the plugin goes here.
        GD.Print("Loaded TilingContainerPlugin");
    }

    public override void _ExitTree()
    {
        // Clean-up of the plugin goes here.
    }
}
#endif
