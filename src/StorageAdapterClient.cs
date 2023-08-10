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

namespace StorageAdapter.Client;

#pragma warning disable 0436
public class StorageAdapterClient
{

    private HttpClient _httpClient;

    private readonly string? _baseAddress;
    private readonly string _storageAdapterPath = "storageMapping/";
    private readonly IConfiguration _configuration;

    public StorageAdapterClient()
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

            if (storageType == StorageType.AzStorage)
            {
                response = new StorageAdapterResponse(
                            tenantStorageMapping, 
                            buildBlobContainerClient(tenantStorageMapping));

            }
            else if (storageType == StorageType.AwsS3)
            {
                response = new StorageAdapterResponse(
                            tenantStorageMapping,
                            buildS3Client(tenantStorageMapping));

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
        string? accountName = tenantStorageMapping.StorageIdentifier;
        string? storageAccessKey = tenantStorageMapping.StorageAccessKeySecretRef; // TODO - retrieve from keyvault
        string? containerName = tenantStorageMapping.ContainerName;
        ConnectionType connectionType = tenantStorageMapping.ConnectionType;
        bool isAzureCrossTenant = tenantStorageMapping.IsAzureCrossTenant;
        string? azureCrossTenantId = tenantStorageMapping.AzureCrossTenantId;
        
        if (isAzureCrossTenant && azureCrossTenantId == null )
        {
            throw new Exception($"AzureCrossTenantId not found when IsAzureCrossTenant is true for tenantId: {tenantStorageMapping.CxTenantId}");
        }

        BlobServiceClient blobServiceClient;

        switch(connectionType)
        {    
            case ConnectionType.AzConnectionString:
                blobServiceClient = new BlobServiceClient(
                    $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={storageAccessKey};EndpointSuffix=core.windows.net");  // 
                break;
            
            case ConnectionType.AzSasUri:
                blobServiceClient = new BlobServiceClient(new Uri(storageAccessKey)); // a direct SasUri unlikeily to be used 
                break;

            case ConnectionType.AzStorageSharedKey:
                StorageSharedKeyCredential storageSharedKeyCredential = new StorageSharedKeyCredential(accountName, storageAccessKey);
                blobServiceClient = new BlobServiceClient(new Uri($"https://{accountName}.blob.core.windows.net"), storageSharedKeyCredential);
                break;

            case ConnectionType.AzOauth:
                Uri serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
                TokenCredential credential;
            
                if (isAzureCrossTenant) 
                {
                    // usage 'az keyvault secret set --vault-name <kvName> --name "mttServicePrincipal--clientId" --value <secretValue>'                    
                    string? clientId = _configuration["mttServicePrincipal:clientId"];
                    string? clientSecret = _configuration["mttServicePrincipal:clientSecret"];                    
                    credential = new ClientSecretCredential(azureCrossTenantId, clientId, clientSecret);
                } 
                else 
                {
                    credential = new DefaultAzureCredential();
                }
            
                blobServiceClient = new BlobServiceClient(serviceUri, credential);  
                
                // var blobServiceClient = new BlobServiceClient(_serviceUri, new 
                // AzureSasCredential(_sasToken), new BlobClientOptions()     
                // {
                //     Transport = new HttpClientTransport(new HttpClient(new HttpClientHandler 
                //     {
                //         Proxy = new WebProxy("proxy-url:port", BypassOnLocal: true),
                //         UseProxy = true
                //     }))
                // });                
                
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
        string? secretAccessKey = tenantStorageMapping.StorageAccessKeySecretRef; // TODO - retrieve from keyvault
        string? bucketRegion = tenantStorageMapping.StorageRegion;
        RegionEndpoint regionEndpoint = RegionEndpoint.GetBySystemName(tenantStorageMapping.StorageRegion);

        IAmazonS3 s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, regionEndpoint);

        return s3Client;
    }

    
}

