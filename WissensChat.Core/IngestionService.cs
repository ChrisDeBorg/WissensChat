using System.Security.Cryptography;
using Microsoft.Extensions.AI;

namespace WissensChat.Core;

public sealed class IngestionService(
    ChunkStore store, IEmbeddingGenerator<string, Embedding<float>> embedder, AppSettings settings)
{
    public async Task<string> IngestAsync(string path, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var name = Path.GetFileName(path);
        string hash;
        await using (var fs = File.OpenRead(path))
            hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));

        if (store.Exists(hash, settings.EmbeddingModel)) return $"{name}: bereits importiert";

        progress?.Report($"{name}: Text wird extrahiert …");
        var pieces = await Task.Run(() => TextExtractor.Extract(path)
            .SelectMany(s => Chunker.Split(s.Text).Select(t => (Text: t, Location: s.Location)))
            .ToList(), ct);
        if (pieces.Count == 0) return $"{name}: kein Text gefunden (gescanntes PDF? OCR wird nicht unterstützt)";

        var chunks = new List<NewChunk>();
        const int batch = 16;
        for (var i = 0; i < pieces.Count; i += batch)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"{name}: Embeddings {Math.Min(i + batch, pieces.Count)}/{pieces.Count}");
            var slice = pieces.Skip(i).Take(batch).ToList();
            var emb = await embedder.GenerateAsync(slice.Select(p => p.Text).ToList(), cancellationToken: ct);
            for (var j = 0; j < slice.Count; j++)
                chunks.Add(new NewChunk(i + j, slice[j].Location, slice[j].Text, emb[j].Vector.ToArray()));
        }

        await Task.Run(() => store.Add(Guid.NewGuid().ToString(), name, path, hash, settings.EmbeddingModel, chunks), ct);
        return $"{name}: {chunks.Count} Abschnitte importiert";
    }
}
