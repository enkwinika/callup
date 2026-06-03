using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace CallUp.Services
{
    public class AdumoHelper
    {
        public static string GenerateJwt(Dictionary<string, object> payload, string secret)
        {
            var header = new { alg = "HS256", typ = "JWT" };
            
            string headerBase64 = Base64UrlEncode(JsonConvert.SerializeObject(header));
            string payloadBase64 = Base64UrlEncode(JsonConvert.SerializeObject(payload));
            
            string stringToSign = $"{headerBase64}.{payloadBase64}";
            string signature = SignHmacSha256(stringToSign, secret);
            
            return $"{stringToSign}.{signature}";
        }

        private static string Base64UrlEncode(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            string base64 = Convert.ToBase64String(bytes);
            return base64.Split('=')[0].Replace('+', '-').Replace('/', '_');
        }

        private static string SignHmacSha256(string input, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes).Split('=')[0].Replace('+', '-').Replace('/', '_');
            }
        }
    }
}
