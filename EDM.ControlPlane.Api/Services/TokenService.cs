using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user, Session session, Guid installationId);
        (string PlaintextToken, string TokenHash) GenerateRefreshToken();
        string HashToken(string token);
        ClaimsPrincipal? ValidateAccessToken(string token, bool validateLifetime = true);
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly byte[] _signingKeyBytes;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenLifetimeMinutes;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Get or fallback to safe development key (unified with Program.cs)
            string jwtSecret = Environment.GetEnvironmentVariable("EDM_JWT_SECRET") 
                ?? _configuration["Jwt:SecretKey"] 
                ?? "EDM_Development_Super_Secret_Key_For_Jwt_Signing_2026_Minimum_256_Bits!";
            _signingKeyBytes = Encoding.UTF8.GetBytes(jwtSecret);
            _issuer = _configuration["Jwt:Issuer"] ?? "EDM.ControlPlane";
            _audience = _configuration["Jwt:Audience"] ?? "EDM.Clients";
            _accessTokenLifetimeMinutes = int.TryParse(_configuration["Jwt:AccessTokenLifetimeMinutes"], out int mins) ? mins : 15;
        }

        public string GenerateAccessToken(User user, Session session, Guid installationId)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (session == null) throw new ArgumentNullException(nameof(session));

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(_signingKeyBytes);
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("session_id", session.Id.ToString()),
                new Claim("family_id", session.FamilyId.ToString()),
                new Claim("installation_id", installationId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_accessTokenLifetimeMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = credentials
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public (string PlaintextToken, string TokenHash) GenerateRefreshToken()
        {
            byte[] randomBytes = new byte[32];
            RandomNumberGenerator.Fill(randomBytes);
            string plaintextToken = Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            string tokenHash = HashToken(plaintextToken);
            return (plaintextToken, tokenHash);
        }

        public string HashToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentNullException(nameof(token));
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            byte[] hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public ClaimsPrincipal? ValidateAccessToken(string token, bool validateLifetime = true)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_signingKeyBytes),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = validateLifetime,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
