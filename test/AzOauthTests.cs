extern alias StorageAdapterClientAlias;

namespace storageAdapterClient.Tests;

public class AzOauthTests : AzBaseTests
{
    public AzOauthTests()
    {        
        // setup tests data 
        cxTenantId = _configuration["AzOauthTest:cxTenantId"];
        storageIdentifier = _configuration["AzOauthTest:storageAcctName"];
        containerName = _configuration["AzOauthTest:containerName"];
    }

    protected override void runCanGenerateSignedUrlAsserts(bool canGenerateSasUri)
    {
        Assert.False(canGenerateSasUri);
    }

}