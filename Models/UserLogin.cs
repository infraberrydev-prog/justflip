namespace JustFlip.Models
{
    public partial class UserLogin
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateTime? LoginTime { get; set; }

        public string? IpAddress { get; set; }

        public bool IsSuccess { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
