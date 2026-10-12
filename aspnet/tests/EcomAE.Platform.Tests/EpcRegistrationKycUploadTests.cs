using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EpcRegistrationKycUploadTests
{
    [Fact]
    public async Task StoreKycUploadFallsBackToAppDataWhenWebRootIsNotWritable()
    {
        var data = Directory.CreateTempSubdirectory("kyc-data-").FullName;
        var previous = Environment.GetEnvironmentVariable("ECOMAE_APP_DATA");
        Environment.SetEnvironmentVariable("ECOMAE_APP_DATA", data);
        var webRoot = Path.GetTempFileName();
        try
        {
            var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            var publicPath = await EpcRegistrationEnhanced.StoreKycUploadAsync(
                webRoot,
                42,
                "epc_doc_trade_licence",
                new EpcRegistrationUpload("licence.pdf", bytes),
                CancellationToken.None);
            Assert.StartsWith("/content/files/kyc/42/", publicPath, StringComparison.Ordinal);
            Assert.EndsWith(".pdf", publicPath, StringComparison.Ordinal);

            var file = publicPath[(publicPath.LastIndexOf('/') + 1)..];
            var resolved = EpcRegistrationEnhanced.ResolveKycFile(webRoot, "42", file);
            Assert.NotNull(resolved);
            Assert.StartsWith(Path.GetFullPath(data), Path.GetFullPath(resolved!), StringComparison.Ordinal);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(resolved!));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ECOMAE_APP_DATA", previous);
            try { File.Delete(webRoot); } catch (IOException) { }
            try { Directory.Delete(data, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task StoreKycUploadWritesWebRootWhenItIsWritable()
    {
        var webRoot = Directory.CreateTempSubdirectory("kyc-web-").FullName;
        var data = Directory.CreateTempSubdirectory("kyc-data-").FullName;
        var previous = Environment.GetEnvironmentVariable("ECOMAE_APP_DATA");
        Environment.SetEnvironmentVariable("ECOMAE_APP_DATA", data);
        try
        {
            var publicPath = await EpcRegistrationEnhanced.StoreKycUploadAsync(
                webRoot,
                7,
                "epc_doc_emirates_id",
                new EpcRegistrationUpload("id.png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
                CancellationToken.None);
            var file = publicPath[(publicPath.LastIndexOf('/') + 1)..];
            var onWeb = Path.Combine(webRoot, "content", "files", "kyc", "7", file);
            Assert.True(File.Exists(onWeb));
            Assert.False(Directory.Exists(Path.Combine(data, "kyc", "7")));
            Assert.Equal(onWeb, EpcRegistrationEnhanced.ResolveKycFile(webRoot, "7", file));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ECOMAE_APP_DATA", previous);
            try { Directory.Delete(webRoot, true); } catch (IOException) { }
            try { Directory.Delete(data, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void ResolveKycFileRejectsTraversal()
    {
        Assert.Null(EpcRegistrationEnhanced.ResolveKycFile("/tmp", "1", "../secret.pdf"));
        Assert.Null(EpcRegistrationEnhanced.ResolveKycFile("/tmp", "1", "a/b.pdf"));
        Assert.Null(EpcRegistrationEnhanced.ResolveKycFile("/tmp", "", "licence.pdf"));
    }
}
