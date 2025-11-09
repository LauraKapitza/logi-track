using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Dtos
{
    [SwaggerSchema("Payload used to register a new user")]
    public record RegisterDto
    {
        [Required]
        [SwaggerSchema("Unique username for the account")]
        public required string UserName { get; init; }

        [Required]
        [EmailAddress]
        [SwaggerSchema("User email address")]
        public required string Email { get; init; }

        [Required]
        [SwaggerSchema("Plain-text password (will be hashed server-side)")]
        public required string Password { get; init; }

        [Required]
        [SwaggerSchema("User given name")]
        public required string FirstName { get; init; }

        [Required]
        [SwaggerSchema("User family name")]
        public required string LastName { get; init; }
    }
}
