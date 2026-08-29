using LuajitDecompilerGui.Models;

namespace LuajitDecompilerGui.Services;

public sealed class FileScanner
{
    private readonly LuaJitBytecodeDetector _detector;

    private sealed record ScanCandidate(string FilePath, string SourceRoot);

    public FileScanner(LuaJitBytecodeDetector detector)
    {
        _detector = detector;
    }

    public Task<List<InputFile>> ScanAsync(
        IEnumerable<string> paths,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(paths, progress, cancellationToken), cancellationToken);

    private List<InputFile> Scan(
        IEnumerable<string> paths,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<ScanCandidate> candidates = [];
        HashSet<string> knownFiles = new(StringComparer.OrdinalIgnoreCase);

        progress?.Report(new ScanProgress(ScanPhase.Discovering, 0, 0));

        foreach (string input in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(input);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                AddCandidate(
                    candidates,
                    knownFiles,
                    fullPath,
                    Path.GetDirectoryName(fullPath) ?? string.Empty,
                    progress);

                continue;
            }

            if (Directory.Exists(fullPath))
            {
                DiscoverDirectory(
                    candidates,
                    knownFiles,
                    fullPath,
                    progress,
                    cancellationToken);
            }
        }

        int total = candidates.Count;
        progress?.Report(new ScanProgress(ScanPhase.Analyzing, 0, total));

        List<InputFile> results = new(total);

        for (int index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScanCandidate candidate = candidates[index];
            InputFile? item = AnalyzeFile(candidate, cancellationToken);

            if (item != null)
                results.Add(item);

            int current = index + 1;

            // Keep the UI responsive without posting thousands of updates.
            if (current == total || current == 1 || current % 10 == 0)
            {
                progress?.Report(new ScanProgress(
                    ScanPhase.Analyzing,
                    current,
                    total,
                    candidate.FilePath));
            }
        }

        return results
            .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void DiscoverDirectory(
        List<ScanCandidate> candidates,
        HashSet<string> knownFiles,
        string directory,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        string sourceRoot = Directory.GetParent(directory)?.FullName ?? directory;

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddCandidate(candidates, knownFiles, file, sourceRoot, progress);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignore inaccessible/problem directories and continue with what was found.
        }
    }

    private static void AddCandidate(
        List<ScanCandidate> candidates,
        HashSet<string> knownFiles,
        string filePath,
        string sourceRoot,
        IProgress<ScanProgress>? progress)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return;
        }

        if (!knownFiles.Add(fullPath))
            return;

        candidates.Add(new ScanCandidate(fullPath, sourceRoot));

        int count = candidates.Count;
        if (count == 1 || count % 25 == 0)
        {
            progress?.Report(new ScanProgress(
                ScanPhase.Discovering,
                count,
                0,
                fullPath));
        }
    }

    private InputFile? AnalyzeFile(
        ScanCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BytecodeDetectionResult detection = _detector.Detect(candidate.FilePath);

        long fileSize = 0;
        try
        {
            fileSize = new FileInfo(candidate.FilePath).Length;
        }
        catch
        {
            // Detection/status carries any read problem.
        }

        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(candidate.SourceRoot, candidate.FilePath);
        }
        catch
        {
            relativePath = Path.GetFileName(candidate.FilePath);
        }

        FileStatus status = detection.Format switch
        {
            FileFormat.LuaJit20Bytecode => FileStatus.Ready,
            FileFormat.LuaJit21Bytecode => FileStatus.Ready,
            FileFormat.Unreadable => FileStatus.Failed,
            _ => FileStatus.Unsupported
        };

        return new InputFile
        {
            SourcePath = candidate.FilePath,
            SourceRoot = candidate.SourceRoot,
            RelativePath = relativePath,
            FileSize = fileSize,
            Detection = detection,
            Status = status
        };
    }
}
