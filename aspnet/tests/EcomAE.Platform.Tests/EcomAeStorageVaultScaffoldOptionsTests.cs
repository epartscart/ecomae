using EcomAE.Platform.Security.Scaffolding;
using EcomAE.Platform.Storage;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeStorageVaultScaffoldOptionsTests
{
    [Fact]
    public void ObjectStorageScaffoldOptionsDefaultToDisabledAndDoNotReplaceLocalPaths()
    {
        var options = new EcomAeObjectStorageScaffoldOptions();
        Assert.Equal("EcomAe:ObjectStorage", EcomAeObjectStorageScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplaceLocalFilePaths);
        Assert.Equal("minio", options.Provider);
        Assert.Equal("ecomae", options.Bucket);
    }

    [Fact]
    public void VaultScaffoldOptionsDefaultToDisabledAndDoNotReplaceEnvSecrets()
    {
        var options = new EcomAeVaultScaffoldOptions();
        Assert.Equal("EcomAe:Vault", EcomAeVaultScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplaceEnvFileSecrets);
        Assert.Equal("hashicorp-vault", options.Provider);
        Assert.Equal("secret", options.SecretsMount);
    }
}
