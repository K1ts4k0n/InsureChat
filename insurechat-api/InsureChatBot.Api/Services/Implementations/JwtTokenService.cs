using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InsureChatBot.Api.Services.Implementations
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _settings;

        public JwtTokenService(IOptions<JwtSettings> options)
        {
            _settings = options.Value;
        }

        public string CreateToken(User user)
        {
            // claim names ตรง ๆ (Program ปิด inbound claim mapping ไว้) เพื่ออ่านได้ตามชื่อ
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username ?? ""),
                new Claim("role", user.Role ?? UserRoles.Customer),
                new Claim("name", user.Name ?? user.Username ?? ""),
                new Claim("email", user.Email ?? ""),
                new Claim("custCode", user.CustCode ?? user.Username ?? "")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
