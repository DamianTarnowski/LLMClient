using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LLMClient.Models;

public class RagDocument : INotifyPropertyChanged
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    private string _fileName = string.Empty;
    private string _content = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private int _chunkCount;

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    [Ignore]
    public int ChunkCount
    {
        get => _chunkCount;
        set { _chunkCount = value; OnPropertyChanged(); }
    }

    public string FileSizeDisplay => Content?.Length > 0 
        ? $"{Content.Length / 1024.0:F1} KB" 
        : "0 KB";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RagChunk
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public byte[]? Embedding { get; set; }

    public int EmbeddingVersion { get; set; }

    public string? Section { get; set; }
}
