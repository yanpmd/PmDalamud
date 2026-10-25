using Dalamud.Plugin.Services;

namespace Una.Drawing;

public static class DebugLogger
{
    public static IPluginLog? Writer { get; set; }

    public static void Log(string message)
    {
        Writer?.Info(message);
    }
    
    public static void Error(string message)
    {
        Writer?.Error(message);
    }
}
