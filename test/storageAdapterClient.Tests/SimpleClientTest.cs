extern alias StorageAdapterClientModels;

using StorageAdapterClientModels::StorageAdapter.Models;

namespace storageAdapterClient.Tests;

public class SimpleClientTest : ClientTestsBase
{

    [Fact]
    public async void initClient()
    {
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest("tenantId"));

        Assert.Null(response);
    }
}