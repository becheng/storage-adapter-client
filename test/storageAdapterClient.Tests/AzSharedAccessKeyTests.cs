extern alias StorageAdapterClientModels;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using StorageAdapterClientModels::StorageAdapter.Models;
using StorageAdapterClientModels::StorageAdapter.Client;
using Azure.Storage.Sas;

namespace storageAdapterClient.Tests;

public class AzSharedAccessKeyTests : ClientTestsBase
{
    private readonly string? connectionString;

    public AzSharedAccessKeyTests()
    {       
        // set expected results 
        cxTenantId = _configuration["AzSharedAccessKeyTest:cxTenantId"];
        storageAcctName = _configuration["AzSharedAccessKeyTest:storageAcctName"];
        containerName = _configuration["AzSharedAccessKeyTest:containerName"];
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

    [Fact]
    public async void CanGenerateSasUri()
    {
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;
        
        // create a blobclient
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        bool canGenerateSasUri = blobClient.CanGenerateSasUri;

        Assert.True(canGenerateSasUri);
    }

    [Fact]
    public async void UploadViaGeneratedSasUri()
    {
        // AFTER:
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;
        
        // Get reference to a blob 
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        try 
        {   
            // generate the sasUri for the given blob, i.e. sampleImage
            Uri blobSASURI = await StorageAdapterUtil.GenerateSasUri(blobClient, BlobContainerSasPermissions.Create, 1);

            // Create a blob client object representing 'sample-blob.txt' with SAS authorization
            BlobClient blobClientSAS = new BlobClient(blobSASURI);

            // Upload data to the blob
            await blobClientSAS.UploadAsync(path, overwrite: true);

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
    public async void DownloadViaGeneratedSasUri()
    {
        // AFTER:
        StorageAdapterResponse response = await _storageAdapterClient.getTenantStorage(new StorageAdapterRequest(cxTenantId));
        BlobContainerClient containerClient = response.blobContainerClient;

        // Get reference to a blob 
        BlobClient blobClient = containerClient.GetBlobClient(sampleImage);

        // first Upload the image so it can be downloaded 
        await blobClient.UploadAsync(path, overwrite: true);

        try 
        {
            // generate the sasUri for the given blob, i.e. sampleImage
            Uri blobSASURI = await StorageAdapterUtil.GenerateSasUri(blobClient, BlobContainerSasPermissions.Read, 1);

            // Create a blob client object representing 'sample-blob.txt' with SAS authorization
            BlobClient blobClientSAS = new BlobClient(blobSASURI);

            // Download the blob's contents and save it to a file
            await blobClientSAS.DownloadToAsync(downloadPath);

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