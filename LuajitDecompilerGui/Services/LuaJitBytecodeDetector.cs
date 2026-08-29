using LuajitDecompilerGui.Models;

namespace LuajitDecompilerGui.Services;

public sealed class LuaJitBytecodeDetector
{
    private const byte Head1 = 0x1B;
    private const byte Head2 = 0x4C;
    private const byte Head3 = 0x4A;
    private const byte LuaJit20Version = 1;
    private const byte LuaJit21Version = 2;
    private const uint FlagBigEndian = 0x01;
    private const uint FlagStrip = 0x02;
    private const uint FlagFfi = 0x04;
    private const uint FlagFr2 = 0x08;

    public BytecodeDetectionResult Detect(string filePath)
    {
        try
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (stream.Length < 4)
                return DetectNonBytecode(filePath);

            Span<byte> header = stackalloc byte[16];
            int bytesRead = stream.Read(header);

            if (bytesRead < 4)
                return DetectNonBytecode(filePath);

            bool hasLuaJitMagic =
                header[0] == Head1 &&
                header[1] == Head2 &&
                header[2] == Head3;

            if (!hasLuaJitMagic)
                return DetectNonBytecode(filePath, header[..bytesRead]);

            byte version = header[3];
            uint? flags = null;

            if (bytesRead > 4 && TryReadUleb128(header[..bytesRead], 4, out uint parsedFlags, out _))
                flags = parsedFlags;

            return BuildLuaJitResult(version, flags);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unreadable(ex.Message);
        }
        catch (IOException ex)
        {
            return Unreadable(ex.Message);
        }
        catch (Exception ex)
        {
            return Unreadable(ex.Message);
        }
    }

    private static BytecodeDetectionResult BuildLuaJitResult(byte version, uint? flags)
    {
        uint flagValue = flags ?? 0;
        bool bigEndian = (flagValue & FlagBigEndian) != 0;
        bool stripped = (flagValue & FlagStrip) != 0;
        bool ffi = (flagValue & FlagFfi) != 0;
        bool fr2 = (flagValue & FlagFr2) != 0;

        if (version == LuaJit20Version)
        {
            return new BytecodeDetectionResult
            {
                Format = FileFormat.LuaJit20Bytecode,
                Version = version,
                Flags = flags,
                IsBigEndian = bigEndian,
                IsStripped = stripped,
                UsesFfi = ffi,
                UsesFr2 = false,
                Description = "LuaJIT 2.0 bytecode"
            };
        }

        if (version == LuaJit21Version)
        {
            return new BytecodeDetectionResult
            {
                Format = FileFormat.LuaJit21Bytecode,
                Version = version,
                Flags = flags,
                IsBigEndian = bigEndian,
                IsStripped = stripped,
                UsesFfi = ffi,
                UsesFr2 = fr2,
                Description = "LuaJIT 2.1 bytecode"
            };
        }

        if (version >= 0x80)
        {
            return new BytecodeDetectionResult
            {
                Format = FileFormat.LuaJitCustomBytecode,
                Version = version,
                Flags = flags,
                IsBigEndian = bigEndian,
                IsStripped = stripped,
                UsesFfi = ffi,
                UsesFr2 = fr2,
                Description = $"Custom LuaJIT bytecode (version 0x{version:X2})"
            };
        }

        return new BytecodeDetectionResult
        {
            Format = FileFormat.LuaJitUnknownBytecode,
            Version = version,
            Flags = flags,
            IsBigEndian = bigEndian,
            IsStripped = stripped,
            UsesFfi = ffi,
            UsesFr2 = fr2,
            Description = $"Unknown LuaJIT bytecode version ({version})"
        };
    }

    private static BytecodeDetectionResult DetectNonBytecode(string filePath, ReadOnlySpan<byte> data = default)
    {
        string extension = Path.GetExtension(filePath);

        if (extension.Equals(".lua", StringComparison.OrdinalIgnoreCase))
        {
            bool probablyText = data.IsEmpty || LooksLikeText(data);
            if (probablyText)
            {
                return new BytecodeDetectionResult
                {
                    Format = FileFormat.PlainLuaSource,
                    Description = "Lua source (not bytecode)"
                };
            }
        }

        return new BytecodeDetectionResult
        {
            Format = FileFormat.NotLuaJit,
            Description = "Not LuaJIT bytecode"
        };
    }

    private static bool LooksLikeText(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return true;
        foreach (byte value in data)
            if (value == 0) return false;
        return true;
    }

    private static BytecodeDetectionResult Unreadable(string message) =>
        new()
        {
            Format = FileFormat.Unreadable,
            Description = $"Unreadable: {message}"
        };

    private static bool TryReadUleb128(ReadOnlySpan<byte> data, int offset, out uint value, out int bytesConsumed)
    {
        value = 0;
        bytesConsumed = 0;
        int shift = 0;

        for (int i = offset; i < data.Length && bytesConsumed < 5; i++)
        {
            byte current = data[i];
            uint chunk = (uint)(current & 0x7F);
            value |= chunk << shift;
            bytesConsumed++;

            if ((current & 0x80) == 0)
                return true;

            shift += 7;
        }

        value = 0;
        bytesConsumed = 0;
        return false;
    }
}
