using System;
using System.Security.Cryptography;
using System.Text;

namespace EDM.ControlPlane.Api.Services
{
    public interface ITotpService
    {
        string GenerateSecret(int byteCount = 20);
        string GenerateQrCodeUri(string email, string secret, string issuer = "EDM Control Plane");
        bool VerifyCode(string secret, string code, int toleranceSteps = 1);
        string GenerateCurrentCode(string secret);
    }

    public class TotpService : ITotpService
    {
        private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private const int StepSeconds = 30;
        private const int Digits = 6;

        public string GenerateSecret(int byteCount = 20)
        {
            byte[] bytes = new byte[byteCount];
            RandomNumberGenerator.Fill(bytes);
            return ToBase32String(bytes);
        }

        public string GenerateQrCodeUri(string email, string secret, string issuer = "EDM Control Plane")
        {
            string cleanIssuer = Uri.EscapeDataString(issuer);
            string cleanEmail = Uri.EscapeDataString(email);
            return $"otpauth://totp/{cleanIssuer}:{cleanEmail}?secret={secret}&issuer={cleanIssuer}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
        }

        public bool VerifyCode(string secret, string code, int toleranceSteps = 1)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim().Replace(" ", "").Replace("-", "");
            if (code.Length != Digits) return false;

            byte[] key;
            try
            {
                key = FromBase32String(secret);
            }
            catch
            {
                return false;
            }

            long currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

            for (int i = -toleranceSteps; i <= toleranceSteps; i++)
            {
                string expected = ComputeTotp(key, currentStep + i);
                if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(code)))
                {
                    return true;
                }
            }

            return false;
        }

        public string GenerateCurrentCode(string secret)
        {
            byte[] key = FromBase32String(secret);
            long currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
            return ComputeTotp(key, currentStep);
        }

        private static string ComputeTotp(byte[] key, long step)
        {
            byte[] stepBytes = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(stepBytes);
            }

            using var hmac = new HMACSHA1(key);
            byte[] hash = hmac.ComputeHash(stepBytes);

            int offset = hash[^1] & 0x0F;
            int binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            int otp = binaryCode % (int)Math.Pow(10, Digits);
            return otp.ToString(new string('0', Digits));
        }

        private static string ToBase32String(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;

            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0;
            int bitsLeft = 0;

            foreach (byte b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(Base32Chars[(buffer >> bitsLeft) & 31]);
                }
            }

            if (bitsLeft > 0)
            {
                buffer <<= (5 - bitsLeft);
                sb.Append(Base32Chars[buffer & 31]);
            }

            return sb.ToString();
        }

        private static byte[] FromBase32String(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

            string cleanInput = input.Trim().ToUpperInvariant().TrimEnd('=');
            var output = new List<byte>();
            int buffer = 0;
            int bitsLeft = 0;

            foreach (char c in cleanInput)
            {
                int val = Base32Chars.IndexOf(c);
                if (val < 0) throw new FormatException($"Invalid Base32 character: {c}");

                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    output.Add((byte)((buffer >> bitsLeft) & 0xFF));
                }
            }

            return output.ToArray();
        }
    }
}
