using System.Diagnostics;

namespace MiaomiaoHelper;

internal static class Utils
{
    private static string StartupPath()
    {
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    public static void StartMiaomiao()
    {
        var executable = Path.Combine(
            StartupPath(),
            OperatingSystem.IsWindows() ? "Miaomiao.exe" : "Miaomiao");
        Process process = new()
        {
            StartInfo = new()
            {
                UseShellExecute = true,
                FileName = executable,
                WorkingDirectory = StartupPath()
            }
        };
        process.Start();
    }
}
