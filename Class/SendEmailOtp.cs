
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace JustFlip.Class
{
    public class SendEmailOtp
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public SendEmailOtp(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task SendOTP(string targetEmail, string otpCode)
        {
            try
            {
                string apiKey =
                    _configuration["SmtpSettings:ApiKey"] ?? "";

                string senderEmail =
                    _configuration["SmtpSettings:SenderEmail"] ?? "";

                string senderName =
                    _configuration["SmtpSettings:SenderName"]
                    ?? "FitHolic Support";

                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new Exception("Brevo API key is missing.");

                if (string.IsNullOrWhiteSpace(senderEmail))
                    throw new Exception("Sender email is missing.");

                // Safely encode dynamic values for HTML
                string safeOtp = HtmlEncoder.Default.Encode(otpCode);
                string safeSenderName =
                    HtmlEncoder.Default.Encode(senderName);

                var htmlContent = $@"
                <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    color: #333333; max-width: 600px; margin: 0 auto;
                    padding: 20px; line-height: 1.6;"">

                    <p style=""font-size: 16px;"">Hello,</p>

                    <p style=""font-size: 15px; margin-bottom: 25px;"">
                        To verify your identity and continue signing in
                        to JustFlip, please enter the following one-time password (OTP):
                    </p>

                    <p style=""font-size: 16px;"">
                        Your one-time password (OTP) is:
                    </p>

                    <div style=""font-size: 36px; font-weight: bold;
                        letter-spacing: 4px; color: #1a1a1a;
                        margin-bottom: 30px; margin-top: 5px;"">
                        {safeOtp}
                    </div>

                    <p style=""font-size: 14px; color: #555555;"">
                        The code is valid for 5 minutes and may only be used once.
                    </p>

                    <p style=""font-size: 14px; color: #555555;"">
                        For security purposes, do not share this code with anyone.
                    </p>

                    <p style=""font-size: 14px; color: #555555;"">
                        If you did not request this verification,
                        please contact your system administrator.
                    </p>

                    <p>Thank you,</p>
                    <p style=""font-weight: 500;"">{safeSenderName}</p>
                </div>";

                // Brevo transactional email API
                var emailPayload = new
                {
                    sender = new
                    {
                        name = senderName,
                        email = senderEmail
                    },
                    to = new[]
                    {
                        new
                        {
                            email = targetEmail
                        }
                    },
                    subject = "Your one-time password",
                    htmlContent = htmlContent
                };

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.brevo.com/v3/smtp/email"
                );

                request.Headers.Add("api-key", apiKey);
                request.Headers.Add("accept", "application/json");

                request.Content = JsonContent.Create(emailPayload);

                using var response = await _httpClient.SendAsync(request);

                string responseBody =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine(
                        $"[BREVO API ERROR] {(int)response.StatusCode}: {responseBody}"
                    );

                    throw new Exception(
                        $"Brevo API failed: {(int)response.StatusCode} - {responseBody}"
                    );
                }

                Console.WriteLine(
                    $"[BREVO API SUCCESS] OTP email accepted: {responseBody}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BREVO EMAIL ERROR]: {ex}");

                throw new Exception(
                    $"Failed to send OTP Email: {ex.Message}", ex
                );
            }
        }
    }
}
