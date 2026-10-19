namespace PowerShellSessionRecorder.Core;

public enum CustomerCatalogKind
{
    Missing,
    Loaded,
    Quarantined,
    Unreadable,
    UnsupportedSchema
}

public sealed class CustomerCatalogDocument
{
    public int SchemaVersion { get; set; } = JsonCustomerCatalog.CurrentSchemaVersion;

    public List<CustomerRecord> Customers { get; set; } = [];
}

public sealed class CustomerCatalog
{
    private CustomerCatalog(CustomerCatalogKind kind, IReadOnlyList<CustomerRecord> customers)
    {
        Kind = kind;
        Customers = customers;
    }

    public CustomerCatalogKind Kind { get; }

    public IReadOnlyList<CustomerRecord> Customers { get; }

    public static CustomerCatalog Missing() => new(CustomerCatalogKind.Missing, []);

    public static CustomerCatalog Loaded(IReadOnlyList<CustomerRecord> customers)
        => new(CustomerCatalogKind.Loaded, customers);

    public static CustomerCatalog Quarantined() => new(CustomerCatalogKind.Quarantined, []);

    public static CustomerCatalog Unreadable() => new(CustomerCatalogKind.Unreadable, []);

    public static CustomerCatalog UnsupportedSchema() => new(CustomerCatalogKind.UnsupportedSchema, []);
}

public enum CustomerEditError
{
    None,
    Required,
    Invalid,
    Duplicate,
    DisplayNameInvalid,
    Missing,
    CatalogUnavailable
}

public readonly record struct CustomerEditResult(CustomerEditError Error, CustomerRecord? Customer)
{
    public static CustomerEditResult Ok(CustomerRecord customer) => new(CustomerEditError.None, customer);

    public static CustomerEditResult Fail(CustomerEditError error) => new(error, null);
}
