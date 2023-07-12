extern alias StorageAdapterClientModels;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using StorageAdapterClientModels::StorageAdapter.Models;

namespace storageAdapterClient.Tests;

public class AzConnStrTests : ClientTestsBase
{
    private readonly string? connectionString;

    public AzConnStrTests()
    {       
        // set expected results 
        cxTenantId = _configuration["AzConnStrTest:cxTenantId"];
        storageAcctName = _configuration["AzConnStrTest:storageAcctName"];
        containerName = _configuration["AzConnStrTest:containerName"];
    }

    [Fact]
    public override async void Authenticate()
    {

        // Get reference to the container 
        // BEFORE:
        // BlobContainerClient containerClient = new BlobContainerClient(connectionString, containerName);
        
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
        // BlobContainerClient containerClient = new BlobContainerClient(connectionString, containerName);
 
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
        // BlobContainerClient containerClient = new BlobContainerClient(connectionString, containerName);
        
        // AFTER:
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;

        // Get reference to a blob 
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        try 
        {
            // first Upload the image so it can be downloaded 
            await blobClient.UploadAsync(path, overwrite: true);

            // Download the blob's contents and save it to a file
            await blobClient.DownloadToAsync(downloadPath);

            // Verify the downloaded image
            Assert.Equal(fi.Length, File.ReadAllBytes(downloadPath).Length);    
        } 
        finally
        {
            // Clean up after the test when we're finished
            await blobClient.DeleteAsync();
        }
    }
}