using System.Diagnostics;
using System.Runtime.Versioning;

namespace NeuralNetGoal.ConsoleApp;

/// <summary>The few things that differ between Windows and macOS (and Linux): where "Downloads" is, and how to open a file.</summary>
public static class Platform
{
    /// <summary>Folder for every file the console creates. <paramref name="configured"/> (Export:Directory) wins if set;
    /// otherwise the user's Downloads folder. A leading "~" means the home folder.</summary>
    public static string ResolveExportDirectory(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var p = configured.Trim();
            if (p == "~" || p.StartsWith("~/") || p.StartsWith("~\\"))
                p = Path.Combine(Home(), p.Length > 2 ? p[2..] : "");
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(p));
        }

        return DownloadsFolder();
    }

    public static string DownloadsFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            var fromRegistry = WindowsDownloadsFolder();
            if (!string.IsNullOrWhiteSpace(fromRegistry)) return fromRegistry!;
        }

        // macOS and Linux: ~/Downloads (the Windows fallback is the same, C:\Users\<name>\Downloads)
        return Path.Combine(Home(), "Downloads");
    }

    /// <summary>Windows lets users move the Downloads folder, so ask the shell settings instead of assuming.</summary>
    [SupportedOSPlatform("windows")]
    private static string? WindowsDownloadsFolder()
    {
        try
        {
            // {374DE290-123F-4565-9164-D6A8D6E9B2F2} is the well-known id of the Downloads folder
            return Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
                "{374DE290-123F-4565-9164-D6A8D6E9B2F2}", null) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string Home() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Opens a file with the program the operating system associates with it (Excel for .xlsx).
    /// Never throws: if it cannot be opened the caller just keeps the path.</summary>
    public static bool OpenFile(string path, out string? error)
    {
        error = null;
        try
        {
            ProcessStartInfo psi;
            if (OperatingSystem.IsWindows())
                psi = new ProcessStartInfo { FileName = path, UseShellExecute = true };
            else if (OperatingSystem.IsMacOS())
                psi = new ProcessStartInfo("open") { ArgumentList = { path } };
            else
                psi = new ProcessStartInfo("xdg-open") { ArgumentList = { path } };

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
