namespace LuajitDecompilerGui.Models;

public sealed record BatchProgress(
    int Completed,
    int Total,
    InputFile File,
    string Message);
