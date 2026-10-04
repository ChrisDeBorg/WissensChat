using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace WissensChat.Core;

public sealed class RagService(
    ChunkStore store, IChatClient chat, IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    public async Task<List<SearchHit>> SearchAsync(string query, int top = 5, CancellationToken ct = default)
    {
        var vec = (await embedder.GenerateVectorAsync(query, cancellationToken: ct)).ToArray();
        return await Task.Run(() => store.Search(vec, top), ct);
    }

    public async Task<RagAnswer> AskAsync(IReadOnlyList<ChatMessage> history, string question, CancellationToken ct = default)
    {
        var standalone = history.Count == 0 ? question : await CondenseAsync(history, question, ct);
        var hits = await SearchAsync(standalone, 5, ct);

        var context = hits.Count == 0
            ? "(keine Quellen vorhanden)"
            : string.Join("\n\n", hits.Select((h, i) => $"[{i + 1}] ({h.Source})\n{h.Text}"));

        var system = $"""
            Du beantwortest Fragen ausschließlich anhand der folgenden Quellen. Antworte auf Deutsch.
            Nenne die verwendeten Quellen als [1], [2] usw.
            Reichen die Quellen nicht aus, sage das offen und rate nicht.

            QUELLEN:
            {context}
            """;

        var messages = new List<ChatMessage> { new(ChatRole.System, system) };
        messages.AddRange(history.TakeLast(10));
        messages.Add(new(ChatRole.User, question));
        return new RagAnswer(hits, Stream(messages, ct));
    }

    private async IAsyncEnumerable<string> Stream(List<ChatMessage> messages, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var u in chat.GetStreamingResponseAsync(messages, cancellationToken: ct))
            if (!string.IsNullOrEmpty(u.Text)) yield return u.Text;
    }

    private async Task<string> CondenseAsync(IReadOnlyList<ChatMessage> history, string question, CancellationToken ct)
    {
        var transcript = string.Join("\n", history.TakeLast(6)
            .Select(m => $"{(m.Role == ChatRole.User ? "Nutzer" : "Assistent")}: {m.Text}"));
        var prompt = $"""
            Formuliere die letzte Frage so um, dass sie ohne den Gesprächsverlauf verständlich ist.
            Antworte nur mit der umformulierten Frage.

            Verlauf:
            {transcript}

            Letzte Frage: {question}
            """;
        var r = await chat.GetResponseAsync(prompt, cancellationToken: ct);
        var t = r.Text.Trim();
        return t.Length == 0 ? question : t;
    }
}
