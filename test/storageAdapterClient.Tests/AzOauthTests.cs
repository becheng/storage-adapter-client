extern alias StorageAdapterClientModels;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using StorageAdapterClientModels::StorageAdapter.Models;

namespace storageAdapterClient.Tests;

public class AzOauthTests : ClientTestsBase
{
    public AzOauthTests()
    {        
        // set expected results 
        cxTenantId = _configuration["AzOauthTest:cxTenantId"];
        storageAcctName = _configuration["AzOauthTest:storageAcctName"];
        containerName = _configuration["AzOauthTest:containerName"];
    }

    [Fact]
    public override async void Authenticate()
    {
        // Get reference to the container
        // BEFORE:
        // BlobContainerClient containerClient = new BlobContainerClient(new Uri(blobContainerUri), tokenCredential);

        // AFTER:
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;
        
        // Verify its the container exists and it's the correct one
        Assert.Equal(storageAcctName, containerClient?.AccountName);
        Assert.Equal(containerName, containerClient?.Name );
    }

    [Fact]
    public override async Task Upload()
    {
        // Get reference to the container
        // BEFORE:
        // BlobContainerClient containerClient = new BlobContainerClient(new Uri(blobContainerUri), tokenCredential);

        // AFTER:
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;
        
        // Get reference to a blob 
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        try 
        {
            // Upload data to the blob
            await blobClient.UploadAsync(path, overwrite: true);

            // Verify the uploaded image
            BlobProperties properties = await blobClient.GetPropertiesAsync();
            Assert.Equal(fi.Length, properties.ContentLength);    

        } 
        finally
        {
            // Clean up after the test when we're finished
            await blobClient.DeleteAsync();
        }
    }

    [Fact]
    public override async Task Download()
    {
        // Get reference to the container
        // BEFORE:
        // BlobContainerClient containerClient = new BlobContainerClient(new Uri(blobContainerUri), tokenCredential);

        // AFTER:
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;
        
        // Get reference to a blob 
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        try 
        {
            // Upload data to the blob
            await blobClient.UploadAsync(path, overwrite: true);

            // Download the blob
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            // Verify the downloaded image
            Assert.Equal(fi.Length, download.ContentLength);
        } 
        finally
        {
            // Clean up after the test when we're finished
            await blobClient.DeleteAsync();
        }
    }

    [Fact]
    public async void CanGenerateSasUri()
    {
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;
        
        // create a blobclient
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        bool canGenerateSasUri = blobClient.CanGenerateSasUri;

        Assert.False(canGenerateSasUri);
    }
}