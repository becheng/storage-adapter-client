extern alias StorageAdapterClientAlias;

using Azure.Storage.Blobs;
using StorageAdapterClientAlias::StorageAdapter.Client;

namespace storageAdapterClient.Tests;

public class AzSignedUrlTests : BaseSignedUrlTests
{
    private BlobClient blobClient;

    public AzSignedUrlTests()
    {        
        // setup test data
        cxTenantId = _configuration["AzAccessKeyTests:cxTenantId"];
        storageIdentifier = _configuration["AzAccessKeyTests:storageAcctName"];
        containerName = _configuration["AzAccessKeyTests:containerName"];
    }

    protected override async Task PrepTestData()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);

        // accessing th internal blobClient to prep the test data 
        blobClient = storageAdapterClient.blobContainerClient.GetBlobClient(sampleImage);
        await blobClient.UploadAsync(path, overwrite: true);
    }

    protected override async Task CleanUp()
    {
        if (blobClient == null)
        {
            StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);
            blobClient = storageAdapterClient.blobContainerClient.GetBlobClient(sampleImage);
        }
        await blobClient.DeleteAsync();
    }
}