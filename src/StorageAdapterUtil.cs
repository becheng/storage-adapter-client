using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace StorageAdapter.Client;

public static class StorageAdapterUtil
{

    public static async Task<Uri> GenerateSasUri (
        BlobClient blobClient,
        BlobContainerSasPermissions sasPermission = BlobContainerSasPermissions.Read,
        double timeoutDurationInHrs = 1) 
    {
        return await GenerateSasUri(blobClient, null, sasPermission, timeoutDurationInHrs);
    }

    public static async Task<Uri> GenerateSasUri(
        BlobClient blobClient,
        string storedPolicyName) 
    {
        return await GenerateSasUri(blobClient, storedPolicyName);
    }

    public static async Task<Uri> GeneratePresignedUrl(
        IAmazonS3 s3Client,
        string bucketName,
        string objectKey,
        HttpVerb verb,
        double timeoutDurationInHrs = 1)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = verb, 
            Expires = DateTime.UtcNow.AddHours(timeoutDurationInHrs),
        };

        string preSigneUrl = s3Client.GetPreSignedURL(request);

        return new Uri(preSigneUrl);
    }

    private static async Task<Uri> GenerateSasUri(
        BlobClient blobClient,
        string storedPolicyName = null,
        BlobContainerSasPermissions sasPermission = BlobContainerSasPermissions.Read,
        double timeoutDurationInHrs = 1 )
    {
        // Check if BlobContainerClient object has been authorized with Shared Key
        if (blobClient.CanGenerateSasUri)
        {
            // Create a SAS token that's valid for one hr
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                BlobName = blobClient.Name,
                Resource = "b"
            };

            if (storedPolicyName == null)
            {
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(timeoutDurationInHrs);
                sasBuilder.SetPermissions(sasPermission);
            }
            else
            {
                sasBuilder.Identifier = storedPolicyName;
            }

            Uri sasURI = blobClient.GenerateSasUri(sasBuilder);

            return sasURI;
        }
        else
        {
            // Client object is not authorized via Shared Key
            return null;
        }
    }
}