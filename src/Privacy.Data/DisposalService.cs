using System.Text.Json;
using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record LedgerEntry(string ItemType, Guid ItemId, DateTimeOffset DisposedAt);

/// <summary>
/// Copia FUORI dal database del registro delle eliminazioni (una riga JSON per elemento: solo tipo e identificativo,
/// nessun dato personale). Serve a riapplicare le cancellazioni dopo il ripristino di un backup più vecchio.
/// Il file va conservato con le copie di backup, in un luogo separato dal database.
/// </summary>
public sealed class DisposalLedger
{
    private readonly string _path;
    public DisposalLedger(string path) => _path = path;

    public IReadOnlyList<LedgerEntry> Read()
    {
        if (!File.Exists(_path)) return Array.Empty<LedgerEntry>();
        var seen = new HashSet<(string, Guid)>();
        var list = new List<LedgerEntry>();
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var e = JsonSerializer.Deserialize<LedgerEntry>(line)!;
            if (seen.Add((e.ItemType, e.ItemId))) list.Add(e);
        }
        return list;
    }

    public void Append(IEnumerable<LedgerEntry> entries)
    {
        var known = Read().Select(e => (e.ItemType, e.ItemId)).ToHashSet();
        var fresh = entries.Where(e => known.Add((e.ItemType, e.ItemId))).ToList();
        if (fresh.Count == 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var w = new StreamWriter(fs);
        foreach (var e in fresh) w.WriteLine(JsonSerializer.Serialize(e));
        w.Flush();
        fs.Flush(flushToDisk: true);   // la riga è su disco prima di tornare al chiamante
    }
}

public sealed record ReapplyResult(int Applied, int AlreadyAbsent, IReadOnlyList<(LedgerEntry Entry, string Error)> Failed);

/// <summary>Eliminazione fisica di elementi scaduti (autorizzata dal titolare) e riapplicazione dopo un ripristino.</summary>
public sealed class DisposalService
{
    private readonly Db _db;
    private readonly DisposalLedger _ledger;
    public DisposalService(Db db, DisposalLedger ledger) { _db = db; _ledger = ledger; }

    /// <summary>Elimina l'elemento; false se non c'era. Dopo il commit accoda l'elemento al registro esterno.</summary>
    public async Task<bool> DisposeAsync(Guid commandId, string itemType, Guid itemId, Guid actorId, string reason, Guid? policyVersionId = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "dispose_item", ct) is not null) return true;   // già eseguito
        var applied = (bool)(await Scalar(conn, tx, "SELECT ps.dispose_item($1,$2,$3,$4,$5)", ct, itemType, itemId, actorId, reason, (object?)policyVersionId ?? DBNull.Value))!;
        await SetResult(conn, tx, commandId, itemId, ct);
        await tx.CommitAsync(ct);
        if (applied) _ledger.Append(new[] { new LedgerEntry(itemType, itemId, DateTimeOffset.UtcNow) });
        return applied;
    }

    /// <summary>Riallinea il file esterno con il registro nel database (copre un arresto fra commit e scrittura del file).</summary>
    public async Task SyncLedgerAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT item_type, item_id, disposed_at FROM ps.disposal_items ORDER BY disposed_at", conn);
        var entries = new List<LedgerEntry>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) entries.Add(new LedgerEntry(r.GetString(0), r.GetGuid(1), r.GetFieldValue<DateTimeOffset>(2)));
        _ledger.Append(entries);
    }

    /// <summary>
    /// Da eseguire dopo ogni ripristino: rilegge il registro esterno e rielimina ciò che il backup ha riportato.
    /// Un elemento già assente è normale; gli errori sono restituiti per l'intervento umano, non nascosti.
    /// </summary>
    public async Task<ReapplyResult> ReapplyAfterRestoreAsync(Guid actorId, CancellationToken ct = default)
    {
        int applied = 0, absent = 0;
        var failed = new List<(LedgerEntry, string)>();
        foreach (var e in _ledger.Read())
        {
            try
            {
                await using var conn = await _db.OpenAsync(ct);
                await using var tx = await conn.BeginTransactionAsync(ct);
                var done = (bool)(await Scalar(conn, tx, "SELECT ps.dispose_item($1,$2,$3,$4,NULL)", ct, e.ItemType, e.ItemId, actorId, "Riapplicazione dopo ripristino da backup"))!;
                await tx.CommitAsync(ct);
                if (done) applied++; else absent++;
            }
            catch (PostgresException ex) { failed.Add((e, ex.MessageText)); }
        }
        return new ReapplyResult(applied, absent, failed);
    }
}
