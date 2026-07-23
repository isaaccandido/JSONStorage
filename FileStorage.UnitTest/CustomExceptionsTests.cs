using Isaac.FileStorage.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class CustomExceptionsTests
{
    [Fact]
    public void EmptyKeyException_ConstructedWithNullMessage_FallsBackToDefault()
    {
        Assert.NotNull(new EmptyKeyException(null).Message);
    }

    [Fact]
    public void InvalidKeyException_ConstructedWithNullMessage_FallsBackToDefault()
    {
        Assert.NotNull(new InvalidKeyException(null).Message);
    }

    [Fact]
    public void LockTimeoutException_ConstructedWithNullMessage_FallsBackToDefault()
    {
        Assert.NotNull(new LockTimeoutException(null).Message);
    }

    [Fact]
    public void StorageKeyNotFoundException_ConstructedWithNullMessage_FallsBackToDefault()
    {
        Assert.NotNull(new StorageKeyNotFoundException(null).Message);
    }

    [Fact]
    public void EmptyKeyException_ConstructedWithCustomMessage_UsesIt()
    {
        Assert.Equal("custom", new EmptyKeyException("custom").Message);
    }
}
