using MelonLoader;

namespace SmartStorage;

internal static class Log
{
    internal static MelonLogger.Instance Logger;

    internal static void Msg(string message)
    {
        try { Logger?.Msg(message); } catch { }
    }

    internal static void Warning(string message)
    {
        try { Logger?.Warning(message); } catch { }
    }

    internal static void Error(string message)
    {
        try { Logger?.Error(message); } catch { }
    }

    internal static void Debug(string message)
    {
        if (Config.DebugLogging != null && Config.DebugLogging.Value) Msg("[debug] " + message);
    }
}
