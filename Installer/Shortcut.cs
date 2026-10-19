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
