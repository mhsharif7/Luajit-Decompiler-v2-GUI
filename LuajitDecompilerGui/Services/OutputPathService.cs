using LuajitDecompilerGui.Models;

namespace LuajitDecompilerGui.Services;

public sealed class OutputPathService
{
    public string GetOutputPath(InputFile input, string outputRoot, bool preserveDirectoryStructure)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
            throw new ArgumentException("Output folder is required.", nameof(outputRoot));

        string fullOutputRoot = Path.GetFullPath(outputRoot);
        string destinationDirectory = fullOutputRoot;

        if (preserveDirectoryStructure)
        {
            string? relativeDirectory = Path.GetDirectoryName(input.RelativePath);
            if (!string.IsNullOrWhiteSpace(relativeDirectory))
                destinationDirectory = Path.Combine(fullOutputRoot, relativeDirectory);
        }

        destinationDirectory = Path.GetFullPath(destinationDirectory);
        EnsureInsideOutputRoot(fullOutputRoot, destinationDirectory);

        string outputFileName = Path.GetFileNameWithoutExtension(input.SourcePath) + ".lua";
        string outputPath = Path.GetFullPath(Path.Combine(destinationDirectory, outputFileName));

        if (PathsEqual(input.SourcePath, outputPath))
            throw new InvalidOperationException("The output path would overwrite the original bytecode file.");

        return outputPath;
    }

    private static bool PathsEqual(string first, string second) =>
        Path.GetFullPath(first).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static void EnsureInsideOutputRoot(string root, string destination)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string normalizedDestination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)) + Path.DirectorySeparatorChar;

        if (!normalizedDestination.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Output path escaped the selected output folder.");
    }
}
