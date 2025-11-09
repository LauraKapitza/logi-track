namespace Dtos
{
    public record RegisterDto
    {
        public string? UserName { get; init; }
        public string? Email { get; init; }
        public string? Password { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
    }
}