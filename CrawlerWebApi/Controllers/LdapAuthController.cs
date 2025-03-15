using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CrawlerWebApi.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class LdapAuthController : ControllerBase
    {
        private readonly ILdapAuthService _ldapAuthService;
        private readonly IConfiguration _configuration;

        public LdapAuthController(ILdapAuthService ldapAuthService, IConfiguration configuration)
        {
            _ldapAuthService = ldapAuthService;
            _configuration = configuration;
        }
        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Username and password are required.");
            }

            // let's auth and get the user
            LDAPUserInfo _LDAPUser = _ldapAuthService.AuthenticateAndGetUser(request.Username, request.Password);
            bool isValidUser = (_LDAPUser != null);

            if (!isValidUser)
            {
                return Unauthorized("Invalid username or password");
            }

            // Generate JWT token
            var token = GenerateJwtToken(request.Username);

            // return token and user
            return Ok(new {
                token,
                userInfo = _LDAPUser
            });
        }

        private string GenerateJwtToken(string username)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", "User") // You can add roles/claims as needed
        };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiresInMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
