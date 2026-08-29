using System.ComponentModel;

namespace LuajitDecompilerGui.Models;

public sealed class InputFile : INotifyPropertyChanged
{
    private FileStatus _status = FileStatus.Pending;
    private string _message = string.Empty;
    private string? _outputPath;

    public string SourcePath { get; init; } = string.Empty;
    public string SourceRoot { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(SourcePath);
    public long FileSize { get; init; }
    public BytecodeDetectionResult Detection { get; init; } = new();
    public string FormatDisplay => Detection.Description;

    public FileStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (_message == value) return;
            _message = value;
            OnPropertyChanged(nameof(Message));
        }
    }

    public string? OutputPath
    {
        get => _outputPath;
        set
        {
            if (_outputPath == value) return;
            _outputPath = value;
            OnPropertyChanged(nameof(OutputPath));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
