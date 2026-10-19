namespace PowerShellSessionRecorder.Core;

public interface ICustomerCatalog
{
    CustomerCatalog Load();

    CustomerEditResult Add(string tag, string? displayName, DateTimeOffset createdUtc);

    CustomerEditResult Update(string id, string tag, string? displayName);

    CustomerEditResult Remove(string id);
}
