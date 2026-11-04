using Npgsql;

namespace Privacy.Data;

public sealed record CatalogItem(string Code, string Label);

/// <summary>Lettura dei cataloghi chiusi (cat_*) per le scelte dell'interfaccia: solo voci abilitate, nome tabella da elenco fisso.</summary>
public sealed class CatalogService
{
    private static readonly HashSet<string> Allowed = new()
    {
        "cat_right", "cat_request_channel", "cat_requester_capacity", "cat_request_outcome",
        "cat_incident_classification", "cat_incident_action_type", "cat_risk_level",
        "cat_notification_decision", "cat_subject_comm_decision", "cat_data_category",
        "cat_delivery_channel", "cat_receipt_status",
        "cat_record_class", "cat_retention_trigger", "cat_duration_kind", "cat_article6", "cat_article9", "cat_consent_assessment", "cat_subject_category", "cat_recipient_category"
    };
    private readonly Db _db;
    public CatalogService(Db db) => _db = db;

    public async Task<IReadOnlyList<CatalogItem>> GetAsync(string table, CancellationToken ct = default)
    {
        if (!Allowed.Contains(table)) throw new ArgumentException("catalogo non ammesso", nameof(table));
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"SELECT code, label FROM ps.{table} WHERE enabled ORDER BY label", conn);
        var list = new List<CatalogItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new CatalogItem(r.GetString(0), r.GetString(1)));
        return list;
    }
}
