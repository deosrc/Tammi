using Deosrc.Tammi.Core.Ssh;

namespace Deosrc.Tammi.Core.Tests.Ssh;

public class SshKeyTests
{
    // A public key from the OpenSSH test suite, with the fingerprint ssh-keygen -l -E sha256 shows for it.
    private const string Data = "AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz";

    [Fact]
    public void TryParsePublicKey_SplitsAlgorithmDataAndComment()
    {
        Assert.True(SshKey.TryParsePublicKey($"ssh-ed25519 {Data} ada@example.com laptop\n",
            out string? algorithm, out string? data, out string? comment));

        Assert.Equal("ssh-ed25519", algorithm);
        Assert.Equal(Data, data);
        Assert.Equal("ada@example.com laptop", comment);
    }

    [Fact]
    public void TryParsePublicKey_WithoutComment()
    {
        Assert.True(SshKey.TryParsePublicKey($"ssh-ed25519 {Data}", out _, out _, out string? comment));
        Assert.Null(comment);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ssh-ed25519")]
    [InlineData("ssh-ed25519 not*base64")]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----")]
    public void TryParsePublicKey_RejectsOtherText(string? line)
    {
        Assert.False(SshKey.TryParsePublicKey(line, out _, out _, out _));
    }

    [Fact]
    public void PublicKey_JoinsTheParts()
    {
        Assert.Equal($"ssh-ed25519 {Data} me", Key("me").PublicKey);
        Assert.Equal($"ssh-ed25519 {Data}", Key(null).PublicKey);
    }

    [Fact]
    public void Matches_IgnoresTheComment()
    {
        SshKey key = Key("ada@example.com");

        Assert.True(key.Matches($"ssh-ed25519 {Data}"));
        Assert.True(key.Matches($"ssh-ed25519 {Data} other"));
        Assert.False(key.Matches($"ssh-rsa {Data}"));
        Assert.False(key.Matches("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIBBBB"));
        Assert.False(key.Matches(null));
    }

    [Fact]
    public void Fingerprint_IsTheUnpaddedBase64Sha256OfTheKeyData()
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(Data));

        Assert.Equal("SHA256:" + Convert.ToBase64String(hash).TrimEnd('='), Key(null).Fingerprint);
        Assert.DoesNotContain("=", Key(null).Fingerprint);
    }

    private static SshKey Key(string? comment) => new("id_ed25519", "/home/ada/.ssh/id_ed25519", "ssh-ed25519", Data, comment);
}
