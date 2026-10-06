using System.Security.Cryptography;
using System.Text;

namespace API.SERVICE.Security;

/// <summary>
/// Verificación compatible con System.Web.Security.SqlMembershipProvider, para que la API valide
/// los mismos usuarios que el WebForms sin migrar contraseñas.
/// En ELRENACER: PasswordFormat = 1 (Hashed), salt de 16 bytes, hash SHA1 de 20 bytes:
/// Base64(SHA1(salt + UTF-16LE(password))).
/// </summary>
public static class MembershipPasswordHasher
{
    public const int HashedFormat = 1;

    public static bool Verify(string password, string storedHash, string storedSalt, int passwordFormat)
    {
        if (passwordFormat != HashedFormat)
            return false; // Clear (0) / Encrypted (2) no se usan en esta base: no se aceptan.

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Hash(password, storedSalt);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static byte[] Hash(string password, string base64Salt)
    {
        var salt = Convert.FromBase64String(base64Salt);
        var passwordBytes = Encoding.Unicode.GetBytes(password);

        var buffer = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, buffer, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, buffer, salt.Length, passwordBytes.Length);

        return SHA1.HashData(buffer);
    }
}
