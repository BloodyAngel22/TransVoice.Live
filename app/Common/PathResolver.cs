namespace TransVoice.Live.Common;

public static class PathResolver
{
    private static string? _cachedRoot;

    public static string GetRootDirectory()
    {
        if (_cachedRoot != null)
            return _cachedRoot;

        var current = AppContext.BaseDirectory;

        for (int i = 0; i < 5; i++)
        {
            if (Directory.Exists(Path.Combine(current, "Models")))
            {
                _cachedRoot = current;
                return current;
            }

            var parent = Path.GetDirectoryName(current);
            if (parent == null || parent == current)
                break;
            current = parent;
        }

        _cachedRoot = AppContext.BaseDirectory;
        return _cachedRoot;
    }
}
