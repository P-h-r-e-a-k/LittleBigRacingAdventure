using System.IO;
using System.Runtime.InteropServices;

namespace LBRAInstaller;

// The game's exe given the game's own icon, the pumpkin, in place of the engine's (the user, 2026-10-24: "LBRA in windowed mode displays
// the icon of the engine, let's use our current LBRA icon instead"). The engine's window takes its icon from the exe's first icon group
// (SDL: ExtractIconEx of the program's own file), and so do the taskbar and Explorer: the exe's icons are replaced -- its icon groups
// and their images taken out, the pumpkin.ico's images put in as group 1 -- with Windows' own resource update.
internal static class GameIcon
{
    private const int RtIcon = 3, RtGroupIcon = 14;
    private const uint LoadLibraryAsDatafile = 0x2, LoadLibraryAsImageResource = 0x20;

    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
    private delegate bool EnumResLangProc(IntPtr module, IntPtr type, IntPtr name, ushort language, IntPtr param);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc proc, IntPtr param);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceLanguages(IntPtr module, IntPtr type, IntPtr name, EnumResLangProc proc, IntPtr param);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(string path, bool deleteExisting);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[]? data, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr update, bool discard);

    // The exe's icons (groups and images) by number and language -- the engine's resource script names them by number.
    private static List<(int Type, int Id, ushort Language)> Icons(string exe)
    {
        var found = new List<(int, int, ushort)>();
        var module = LoadLibraryEx(exe, IntPtr.Zero, LoadLibraryAsDatafile | LoadLibraryAsImageResource);
        if (module == IntPtr.Zero) throw new IOException($"{Path.GetFileName(exe)} could not be read for its icons (error {Marshal.GetLastWin32Error()}).");
        try
        {
            foreach (var type in new[] { RtGroupIcon, RtIcon })
                EnumResourceNames(module, (IntPtr)type, (m, t, name, p) =>
                {
                    if ((ulong)name >> 16 != 0) return true;   // (a named one: the engine has none)
                    EnumResourceLanguages(m, t, name, (m2, t2, n2, language, p2) => { found.Add((type, (int)name, language)); return true; }, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
        }
        finally { FreeLibrary(module); }
        return found;
    }

    // `icon`: an .ico file's bytes. Returns how many images went in.
    public static int Replace(string exe, byte[] icon)
    {
        if (icon.Length < 6 || BitConverter.ToUInt16(icon, 2) != 1) throw new InvalidDataException("The game's icon isn't an icon file.");
        var count = BitConverter.ToUInt16(icon, 4);
        if (count == 0 || icon.Length < 6 + 16 * count) throw new InvalidDataException("The game's icon has no pictures.");
        var old = Icons(exe);
        var language = old.Where(o => o.Type == RtGroupIcon).Select(o => o.Language).DefaultIfEmpty((ushort)0).First();
        // the group: the .ico's directory with each image's place in the file replaced by its number among the exe's icons
        var group = new byte[6 + 14 * count];
        Array.Copy(icon, 0, group, 0, 6);
        var images = new List<byte[]>();
        for (var i = 0; i < count; i++)
        {
            var entry = 6 + 16 * i;
            Array.Copy(icon, entry, group, 6 + 14 * i, 12);
            var size = BitConverter.ToInt32(icon, entry + 8);
            var at = BitConverter.ToInt32(icon, entry + 12);
            if (size <= 0 || at < 0 || at + size > icon.Length) throw new InvalidDataException("The game's icon is cut short.");
            images.Add(icon[at..(at + size)]);
            BitConverter.GetBytes((ushort)(i + 1)).CopyTo(group, 6 + 14 * i + 12);
        }
        var update = BeginUpdateResource(exe, false);
        if (update == IntPtr.Zero) throw new IOException($"{Path.GetFileName(exe)} could not be opened for its icons (error {Marshal.GetLastWin32Error()}).");
        var done = false;
        try
        {
            foreach (var (type, id, lang) in old)
                if (!UpdateResource(update, (IntPtr)type, (IntPtr)id, lang, null, 0)) throw new IOException($"the engine's icon {id} could not be taken out (error {Marshal.GetLastWin32Error()}).");
            for (var i = 0; i < images.Count; i++)
                if (!UpdateResource(update, (IntPtr)RtIcon, (IntPtr)(i + 1), language, images[i], (uint)images[i].Length)) throw new IOException($"the icon's picture {i + 1} could not be put in (error {Marshal.GetLastWin32Error()}).");
            if (!UpdateResource(update, (IntPtr)RtGroupIcon, (IntPtr)1, language, group, (uint)group.Length)) throw new IOException($"the icon could not be put in (error {Marshal.GetLastWin32Error()}).");
            done = true;
        }
        finally
        {
            if (!EndUpdateResource(update, !done) && done) throw new IOException($"{Path.GetFileName(exe)}'s icons could not be written (error {Marshal.GetLastWin32Error()}).");
        }
        return images.Count;
    }
}
