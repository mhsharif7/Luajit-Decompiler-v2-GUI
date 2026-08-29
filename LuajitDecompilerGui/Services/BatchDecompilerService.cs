using LuajitDecompilerGui.Models;

namespace LuajitDecompilerGui.Services;

public sealed class BatchDecompilerService
{
    private readonly DecompilerService _decompiler;

    public BatchDecompilerService(DecompilerService decompiler)
    {
        _decompiler = decompiler;
    }

    public async Task RunAsync(
        IReadOnlyList<InputFile> files,
        string outputRoot,
        bool preserveDirectoryStructure,
        DecompileOptions options,
        IProgress<BatchProgress>? progress = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        List<InputFile> candidates = files
            .Where(x => x.Status is FileStatus.Ready or FileStatus.Pending or FileStatus.Failed or FileStatus.Cancelled)
            .ToList();

        int total = candidates.Count;
        int completed = 0;

        foreach (InputFile file in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                MarkRemainingCancelled(candidates, file);
                break;
            }

            if (!file.Detection.IsSupportedBytecode)
            {
                file.Status = FileStatus.Unsupported;
                file.Message = file.Detection.Description;
                completed++;
                continue;
            }

            if (!options.MatchesExtension(file.SourcePath))
            {
                file.Status = FileStatus.Skipped;
                file.Message = "Extension filter";
                completed++;
                progress?.Report(new BatchProgress(completed, total, file, file.Message));
                continue;
            }

            file.Status = FileStatus.Processing;
            file.Message = "Decompiling...";
            progress?.Report(new BatchProgress(completed, total, file, "Processing"));

            DecompileResult result = await _decompiler.DecompileAsync(
                file,
                outputRoot,
                preserveDirectoryStructure,
                options,
                log,
                cancellationToken);

            if (result.Cancelled)
            {
                file.Status = FileStatus.Cancelled;
                file.Message = result.Message;
                MarkRemainingCancelled(candidates, file);
                break;
            }

            if (result.Skipped)
                file.Status = FileStatus.Skipped;
            else if (result.Success)
            {
                file.Status = FileStatus.Success;
                file.OutputPath = result.OutputPath;
            }
            else
                file.Status = FileStatus.Failed;

            file.Message = result.Message;
            completed++;
            progress?.Report(new BatchProgress(completed, total, file, result.Message));
        }
    }

    private static void MarkRemainingCancelled(IEnumerable<InputFile> files, InputFile current)
    {
        bool foundCurrent = false;

        foreach (InputFile file in files)
        {
            if (ReferenceEquals(file, current))
            {
                foundCurrent = true;
                continue;
            }

            if (!foundCurrent) continue;

            if (file.Status is FileStatus.Ready or FileStatus.Pending)
            {
                file.Status = FileStatus.Cancelled;
                file.Message = "Batch cancelled.";
            }
        }
    }
}
