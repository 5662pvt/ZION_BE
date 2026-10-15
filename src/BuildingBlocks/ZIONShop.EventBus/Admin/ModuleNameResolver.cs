namespace ZIONShop.EventBus.Admin;

/// <summary>Derives the admin-facing module name from a module's DbContext type (e.g. "OrdersDbContext" ->
/// "Orders"), shared by every outbox/inbox admin store so the name reported to the admin "Hệ thống" screens
/// is consistent regardless of which module registered it.</summary>
public static class ModuleNameResolver
{
    private const string DbContextSuffix = "DbContext";

    public static string FromDbContext(Type dbContextType)
    {
        var name = dbContextType.Name;
        return name.EndsWith(DbContextSuffix, StringComparison.Ordinal)
            ? name[..^DbContextSuffix.Length]
            : name;
    }
}
