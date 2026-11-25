using System.IO;
using System.Runtime.InteropServices;

namespace LBRAInstaller;

// A desktop shortcut to the game (Windows Script Host's WScript.Shell, late bound: no interop assembly needed).
internal static class Shortcut
{
    // (the desktop; for tests another folder: LBRA_DESKTOP_DIR)
    private static string DesktopFolder() =>
        Environment.GetEnvironmentVariable("LBRA_DESKTOP_DIR") is { Length: > 0 } dir ? dir : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    // The desktop's shortcut `name` to `from` (the game's exe of before) made to start `to` instead. True: it was.
    public static bool Repoint(string from, string to, string name)
    {
        var path = Path.Combine(DesktopFolder(), name + ".lnk");
        if (!File.Exists(path)) return false;
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host isn't there.");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            var changed = false;
            if (string.Equals(Path.GetFullPath((string)link.TargetPath), Path.GetFullPath(from), StringComparison.OrdinalIgnoreCase))
            {
                link.TargetPath = to;
                link.WorkingDirectory = Path.GetDirectoryName(to);
                link.Save();
                changed = true;
            }
            Marshal.FinalReleaseComObject(link);
            return changed;
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }

    // The desktop's shortcut `from` renamed `to` (the game's name changed: Little Big Halloween Racing Adventure became Little Big Racing
    // Adventure, 2026-11-25), its description with it -- only one that starts a game in `installDir` (another install's, a test's, left as
    // it is: a sandbox install renamed the player's own shortcut, 2026-11-25). True: it was. Not when there's one by the new name already.
    public static bool Rename(string from, string to, string installDir)
    {
        var old = Path.Combine(DesktopFolder(), from + ".lnk");
        var now = Path.Combine(DesktopFolder(), to + ".lnk");
        if (!File.Exists(old) || File.Exists(now)) return false;
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host isn't there.");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic link = shell.CreateShortcut(old);
            var target = (string)link.TargetPath;
            Marshal.FinalReleaseComObject(link);
            var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDir)) + Path.DirectorySeparatorChar;
            if (string.IsNullOrEmpty(target) || !Path.GetFullPath(target).StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return false;
            File.Move(old, now);
            link = shell.CreateShortcut(now);
            link.Description = to;
            link.Save();
            Marshal.FinalReleaseComObject(link);
        }
        finally { Marshal.FinalReleaseComObject(shell); }
        return true;
    }

    public static void Desktop(string target, string name, string? icon = null)
    {
        var path = Path.Combine(DesktopFolder(), name + ".lnk");
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host isn't there.");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = target;
            link.WorkingDirectory = Path.GetDirectoryName(target);
            link.IconLocation = (icon is not null && File.Exists(icon) ? icon : target) + ",0";
            link.Description = name;
            link.Save();
            Marshal.FinalReleaseComObject(link);
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
