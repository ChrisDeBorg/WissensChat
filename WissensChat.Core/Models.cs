namespace WissensChat.Core;

public sealed class AppSettings
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "llama3.1:8b";
    public string EmbeddingModel { get; set; } = "bge-m3"; // mehrsprachig, 1024 Dim.
    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WissensChat", "wissen.db");
}

public sealed record Segment(string Text, string? Location);
public sealed record NewChunk(int Index, string? Location, string Text, float[] Embedding);
public sealed record DocumentInfo(string Id, string FileName, string FilePath, int ChunkCount, DateTime AddedUtc);

public sealed record SearchHit(long ChunkId, string FileName, string? Location, string Text, float Score)
{
    public string Source => Location is null ? FileName : $"{FileName}, {Location}";
    public string Header => $"{Source} · {Score:P0}";
}

public sealed record RagAnswer(IReadOnlyList<SearchHit> Sources, IAsyncEnumerable<string> Tokens);
