namespace Dtos
{
    public record LoginDto
    {
        public string? UserName { get; init; } // can be username or email
        public string? Password { get; init; }
    }
}
    