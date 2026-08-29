using System.Diagnostics;
using LuajitDecompilerGui.Models;

namespace LuajitDecompilerGui.Services;

public sealed class DecompilerService
{
    private readonly string _decompilerPath;
    private readonly OutputPathService _outputPathService;

    public DecompilerService(OutputPathService outputPathService)
    {
        _outputPathService = outputPathService;
        _decompilerPath = Path.Combine(AppContext.BaseDirectory, "third_party", "luajit-decompiler-v2.exe");
    }

    public bool IsDecompilerAvailable => File.Exists(_decompilerPath);
    public string DecompilerPath => _decompilerPath;

    public async Task<DecompileResult> DecompileAsync(
        InputFile input,
        string outputRoot,
        bool preserveDirectoryStructure,
        DecompileOptions options,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch timer = Stopwatch.StartNew();
        string? temporaryDirectory = null;

        try
        {
            if (!File.Exists(_decompilerPath))
                return Failure($"Decompiler executable not found: {_decompilerPath}", timer);

            if (!File.Exists(input.SourcePath))
                return Failure("Input file no longer exists.", timer);

            if (!input.Detection.IsSupportedBytecode)
            {
                return new DecompileResult
                {
                    Success = false,
                    Skipped = true,
                    Message = $"Unsupported format: {input.Detection.Description}",
                    Duration = timer.Elapsed
                };
            }

            if (!options.MatchesExtension(input.SourcePath))
            {
                return new DecompileResult
                {
                    Success = false,
                    Skipped = true,
                    Message = "Skipped by extension filter.",
                    Duration = timer.Elapsed
                };
            }

            string finalOutputPath = _outputPathService.GetOutputPath(input, outputRoot, preserveDirectoryStructure);
            string? finalDirectory = Path.GetDirectoryName(finalOutputPath);

            if (string.IsNullOrWhiteSpace(finalDirectory))
                return Failure("Unable to determine output directory.", timer);

            if (File.Exists(finalOutputPath) && !options.ForceOverwrite)
            {
                return new DecompileResult
                {
                    Success = false,
                    Skipped = true,
                    OutputPath = finalOutputPath,
                    Message = "Output already exists. Enable -f to overwrite.",
                    Duration = timer.Elapsed
                };
            }

            Directory.CreateDirectory(finalDirectory);
            temporaryDirectory = CreateTemporaryDirectory();

            string generatedFile = Path.Combine(
                temporaryDirectory,
                Path.GetFileNameWithoutExtension(input.SourcePath) + ".lua");

            log?.Invoke($"Decompiling: {input.SourcePath}");
            log?.Invoke($"Format: {input.Detection.Description}");

            ProcessStartInfo startInfo = new()
            {
                FileName = _decompilerPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_decompilerPath) ?? AppContext.BaseDirectory
            };

            startInfo.ArgumentList.Add(input.SourcePath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(temporaryDirectory);
            options.AddArguments(startInfo);

            log?.Invoke("Starting luajit-decompiler-v2.exe");

            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
                return Failure("Failed to start the decompiler process.", timer);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                timer.Stop();
                log?.Invoke($"Cancelled: {input.FileName}");

                return new DecompileResult
                {
                    Success = false,
                    Cancelled = true,
                    Message = "Cancelled.",
                    Duration = timer.Elapsed
                };
            }

            int exitCode = process.ExitCode;

            if (!File.Exists(generatedFile))
            {
                timer.Stop();
                string message = $"Decompiler produced no output file. Exit code: {exitCode}.";
                log?.Invoke(message);

                return new DecompileResult
                {
                    Success = false,
                    ExitCode = exitCode,
                    Message = message,
                    Duration = timer.Elapsed
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(finalOutputPath))
            {
                if (!options.ForceOverwrite)
                {
                    return new DecompileResult
                    {
                        Success = false,
                        Skipped = true,
                        ExitCode = exitCode,
                        OutputPath = finalOutputPath,
                        Message = "Output already exists.",
                        Duration = timer.Elapsed
                    };
                }

                File.Delete(finalOutputPath);
            }

            File.Move(generatedFile, finalOutputPath);
            timer.Stop();
            log?.Invoke($"Success: {finalOutputPath}");

            return new DecompileResult
            {
                Success = true,
                ExitCode = exitCode,
                OutputPath = finalOutputPath,
                Message = "Successfully decompiled.",
                Duration = timer.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            timer.Stop();
            return new DecompileResult
            {
                Success = false,
                Cancelled = true,
                Message = "Cancelled.",
                Duration = timer.Elapsed
            };
        }
        catch (Exception ex)
        {
            timer.Stop();
            log?.Invoke($"ERROR: {ex.Message}");
            return new DecompileResult
            {
                Success = false,
                Message = ex.Message,
                Duration = timer.Elapsed
            };
        }
        finally
        {
            if (temporaryDirectory != null)
                TryDeleteDirectory(temporaryDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LuajitDecompilerGui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch { }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch { }
    }

    private static DecompileResult Failure(string message, Stopwatch timer)
    {
        timer.Stop();
        return new DecompileResult
        {
            Success = false,
            Message = message,
            Duration = timer.Elapsed
        };
    }
}
