using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Dtos;
using Models;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        // POST: /api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (model == null)
                return BadRequest(new ProblemDetails { Title = "Invalid payload", Status = 400 });

            var user = new ApplicationUser
            {
                UserName = model.UserName ?? model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                var pd = new ProblemDetails
                {
                    Title = "User creation failed",
                    Status = 400,
                    Detail = string.Join("; ", GetErrors(result))
                };
                return BadRequest(pd);
            }
            await _userManager.AddToRoleAsync(user, "User");

            return CreatedAtAction(null, null); // 201 with empty location
        }

        // POST: /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (model == null)
                return BadRequest(new ProblemDetails { Title = "Invalid payload", Status = 400 });

            var user = await _userManager.FindByNameAsync(model.UserName) ??
                       await _userManager.FindByEmailAsync(model.UserName);

            if (user == null)
                return Unauthorized(new ProblemDetails { Title = "Invalid credentials", Status = 401 });

            if (!user.IsActive)
                return Forbid();

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
            if (!signInResult.Succeeded)
                return Unauthorized(new ProblemDetails { Title = "Invalid credentials", Status = 401 });

            var token = await GenerateJwtToken(user);
            var response = new LoginResponseDto
            {
                Token = token,
                ExpiresInSeconds = int.Parse(_configuration["Jwt:ExpiresSeconds"] ?? "3600"),
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email
            };

            return Ok(response);
        }

        private static IEnumerable<string> GetErrors(IdentityResult result)
        {
            foreach (var e in result.Errors)
                yield return e.Description;
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var key = _configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("JWT Key is not configured. Set Jwt:Key in configuration.");

            var issuer = _configuration["Jwt:Issuer"] ?? "LogiTrack";
            var audience = _configuration["Jwt:Audience"] ?? "LogiTrackClients";
            var expiresSeconds = int.Parse(_configuration["Jwt:ExpiresSeconds"] ?? "3600");

            // Standard claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim("given_name", user.FirstName ?? string.Empty),
                new Claim("family_name", user.LastName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Include role claims if any
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var symmetricKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddSeconds(expiresSeconds),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}
