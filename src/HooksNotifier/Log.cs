namespace HooksNotifier;

/// <summary>Simple file-based error logging (shared across all modes).</summary>
internal static class Log
{
    private static string GetPath()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath)!;
        return Path.Combine(dir, "notifier_error.log");
    }

    public static void Error(string message)
    {
        try
        {
            File.AppendAllText(GetPath(),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { /* ignore */ }
    }
}
