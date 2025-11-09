namespace Dtos
{
    public record LoginResponseDto
    {
        public string? Token { get; init; }
        public int ExpiresInSeconds { get; init; }
        public string? UserId { get; init; }
        public string? UserName { get; init; }
        public string? Email { get; init; }
    }
}