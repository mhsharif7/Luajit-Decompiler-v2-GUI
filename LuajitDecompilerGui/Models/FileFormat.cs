namespace LuajitDecompilerGui.Models;

public enum FileFormat
{
    Unknown = 0,
    LuaJit20Bytecode,
    LuaJit21Bytecode,
    LuaJitUnknownBytecode,
    LuaJitCustomBytecode,
    PlainLuaSource,
    NotLuaJit,
    Unreadable
}
