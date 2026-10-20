namespace CoreKit.Access.Persistence;

internal interface IAccessAuditRepository
{
    void Add(AccessAuditEntry entry);
}

internal sealed class AccessAuditRepository : IAccessAuditRepository
{
    private readonly AccessDbContext _db;

    public AccessAuditRepository(AccessDbContext db) => _db = db;

    public void Add(AccessAuditEntry entry) => _db.AuditEntries.Add(entry);
}