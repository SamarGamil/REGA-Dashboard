using AqarInsights.Api.Services;

namespace AqarInsights.Api.Tests;

public class JsonUserStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"users-{Guid.NewGuid():N}.json");

    private void Write(string json) => File.WriteAllText(_file, json);

    [Fact]
    public void Find_ReturnsAccount_ForExactUsername()
    {
        Write("""[{"username":"admin","passwordHash":"hash-1"},{"username":"a@rega.gov.sa","passwordHash":"hash-2"}]""");
        var store = new JsonUserStore(_file);

        Assert.Equal("hash-2", store.Find("a@rega.gov.sa")?.PasswordHash);
    }

    [Fact]
    public void Find_IsCaseSensitive()
    {
        Write("""[{"username":"admin","passwordHash":"hash-1"}]""");
        var store = new JsonUserStore(_file);

        Assert.Null(store.Find("Admin"));
    }

    [Fact]
    public void Find_ReturnsNull_ForUnknownUsername()
    {
        Write("""[{"username":"admin","passwordHash":"hash-1"}]""");
        var store = new JsonUserStore(_file);

        Assert.Null(store.Find("nobody"));
    }

    [Fact]
    public void Ctor_Throws_WhenFileMissing()
    {
        Assert.Throws<InvalidOperationException>(() => new JsonUserStore(_file));
    }

    [Fact]
    public void Ctor_Throws_WhenNoValidAccounts()
    {
        Write("[]");
        Assert.Throws<InvalidOperationException>(() => new JsonUserStore(_file));
    }

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }
}
