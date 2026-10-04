using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace WissensChat.Core;

/// SQLite-Speicher mit Brute-Force-Cosine-Suche. Für einige 10.000 Abschnitte völlig ausreichend.
/// Später austauschbar gegen Microsoft.Extensions.VectorData + sqlite-vec.
public sealed class ChunkStore(AppSettings settings)
{
    private SqliteConnection Open()
    {
        var c = new SqliteConnection($"Data Source={settings.DatabasePath}");
        c.Open();
        return c;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.DatabasePath)!);
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS documents(
              id TEXT PRIMARY KEY, file_name TEXT NOT NULL, file_path TEXT NOT NULL,
              hash TEXT NOT NULL, embed_model TEXT NOT NULL, chunk_count INTEGER NOT NULL, added_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS chunks(
              id INTEGER PRIMARY KEY AUTOINCREMENT, document_id TEXT NOT NULL, idx INTEGER NOT NULL,
              location TEXT, text TEXT NOT NULL, embedding BLOB NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_chunks_doc ON chunks(document_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public bool Exists(string hash, string model)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM documents WHERE hash=$h AND embed_model=$m";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$m", model);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public void Add(string id, string fileName, string filePath, string hash, string model, IReadOnlyList<NewChunk> chunks)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var old in ScalarList(c, "SELECT id FROM documents WHERE hash=$h", ("$h", hash)))
            DeleteInTx(c, old);

        Run(c, "INSERT INTO documents VALUES($id,$n,$p,$h,$m,$cnt,$t)",
            ("$id", id), ("$n", fileName), ("$p", filePath), ("$h", hash), ("$m", model),
            ("$cnt", chunks.Count), ("$t", DateTime.UtcNow.ToString("o")));

        foreach (var ch in chunks)
            Run(c, "INSERT INTO chunks(document_id,idx,location,text,embedding) VALUES($d,$i,$l,$t,$e)",
                ("$d", id), ("$i", ch.Index), ("$l", ch.Location), ("$t", ch.Text),
                ("$e", MemoryMarshal.AsBytes(ch.Embedding.AsSpan()).ToArray()));
        tx.Commit();
    }

    public List<DocumentInfo> List()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,file_name,file_path,chunk_count,added_utc FROM documents ORDER BY added_utc DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<DocumentInfo>();
        while (r.Read())
            list.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        return list;
    }

    public void Delete(string id)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        DeleteInTx(c, id);
        tx.Commit();
    }

    public List<SearchHit> Search(float[] query, int top)
    {
        var scored = new List<(long Id, float Score)>();
        using var c = Open();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT id, embedding FROM chunks";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var vec = MemoryMarshal.Cast<byte, float>(r.GetFieldValue<byte[]>(1));
                if (vec.Length != query.Length) continue; // anderes Embedding-Modell
                scored.Add((r.GetInt64(0), Cosine(query, vec)));
            }
        }

        var hits = new List<SearchHit>();
        foreach (var (id, score) in scored.OrderByDescending(s => s.Score).Take(top))
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT d.file_name, c.location, c.text FROM chunks c JOIN documents d ON d.id=c.document_id WHERE c.id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
                hits.Add(new(id, r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2), score));
        }
        return hits;
    }

    private static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb) + 1e-9f);
    }

    private static void DeleteInTx(SqliteConnection c, string id)
    {
        Run(c, "DELETE FROM chunks WHERE document_id=$id", ("$id", id));
        Run(c, "DELETE FROM documents WHERE id=$id", ("$id", id));
    }

    private static void Run(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static List<string> ScalarList(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        var l = new List<string>();
        while (r.Read()) l.Add(r.GetString(0));
        return l;
    }
}
