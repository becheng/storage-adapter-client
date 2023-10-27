extern alias StorageAdapterClientAlias;

using System.Net;
using StorageAdapterClientAlias::StorageAdapter.Client;

namespace storageAdapterClient.Tests;

public abstract class BaseSignedUrlTests : BaseTests
{
    private readonly HttpClient httpClient;

    public BaseSignedUrlTests()
    {
        httpClient = new HttpClient();
    }
    
    [Fact]
    public override async void Authenticate()
    {
        // AFTER:
        StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);

        // verify the response
        Assert.True(storageAdapterClient.isAuthenicated());
        Assert.Equal(containerName, storageAdapterClient.getContainerName());
        Assert.Equal(storageIdentifier, storageAdapterClient.getStorageIdentifier());
    }

    [Fact]
    public override async void Upload()
    {
        try 
        {            
            StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);
            Uri? preSignedUrl = await storageAdapterClient.GenerateSignedUri(sampleImage, StorageAdapterClient.SignedUriAction.Upload);
            
            // add the required headers to the httpclient request (only applicable to Azure, will be ignored by AWS)
            httpClient.DefaultRequestHeaders.Add("x-ms-date", DateTime.UtcNow.ToString("R"));
            httpClient.DefaultRequestHeaders.Add("x-ms-blob-type", "BlockBlob");

            // send the request / upload the file
            HttpResponseMessage response = await httpClient.PutAsync(preSignedUrl, new ByteArrayContent(File.ReadAllBytes(path)));

            // verify the response
            Assert.True(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
        }
        finally
        {
            // clean up the image
            await CleanUp();
        }
    }

    [Fact]
    public override async void Download()
    {
        // upload the image first
        await PrepTestData();

        try
        {
            StorageAdapterClient storageAdapterClient = await StorageAdapterClient.create(cxTenantId);    
            Uri? preSignedUrl = await storageAdapterClient.GenerateSignedUri(sampleImage, StorageAdapterClient.SignedUriAction.Download);

            // send the request / recieved the download stream 
            using (Stream respStream = await httpClient.GetStreamAsync(preSignedUrl))
            {
                var buffer = new byte[8000];
                using (FileStream fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead = 0;
                    while ((bytesRead = respStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                    }
                }
            }

            // Verify the downloaded image
            Assert.Equal(fi.Length, File.ReadAllBytes(downloadPath).Length);
        }
        finally
        {
            // cleanup the image
            await CleanUp();
        }                
    }

    [Fact]
    public override void CanGenerateSignedUrl()
    {
        Assert.True(true); // not applicable so always assert true
    }

    protected abstract Task PrepTestData();
    protected abstract Task CleanUp();
}