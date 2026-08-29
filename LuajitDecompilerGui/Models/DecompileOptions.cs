using System.Diagnostics;

namespace LuajitDecompilerGui.Models;

public sealed class DecompileOptions
{
    public bool ForceOverwrite { get; set; }
    public bool SilentAssertions { get; set; } = true;
    public bool IgnoreDebugInfo { get; set; }
    public bool MinimizeDiffs { get; set; }
    public bool UnrestrictedAscii { get; set; }
    public string ExtensionFilter { get; set; } = string.Empty;

    public void AddArguments(ProcessStartInfo startInfo)
    {
        if (ForceOverwrite) startInfo.ArgumentList.Add("-f");
        if (SilentAssertions) startInfo.ArgumentList.Add("-s");
        if (IgnoreDebugInfo) startInfo.ArgumentList.Add("-i");
        if (MinimizeDiffs) startInfo.ArgumentList.Add("-m");
        if (UnrestrictedAscii) startInfo.ArgumentList.Add("-u");

        if (!string.IsNullOrWhiteSpace(ExtensionFilter))
        {
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(NormalizeExtension(ExtensionFilter));
        }
    }

    public bool MatchesExtension(string filePath)
    {
        if (string.IsNullOrWhiteSpace(ExtensionFilter)) return true;

        string required = NormalizeExtension(ExtensionFilter);
        string actual = Path.GetExtension(filePath);

        return actual.Equals(required, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension)
    {
        extension = extension.Trim();
        if (extension.Length == 0) return extension;
        return extension.StartsWith('.') ? extension : "." + extension;
    }
}
