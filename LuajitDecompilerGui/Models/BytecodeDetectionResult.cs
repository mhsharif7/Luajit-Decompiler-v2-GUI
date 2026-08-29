namespace LuajitDecompilerGui.Models;

public sealed class BytecodeDetectionResult
{
    public FileFormat Format { get; init; }
    public byte? Version { get; init; }
    public uint? Flags { get; init; }
    public bool IsBigEndian { get; init; }
    public bool IsStripped { get; init; }
    public bool UsesFfi { get; init; }
    public bool UsesFr2 { get; init; }
    public string Description { get; init; } = string.Empty;

    public bool IsSupportedBytecode =>
        Format is FileFormat.LuaJit20Bytecode or FileFormat.LuaJit21Bytecode;
}
