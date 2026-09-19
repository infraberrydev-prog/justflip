namespace JustFlip.Models
{
    public class UserOTP
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string OtpCode { get; set; } = string.Empty; public DateTime ExpiryTime { get; set; }
        public bool IsUsed { get; set; } = false;
        public User User { get; set; } = null!;
    }
}
