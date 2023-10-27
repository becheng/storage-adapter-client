extern alias StorageAdapterClientAlias;

using StorageAdapterClientAlias::StorageAdapter.Client;
using Amazon.S3;
using Amazon.S3.Model;

namespace storageAdapterClient.Tests;

public class AwsTests : BaseTests
{
    public AwsTests()
    {       
        // prep test data   
        cxTenantId = _configuration["AwsS3Test:cxTenantId"];
        containerName = _configuration["AwsS3Test:AwsBucketName"];
        storageIdentifier = _configuration["AwsS3Test:awsAccessKeyId"];
    }

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
    public override async void Download()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId); // using a factory method instead of a constructor                

        // first Upload the image so it can be downloaded 
        await storageAdapterClient.UploadAsync(sampleImage, path);

        // Download the blob's contents and save it to a file
        await storageAdapterClient.DownloadToAsync(sampleImage, downloadPath);

        // verify results
        // only need the s3Client to verify results
        IAmazonS3 s3Client = storageAdapterClient.s3Client;
        try 
        {
            // Verify the downloaded image
            Assert.Equal(fi.Length, File.ReadAllBytes(downloadPath).Length);    
        }
        finally
        {
            // clean up after the test when we're finished
            await s3Client.DeleteObjectAsync(containerName, sampleImage);
        }
    }

    [Fact]
    public override async void Upload()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId); // using a factory method instead of a constructor                
        await storageAdapterClient.UploadAsync(sampleImage, path);

        // verify results
        IAmazonS3 s3Client = storageAdapterClient.s3Client;
        try 
        {
            GetObjectResponse objResp = await s3Client.GetObjectAsync(containerName, sampleImage);
            Assert.Equal(fi.Length, objResp.ContentLength);    
        }
        finally
        {
            // clean up after the test when we're finished
            await s3Client.DeleteObjectAsync(containerName, sampleImage);
        }
    }

    [Fact]
    public override async void CanGenerateSignedUrl()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId); // using a factory method instead of a constructor                
        bool canGenerateSasUri = storageAdapterClient.CanGenerateSignedUrl();

        Assert.True(canGenerateSasUri);
    }
}


