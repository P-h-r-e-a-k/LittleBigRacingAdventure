using System.IO;
using System.Runtime.InteropServices;

namespace LBRAInstaller;

// A desktop shortcut to the game (Windows Script Host's WScript.Shell, late bound: no interop assembly needed).
internal static class Shortcut
{
    public static void Desktop(string target, string name, string? icon = null)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk");
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
