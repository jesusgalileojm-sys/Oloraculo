using System.Security.Cryptography;
using System.Text;

namespace Oloraculo.Web.Helpers
{
    public class CryptoUtil
    {
        public static string GetSha256(string value)
        {
            var Bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(Bytes).ToLowerInvariant();
        }
    }
}
