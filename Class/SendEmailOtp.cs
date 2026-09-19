using MailKit.Security;
using MimeKit;
using MailKit.Net.Smtp;

namespace JustFlip.Class
{
    public class SendEmailOtp
    {
        private readonly IConfiguration _configuration;

        public SendEmailOtp(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOTP(string targetEmail, string otpCode)
        {
            try
            {
                // Fallback default Values para sa Brevo Port 2525
                string smtpServer = _configuration["SmtpSettings:Server"] ?? "smtp-relay.brevo.com";
                int smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "2525");

                // Username/Login sa Brevo (b3e450001@smtp-brevo.com)
                string username = _configuration["SmtpSettings:Username"] ?? "";

                // SMTP Master Key / Password mula sa Brevo
                string password = _configuration["SmtpSettings:Password"] ?? "";

                // Sender Info (Dapat naka-register/verify sa Brevo Senders)
                string senderEmail = _configuration["SmtpSettings:SenderEmail"] ?? "";
                string senderName = _configuration["SmtpSettings:SenderName"] ?? "FitHolic Support";

                // 1. Build MimeMessage
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress("", targetEmail));
                message.Subject = "Your one-time password";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                    <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333333; max-width: 600px; margin: 0 auto; padding: 20px; line-height: 1.6;"">
                        <p style=""font-size: 16px;"">Hello,</p>
                        <p style=""font-size: 15px; margin-bottom: 25px;"">
                            To verify your identity and continue signing in to the JustFlip, 
                            please enter the following one-time password (OTP):
                        </p>
                        <p style=""font-size: 16px; margin-bottom: 10px;"">Your one-time password (OTP) is:</p>
                        <div style=""font-size: 36px; font-weight: bold; letter-spacing: 4px; color: #1a1a1a; margin-bottom: 30px; margin-top: 5px;"">
                            {otpCode}
                        </div>
                        <p style=""font-size: 14px; color: #555555; margin-bottom: 15px;"">
                            The code is valid for 5 minutes and may only be used once.
                        </p>
                        <p style=""font-size: 14px; color: #555555; margin-bottom: 15px;"">
                            For security purposes, do not share this code with anyone.
                        </p>
                        <p style=""font-size: 14px; color: #555555; margin-bottom: 35px;"">
                            If you did not request this verification, please contact your system administrator.
                        </p>
                        <p style=""font-size: 15px; margin-bottom: 5px;"">Thank you,</p>
                        <p style=""font-size: 15px; font-weight: 500; margin-top: 0;"">{senderName}</p>
                    </div>"
                };

                message.Body = bodyBuilder.ToMessageBody();

                // 2. MailKit SmtpClient Execution
                using (var client = new SmtpClient())
                {
                    // SecureSocketOptions.Auto handles STARTTLS automatically for Port 2525
                    await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.Auto);

                    // Gamitin ang Brevo Login (Username) at Brevo Key (Password)
                    await client.AuthenticateAsync(username, password);

                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BREVO SMTP ERROR]: {ex.Message}");
                throw new Exception($"Failed to send OTP Email: {ex.Message}", ex);
            }
        }
    }
}
