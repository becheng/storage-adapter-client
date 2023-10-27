extern alias StorageAdapterClientAlias;

using StorageAdapterClientAlias::StorageAdapter.Client;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace storageAdapterClient.Tests;

// Represents a base test class for Azure tests.
public abstract class AzBaseTests : BaseTests
{
    [Fact]
    public override async void Authenticate()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);

        // verify the response
        Assert.True(storageAdapterClient.isAuthenicated());
        Assert.Equal(containerName, storageAdapterClient.getContainerName());
        Assert.Equal(storageIdentifier, storageAdapterClient.getStorageIdentifier());
    }

    [Fact]
    public override async void Upload()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);  // using a factory method instead of a constructor                                
        
        // first Upload the image so it can be downloaded 
        await storageAdapterClient.UploadAsync(sampleImage, path);

        // verify results
        // only need the blobClient to verify results
        BlobClient blobClient = storageAdapterClient.blobContainerClient.GetBlobClient(sampleImage);
        try 
        {
            BlobProperties properties = await blobClient.GetPropertiesAsync();
            Assert.Equal(fi.Length, properties.ContentLength);    
        } 
        finally 
        {
            await blobClient.DeleteAsync();  // clean up
        }
    }

    [Fact]
    public override async void Download()
    {        
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);  // using a factory method instead of a constructor                                
        
        // first Upload the image so it can be downloaded 
        await storageAdapterClient.UploadAsync(sampleImage, path);

        // Download the blob's contents and save it to a file
        await storageAdapterClient.DownloadToAsync(sampleImage, downloadPath);

        // verify results
        // only need the blobClient to verify results
        BlobClient blobClient = storageAdapterClient.blobContainerClient.GetBlobClient(sampleImage);
        try 
        {
            // Verify the downloaded image
            Assert.Equal(fi.Length, File.ReadAllBytes(downloadPath).Length);    
        } 
        finally 
        {
            await blobClient.DeleteAsync();  // clean up
        }
    }

    [Fact]
    public override async void CanGenerateSignedUrl()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);  // using a factory method instead of a constructor                                
        bool canGenerateSasUri = storageAdapterClient.CanGenerateSignedUrl();

        // Assert.True(canGenerateSasUri);
        runCanGenerateSignedUrlAsserts(canGenerateSasUri);
    }

    protected abstract void runCanGenerateSignedUrlAsserts(bool canGenerateSasUri);
}