using System.Data;

namespace JustFlip.Models
{
    public class User
    {
        public int Id { get; set; }
        public int BranchId { get; set; }

        public string Username { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public int RoleId { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;

        public bool IsTwoFactorEnabled { get; set; } = true;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiryTime { get; set; }

        public virtual Role Role { get; set; } = null!;

        public virtual ICollection<UserLogin> UserLogins { get; set; } = new List<UserLogin>();

        public ICollection<UserOTP> UserOTPs { get; set; } = new List<UserOTP>();

        public Branch? Branch { get; set; }
    }
}
