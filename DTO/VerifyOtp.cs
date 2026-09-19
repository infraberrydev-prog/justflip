using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace JustFlip.DTO
{
    public class VerifyOtpRequest
    {
        public int UserId { get; set; }
        public string OtpCode { get; set; } = string.Empty;
    }

    public class VerifyOtpResponse
    {
        public string message { get; set; }
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
    }

    public class VerifyOtpValidator : AbstractValidator<VerifyOtpRequest>
    {
        private readonly JustFlipDbContext _context;
        public VerifyOtpValidator(JustFlipDbContext context)
        {
            _context = context;

            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode("VE001").WithMessage("UserId is required.")
                .MustAsync(haveLatestOtp).WithErrorCode("VE002").WithMessage("Security context invalid. Please log in again.");

            RuleFor(x => x.OtpCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode("VE003").WithMessage("One-time PIN is required.")
                .MustAsync(isMatch).WithErrorCode("VE004").WithMessage("The 6-digit code is incorrect or expired. Please try again.")
                .MustAsync(isOtpExpired).WithErrorCode("VE005").WithMessage("This code has expired. Please request a new one.");
        }

        private async Task<bool> isMatch(string enteredOtp, CancellationToken cancellationToken)
        {
            var isMatch = await _context.UserOtps
                .AnyAsync(u => u.OtpCode == enteredOtp, cancellationToken);

            return isMatch;
        }

        private async Task<bool> isOtpExpired(string enteredOtp, CancellationToken cancellationToken)
        {
            var isExpired = await _context.UserOtps
                .AnyAsync(u => u.ExpiryTime < DateTime.UtcNow, cancellationToken);

            return isExpired;
        }

        private async Task<bool> haveLatestOtp(int userID, CancellationToken cancellationToken)
        {
            var haveLatestOtp = await _context.UserOtps
                .Where(o => o.UserId == userID && !o.IsUsed)
                .OrderByDescending(o => o.ExpiryTime)
                .FirstOrDefaultAsync(cancellationToken);

            return haveLatestOtp != null;
        }
    }
}
