namespace JustFlip.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public required string BranchCode { get; set; }
        public required string BranchName { get; set; }
        public string? Address { get; set; }
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
