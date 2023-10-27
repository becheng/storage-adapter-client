namespace storageAdapterClient.Tests;

public class AzOauthCrossTenantTests : AzBaseTests
{
    public AzOauthCrossTenantTests()
    {        
        // set expected results 
        cxTenantId = _configuration["AzOauthCrossTenantTest:cxTenantId"];
        storageIdentifier = _configuration["AzOauthCrossTenantTest:storageAcctName"];
        containerName = _configuration["AzOauthCrossTenantTest:containerName"];        
    }

    protected override void runCanGenerateSignedUrlAsserts(bool canGenerateSasUri)
    {
        Assert.False(canGenerateSasUri);
    }
}