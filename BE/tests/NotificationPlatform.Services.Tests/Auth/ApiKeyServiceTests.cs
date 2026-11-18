using System.Text.RegularExpressions;
using NotificationPlatform.Services.Auth;

namespace NotificationPlatform.Services.Tests.Auth;

public class ApiKeyServiceTests
{
    private readonly ApiKeyService _service = new();

    [Fact]
    public void Generate_KeyHasNpPrefixAndBase64UrlBody()
    {
        var key = _service.Generate().Key;

        Assert.StartsWith("np_", key);
        Assert.Equal(3 + 43, key.Length); // 32 bytes as base64url without padding = 43 chars
        Assert.Matches(new Regex("^np_[A-Za-z0-9_-]{43}$"), key);
    }

    [Fact]
    public void Generate_PrefixIsNpPlusFirstEightCharacters()
    {
        var generated = _service.Generate();

        Assert.Equal(generated.Key[..11], generated.Prefix); // "np_" + 8 chars
        Assert.Equal(11, generated.Prefix.Length);
    }

    [Fact]
    public void Hash_IsDeterministicLowercaseHexSha256()
    {
        var generated = _service.Generate();

        var hash = _service.Hash(generated.Key);

        Assert.Equal(hash, _service.Hash(generated.Key));
        Assert.Equal(generated.Hash, hash);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), hash);
    }

    [Fact]
    public void Hash_MatchesKnownSha256Value()
    {
        // SHA-256("np_test") computed independently with: printf '%s' 'np_test' | sha256sum
        Assert.Equal("74f399fc73956ee52db53cc10fd6be12addeca0b67515a0bca45b9f71e29097c", _service.Hash("np_test"));
    }

    [Fact]
    public void HasValidFormat_AcceptsGeneratedKey()
    {
        Assert.True(_service.HasValidFormat(_service.Generate().Key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("np_short")]
    [InlineData("xx_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]  // 46 chars, wrong prefix
    [InlineData("np_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 47 chars
    public void HasValidFormat_RejectsMalformedKeys(string apiKey)
    {
        Assert.False(_service.HasValidFormat(apiKey));
    }

    [Fact]
    public void Matches_TrueForOwnHash_FalseForAnotherKey()
    {
        var generated = _service.Generate();

        Assert.True(_service.Matches(generated.Key, generated.Hash));
        Assert.False(_service.Matches(_service.Generate().Key, generated.Hash));
    }

    [Fact]
    public void Generate_TwoKeysDiffer()
    {
        var first = _service.Generate();
        var second = _service.Generate();

        Assert.NotEqual(first.Key, second.Key);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}
