namespace storageAdapterClient.Tests;

public class AzAccessKeyTests : AzBaseTests
{
    public AzAccessKeyTests()
    {       
        // setup test data
        cxTenantId = _configuration["AzAccessKeyTests:cxTenantId"];
        storageIdentifier = _configuration["AzAccessKeyTests:storageAcctName"];
        containerName = _configuration["AzAccessKeyTests:containerName"];
    }
    protected override void runCanGenerateSignedUrlAsserts(bool canGenerateSasUri)
    {
        Assert.True(canGenerateSasUri);
    }
}