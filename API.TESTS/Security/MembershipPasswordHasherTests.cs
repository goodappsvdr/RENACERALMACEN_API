using API.SERVICE.Security;
using FluentAssertions;

namespace API.TESTS.Security;

public class MembershipPasswordHasherTests
{
    // Vector calculado de forma independiente con .NET Framework (Windows PowerShell 5.1):
    // Base64(SHA1(salt + UTF-16LE("Secreto123"))) con salt = bytes 0..15.
    private const string Salt = "AAECAwQFBgcICQoLDA0ODw==";
    private const string Hash = "JQWLd73fvTZXfnT4t8BV9yxk1Rw=";

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        MembershipPasswordHasher.Verify("Secreto123", Hash, Salt, MembershipPasswordHasher.HashedFormat).Should().BeTrue();
    }

    [Theory]
    [InlineData("secreto123")]
    [InlineData("Secreto1234")]
    [InlineData("")]
    public void Verify_WrongPassword_ReturnsFalse(string password)
    {
        MembershipPasswordHasher.Verify(password, Hash, Salt, MembershipPasswordHasher.HashedFormat).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)] // Clear
    [InlineData(2)] // Encrypted
    public void Verify_NonHashedFormat_ReturnsFalse(int format)
    {
        MembershipPasswordHasher.Verify("Secreto123", Hash, Salt, format).Should().BeFalse();
    }

    [Fact]
    public void Verify_CorruptStoredHash_ReturnsFalse()
    {
        MembershipPasswordHasher.Verify("Secreto123", "no-es-base64!", Salt, MembershipPasswordHasher.HashedFormat).Should().BeFalse();
    }
}
