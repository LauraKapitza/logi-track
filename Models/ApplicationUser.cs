using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        // Read-only convenience property (not mapped to DB)
        public string FullName => string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
            ? UserName ?? Email ?? string.Empty
            : $"{FirstName} {LastName}".Trim();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // for soft-delete / deactivate user
        public bool IsActive { get; set; } = true;
    }
}
