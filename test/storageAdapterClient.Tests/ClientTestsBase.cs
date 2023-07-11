extern alias StorageAdapterClientModels;

using StorageAdapterClientModels::StorageAdapter.Client;

namespace storageAdapterClient.Tests;
public abstract class ClientTestsBase
{
    protected readonly StorageAdapterClient _storageAdapterClient;

    public ClientTestsBase()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        _storageAdapterClient = new StorageAdapterClient();
    }
}