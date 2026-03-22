using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TransVoice.Live.Infrastructure;

/// <summary>
/// Копирует текст в системный буфер обмена через утилиту xclip.
/// </summary>
public class ClipboardManager
{
    public void CopyText(string text)
    {
        try
        {
            string cleanText = Regex.Replace(
                text,
                @"^\d{2}:\d{2}:\d{2}: ",
                "",
                RegexOptions.Multiline
            );

            var psi = new ProcessStartInfo("xclip", "-selection clipboard")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.StandardInput.Write(cleanText);
                process.StandardInput.Close();
                process.WaitForExit(2000);
            }
        }
        catch { }
    }
}
