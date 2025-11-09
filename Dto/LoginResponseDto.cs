using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Dtos
{
    [SwaggerSchema("Response returned after a successful login")]
    public record LoginResponseDto
    {
        [Required]
        [SwaggerSchema("JWT access token")]
        public required string Token { get; init; }

        [SwaggerSchema("Token lifetime in seconds")]
        public int ExpiresInSeconds { get; init; }

        [Required]
        [SwaggerSchema("Identifier of the authenticated user")]
        public required string UserId { get; init; }

        [Required]
        [SwaggerSchema("Username of the authenticated user")]
        public required string UserName { get; init; }

        [Required]
        [EmailAddress]
        [SwaggerSchema("User email address")]
        public required string Email { get; init; }
    }
}
