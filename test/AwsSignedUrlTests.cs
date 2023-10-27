extern alias StorageAdapterClientAlias;

using Amazon.S3;
using StorageAdapterClientAlias::StorageAdapter.Client;

namespace storageAdapterClient.Tests;

public class AwsSignedUrlTests : BaseSignedUrlTests
{
    private IAmazonS3 s3Client;

    public AwsSignedUrlTests()
    {   
        // to set up the tests
        // prep test data   
        cxTenantId = _configuration["AwsS3Test:cxTenantId"];
        containerName = _configuration["AwsS3Test:AwsBucketName"];
        storageIdentifier = _configuration["AwsS3Test:awsAccessKeyId"];        
    }

    protected override async Task PrepTestData()
    {
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);

        // accessing th internal s3Cleint to prep the test data 
        s3Client = storageAdapterClient.s3Client;
        await s3Client.UploadObjectFromFilePathAsync(containerName, sampleImage, path, null);
    }

    protected override async Task CleanUp()
    {
        if (s3Client == null)
        {
            StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);
            s3Client = storageAdapterClient.s3Client;
        }
        await s3Client.DeleteObjectAsync(containerName, sampleImage);
    }
}