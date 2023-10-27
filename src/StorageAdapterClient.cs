using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Identity;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StorageAdapter.Models;
using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Sas;

namespace StorageAdapter.Client;

#pragma warning disable 0436
public class StorageAdapterClient
{
    private HttpClient _httpClient;
    private readonly string? _baseAddress;
    private readonly string _storageAdapterPath = "storageMapping/";
    private readonly IConfiguration _configuration;

    private TenantToStorageMapping _tenantStorageMapping;
    public BlobContainerClient? blobContainerClient { get; private set; }
    public IAmazonS3? s3Client { get; private set; } 

    private readonly string _cxTenantId;

    public enum SignedUriAction
    {
        None,
        Upload,
        Download
    }

    private StorageAdapterClient(string cxTenantId)
    {
        // override the appsettings.json with appsettings.{environmentName}.json if one exists    
        string? environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(@"appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .Build();

        // to avoid the circular reference because we need the KeyVaultName from the configuration
        // we need to build the configuration again after we have the KeyVaultName from the configuration
        _configuration = new ConfigurationBuilder()
            .AddConfiguration(_configuration)
            .AddAzureKeyVault(
                new Uri($"https://{_configuration["KeyVaultName"]}.vault.azure.net/"),
                new DefaultAzureCredential())
            .Build();    

        _baseAddress = _configuration["StorageAdapterApiBaseAddress"];

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_baseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        _cxTenantId = cxTenantId;
    }

    
    private async Task initialize()
    {
        _tenantStorageMapping = 
                await _httpClient.GetFromJsonAsync<TenantToStorageMapping>($"{_storageAdapterPath}{_cxTenantId}");

        if (_tenantStorageMapping == null)
        {
            throw new Exception($"TenantStorageMapping not found for tenantId: {_cxTenantId}");
        }
            
        StorageType storageType = _tenantStorageMapping.StorageType;

        if (storageType == StorageType.AzStorage)
        {
            blobContainerClient = buildBlobContainerClient(_tenantStorageMapping); 

        }
        else if (storageType == StorageType.AwsS3)
        {
            s3Client = buildS3Client(_tenantStorageMapping);
        } 
        else
        {
            throw new Exception($"StorageType not supported: {storageType}");
        }

    }

    public static async Task<StorageAdapterClient> create(string cxTenantId){
         StorageAdapterClient storageAdapterClient = new StorageAdapterClient(cxTenantId);
         await storageAdapterClient.initialize();
         return storageAdapterClient;
    }

    public async Task UploadAsync(string blobName, string path, bool overwrite = true)
    {
        StorageType storageType = _tenantStorageMapping.StorageType;
        string? volumeName = _tenantStorageMapping.ContainerName;

        if (storageType == StorageType.AzStorage)
        {
            // Get reference to a blob 
            BlobClient blobClient = blobContainerClient!.GetBlobClient(blobName);

            // Upload data to the blob
            await blobClient.UploadAsync(path, overwrite: overwrite);
        }
        else if (storageType == StorageType.AwsS3)
        {
            // upload an image to the bucket so it can downloaded
            await s3Client.UploadObjectFromFilePathAsync(volumeName, blobName, path, null);

        } 
    }

    public async Task DownloadToAsync(string blobName, string path)
    {
        StorageType storageType = _tenantStorageMapping.StorageType;
        string? volumeName = _tenantStorageMapping.ContainerName;

        if (storageType == StorageType.AzStorage)
        {
            // Get reference to a blob 
            BlobClient blobClient = blobContainerClient!.GetBlobClient(blobName);

            await blobClient.DownloadToAsync(path);
        }
        else if (storageType == StorageType.AwsS3)
        {
            // upload an image to the bucket so it can downloaded
            await s3Client.DownloadToFilePathAsync(volumeName, blobName, path, null);
        } 

    }

    /**
    * Returns true if one of the storage specific SDK clients were instantiated, otherwise false
    */
    public bool isAuthenicated()
    {
        return blobContainerClient != null || s3Client != null;
    }

    // return a bool re if this client can generate a signed uri for the given action
    public bool CanGenerateSignedUrl()
    {
        return ( blobContainerClient != null ? blobContainerClient.CanGenerateSasUri : ( s3Client != null ? true : false ) );
    }

    public async Task<Uri> GenerateSignedUri(string blobName, SignedUriAction action)
    {
        StorageType storageType = _tenantStorageMapping.StorageType;
        string? volumeName = _tenantStorageMapping.ContainerName;
        Uri presignedUri = null;


        if (storageType == StorageType.AzStorage)
        {
            // Get reference to a blob 
            BlobClient blobClient = blobContainerClient!.GetBlobClient(blobName);

            // convert action to BlobContainerSasPermissions
            BlobContainerSasPermissions permissions = action switch
            {
                SignedUriAction.Upload => BlobContainerSasPermissions.Write,
                SignedUriAction.Download => BlobContainerSasPermissions.Read,
                _ => throw new Exception($"SignedUriAction not supported: {action}")
            };

            // generate the sasUri for the given blob, i.e. sampleImage
            presignedUri = await StorageAdapterUtil.GenerateSasUri(blobClient, permissions);
        }
        else if (storageType == StorageType.AwsS3)
        {
            // convert action to HttpVerb 
            HttpVerb httpVerb = action switch
            {
                SignedUriAction.Upload => HttpVerb.PUT,
                SignedUriAction.Download => HttpVerb.GET,
                _ => throw new Exception($"SignedUriAction not supported: {action}")
            };

            // generated presignedUrl
            presignedUri = await StorageAdapterUtil.GeneratePresignedUrl(
                                    s3Client!,
                                    volumeName, 
                                    blobName, 
                                    httpVerb);
        } 

        return presignedUri;
    }
    
    private BlobContainerClient buildBlobContainerClient(TenantToStorageMapping tenantStorageMapping)
    {
        string? accountName = tenantStorageMapping.StorageIdentifier;
        string? storageAccessKey = tenantStorageMapping.StorageAccessKeySecretRef; 
        string? storageAccessKeyVal = _configuration[storageAccessKey];
        string? containerName = tenantStorageMapping.ContainerName;
        ConnectionType connectionType = tenantStorageMapping.ConnectionType;
        bool isAzureCrossTenant = tenantStorageMapping.IsAzCrossTenant;
        string? azureCrossTenantId = tenantStorageMapping.AzCrossTenantId;
        
        if (isAzureCrossTenant && azureCrossTenantId == null )
        {
            throw new Exception($"AzureCrossTenantId not found when IsAzureCrossTenant is true for tenantId: {tenantStorageMapping.CxTenantId}");
        }

        BlobServiceClient blobServiceClient;

        switch(connectionType)
        {    
            case ConnectionType.AzStorageSharedKey:
                StorageSharedKeyCredential storageSharedKeyCredential = new StorageSharedKeyCredential(accountName, storageAccessKeyVal);
                blobServiceClient = new BlobServiceClient(new Uri($"https://{accountName}.blob.core.windows.net"), storageSharedKeyCredential);
                break;

            case ConnectionType.AzOauth:
                Uri serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
                TokenCredential credential;
            
                if (isAzureCrossTenant) 
                {
                    string? clientId = _configuration["multitenantSP:clientId"];
                    string? clientSecret = _configuration["multitenantSP:clientSecret"];                    
                    credential = new ClientSecretCredential(azureCrossTenantId, clientId, clientSecret);
                } 
                else 
                {
                    credential = new DefaultAzureCredential();
                }
            
                blobServiceClient = new BlobServiceClient(serviceUri, credential);  
                                
                break;
            
            default:
                throw new Exception($"ConnectionType not supported: {connectionType}");


        }


        // return a blob container client for the given container
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        return containerClient;
    } 

    private IAmazonS3 buildS3Client(TenantToStorageMapping tenantStorageMapping){
        string? accessKeyId = tenantStorageMapping.StorageIdentifier; 
        string? secretAccessKey = tenantStorageMapping.StorageAccessKeySecretRef;
        string? storageAccessKeyVal = _configuration[secretAccessKey]; 
        string? bucketRegion = tenantStorageMapping.StorageRegion;
        RegionEndpoint regionEndpoint = RegionEndpoint.GetBySystemName(bucketRegion);

        IAmazonS3 s3Client = new AmazonS3Client(accessKeyId, storageAccessKeyVal, regionEndpoint);

        return s3Client;
    }

    public string getStorageIdentifier()
    {
        return _tenantStorageMapping.StorageIdentifier;
    }
    
    public string getContainerName()
    {
        return _tenantStorageMapping.ContainerName;
    }
}

