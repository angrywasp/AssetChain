using AngryWasp.Cryptography;
using System.Text;

namespace Common
{
    public static class PasswordExtensions
    {
        public static byte[] Empty = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public static byte[] ToAesKey(this string value)
        {
            byte[] plainBytes = string.IsNullOrEmpty(value) ? Empty : Encoding.ASCII.GetBytes(value);
            return Keccak.Hash128(plainBytes);
        }
    }
}