using System.Diagnostics;

namespace TransVoice.Live.Infrastructure;

/// <summary>
/// Результат проверки одной зависимости.
/// </summary>
public record DependencyStatus(string Name, bool IsAvailable, string? InstallHint);

/// <summary>
/// Проверяет наличие системных зависимостей: arecord и xclip.
/// </summary>
public class DependencyChecker
{
    private static readonly (
        string Command,
        string Package,
        string InstallCommand
    )[] RequiredDeps =
    [
        (
            "arecord",
            "alsa-utils",
            "sudo apt install alsa-utils   # или: sudo dnf install alsa-utils"
        ),
        ("xclip", "xclip", "sudo apt install xclip        # или: sudo dnf install xclip"),
    ];

    /// <summary>
    /// Проверяет все зависимости и возвращает их статусы.
    /// </summary>
    public IReadOnlyList<DependencyStatus> CheckAll()
    {
        var results = new List<DependencyStatus>();
        foreach (var (cmd, pkg, hint) in RequiredDeps)
        {
            bool available = IsCommandAvailable(cmd);
            results.Add(
                new DependencyStatus(
                    Name: cmd,
                    IsAvailable: available,
                    InstallHint: available ? null : $"Установите пакет '{pkg}':\n  {hint}"
                )
            );
        }
        return results;
    }

    /// <summary>
    /// Возвращает true, если все обязательные зависимости присутствуют.
    /// </summary>
    public bool AllSatisfied() => CheckAll().All(d => d.IsAvailable);

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo("which", command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process == null)
                return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
