extern alias StorageAdapterClientModels;

using StorageAdapterClientModels::StorageAdapter.Models;

namespace storageAdapterClient.Tests;

public class SimpleClientTest : ClientTestsBase
{

    [Fact]
    public async void initClient()
    {
        try 
        {
            StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest("tenantId"));
        } 
        catch (Exception e) 
        {
            // 1. expect Exception if the adapter api is not running 
            // 2. expect Exception since not mapping will be match for the fake "tenantId" cxTenantId value
            // Console.WriteLine(e.Message);
            // Assert.NotNull(e.Message);
            Assert.True(e.Message.Contains("Connection refused") || e.Message.Contains("TenantStorageMapping not found for tenantId"));
        }
    }

    public override void Authenticate()
    {
        throw new NotImplementedException();
    }

    public override Task Download()
    {
        throw new NotImplementedException();
    }

    public override Task Upload()
    {
        throw new NotImplementedException();
    }
}