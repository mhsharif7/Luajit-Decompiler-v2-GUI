namespace LuajitDecompilerGui.Models;

public enum FileStatus
{
    Pending,
    Ready,
    Processing,
    Success,
    Failed,
    Unsupported,
    Skipped,
    Cancelled
}
