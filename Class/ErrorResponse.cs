namespace JustFlip.Class
{
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string ErrorType { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public ErrorResponse(int statusCode, string errorType, string message)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
            Message = message;
            Timestamp = DateTime.UtcNow;
        }
    }
}
