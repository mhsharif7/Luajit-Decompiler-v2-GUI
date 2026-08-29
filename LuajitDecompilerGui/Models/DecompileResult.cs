namespace LuajitDecompilerGui.Models;

public sealed class DecompileResult
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public bool Skipped { get; init; }
    public int? ExitCode { get; init; }
    public string? OutputPath { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}
