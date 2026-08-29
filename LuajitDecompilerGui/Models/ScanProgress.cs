namespace LuajitDecompilerGui.Models;

public enum ScanPhase
{
    Discovering,
    Analyzing
}

public sealed record ScanProgress(
    ScanPhase Phase,
    int Current,
    int Total,
    string? CurrentPath = null);
