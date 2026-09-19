namespace JustFlip.DTO
{
    public class ResendOtpRequest
    {
        public int UserId { get; set; }
    }

    public class ResendOtpResponse
    {
        public string Message { get; set; }
        public string EmailHint { get; set; }
        public int UserId { get; set; }
    }
}
