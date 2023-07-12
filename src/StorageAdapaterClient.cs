using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Identity;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StorageAdapter.Models;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace StorageAdapter.Client;

#pragma warning disable 0436
public class StorageAdapterClient
{

    private HttpClient _httpClient;

    private readonly string _baseAddress;
    private readonly string _storageAdapterPath = "storageMapping/";
    private readonly IConfiguration _configuration;

    public StorageAdapterClient()
    {
        // override the appsettings.json with appsettings.{environmentName}.json if one exists    
        string environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(@"appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .Build();

        _baseAddress = _configuration["StorageAdapterApiBaseAddress"];

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_baseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // method to get storage artifacts for a given tenantId
    public async Task<StorageAdapterResponse> getTenantStorage(StorageAdapterRequest request, CancellationToken cts = default)
    {
        StorageAdapterResponse response;
        string cxTenantId = request.cxTenantId;
        
        try 
        {
            TenantToStorageMapping? tenantStorageMapping = 
                await _httpClient.GetFromJsonAsync<TenantToStorageMapping>($"{_storageAdapterPath}{cxTenantId}", cts);

            if (tenantStorageMapping == null)
            {
                throw new Exception($"TenantStorageMapping not found for tenantId: {cxTenantId}");
            }
            
            StorageType storageType = tenantStorageMapping.StorageType;

            if (storageType == StorageType.AzureStorageAccount)
            {
                // set reponse with the blobContainerClient 
                response = new StorageAdapterResponse(
                            tenantStorageMapping, 
                            buildBlobContainerClient(tenantStorageMapping));

                // TODO - or sasUri.  Note a StorageAdapterRequest object by SaSUri already exists.

            }
            else if (storageType == StorageType.AmazonS3)
            {
                StorageAdapterRequest.SignedURIAction preSignedUriAction = request.signedURIAction;
                HttpVerb httpVerb;

                switch(preSignedUriAction)
                {
                    case StorageAdapterRequest.SignedURIAction.Upload:
                        httpVerb = HttpVerb.PUT;
                        break;
                    case StorageAdapterRequest.SignedURIAction.Download:
                        httpVerb = HttpVerb.GET;
                        break;
                    default:
                        throw new Exception($"SignedURIAction not supported: {preSignedUriAction}");
                }

                // set reponse with awsS3 presignedUrl
                response = new StorageAdapterResponse(
                            tenantStorageMapping, 
                            buildS3PresignedUrl(
                                tenantStorageMapping, 
                                request.fileName,
                                httpVerb,
                                request.timeoutDurationInHrs));
            } 
            else
            {
                throw new Exception($"StorageType not supported: {storageType}");
            }
            
            return response;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
            throw e;
        }
    }

    
    private BlobContainerClient buildBlobContainerClient(TenantToStorageMapping tenantStorageMapping)
    {
        string? containerName = tenantStorageMapping.ContainerName;
        string? connectionUri = tenantStorageMapping.ConnectionUri;
        ConnectionUriType connectionUriType = tenantStorageMapping.ConnectionUriType;
        bool isAzureCrossTenant = tenantStorageMapping.IsAzureCrossTenant;
        string? azureCrossTenantId = tenantStorageMapping.AzureCrossTenantId;

        if (isAzureCrossTenant && azureCrossTenantId == null )
        {
            throw new Exception($"AzureCrossTenantId not found when IsAzureCrossTenant is true for tenantId: {tenantStorageMapping.CxTenantId}");
        }

        BlobServiceClient blobServiceClient;

        switch(connectionUriType)
        {    
            case ConnectionUriType.ConnectionString:
                blobServiceClient = new BlobServiceClient(connectionUri);
                break;
            
            case ConnectionUriType.SasUri:
                blobServiceClient = new BlobServiceClient(new Uri(connectionUri));
                break;

            case ConnectionUriType.ContainerUri:
                Uri serviceUri = new Uri(connectionUri);
                TokenCredential credential;
            
                if (isAzureCrossTenant) 
                {
                    // TODO - retrieve from tenantStorageMapping which should be set from the api via key vault 
                    string clientId = "148e6be3-b0ee-4761-98cb-1d67fafa9b0d";
                    // TODO - retrieve from tenantStorageMapping which should be set from the api via key vault 
                    string clientSecret = "aDH8Q~.UhIb6~-jyyqUPD_e29sFkBdSNDCVJZaDh";                    
                    credential = new ClientSecretCredential(azureCrossTenantId, clientId, clientSecret);
                } 
                else 
                {
                    credential = new DefaultAzureCredential();
                }
            
                blobServiceClient = new BlobServiceClient(serviceUri, credential);  
                break;
            
            default:
                throw new Exception($"ConnectionUriType not supported: {connectionUriType}");
        }


        // Create the container and return a container client object
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        return containerClient;
    } 

    private string buildS3PresignedUrl(
        TenantToStorageMapping tenantStorageMapping, 
        string objectKey,
        HttpVerb verb,
        double timeoutDurationInHrs)
    {
        string? bucketName = tenantStorageMapping.ConnectionUri;
        string? bucketRegion = tenantStorageMapping.StorageRegion;
        RegionEndpoint regionEndpoint = RegionEndpoint.GetBySystemName(tenantStorageMapping.StorageRegion);

        // TODO - store the accessKey and secret in key vault OR use temporary credentials (https://docs.aws.amazon.com/AmazonS3/latest/userguide/AuthUsingTempSessionToken.html)
        IAmazonS3 s3Client = new AmazonS3Client(
            "AKIAVYRZSLZINPYEUHJ3", 
            "s0RXFzXDT+st7qPizgjSDW/Tj/RsqYWPievcggXZ", 
            regionEndpoint);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = verb, 
            Expires = DateTime.UtcNow.AddHours(timeoutDurationInHrs),
        };

        string preSigneUrl = s3Client.GetPreSignedURL(request);

        return preSigneUrl;
    }

    private string buildAzSasUri(
        TenantToStorageMapping tenantStorageMapping
    )
    {
        // TODO - implement
        return "";
    }

}

