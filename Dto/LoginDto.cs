using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Dtos
{
    public record LoginDto
    {
        [Required]
        [SwaggerSchema("Username or email used to authenticate")]
        public required string UserName { get; init; }

        [Required]
        [SwaggerSchema("Plain-text password")]
        public required string Password { get; init; }
    }
}
    