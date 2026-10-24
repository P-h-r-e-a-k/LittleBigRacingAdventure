namespace LBAAssembler;

// The editor's DataPaths (LBA Assembler's DataPaths.cs) as the installer has it: the editor's own folders come from its settings.json,
// which the installer has none of, so Windows' own. (RendererLibraryApi.cs and Lba2Engine.cs, compiled in from the editor, ask for it.)
internal static class DataPaths
{
    public static string RoamingAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
}
