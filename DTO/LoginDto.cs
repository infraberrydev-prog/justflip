using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace JustFlip.DTO
{
    public class LoginDto
    {
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginSuccessResponse
        {
            public string Message { get; set; }
            public string EmailHint { get; set; }
            public int UserId { get; set; }
        }

        public class LoginValidator : AbstractValidator<LoginRequest>
        {
            private readonly JustFlipDbContext _context;

            public LoginValidator(JustFlipDbContext context)
            {
                _context = context;

                RuleFor(x => x.Username)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty().WithErrorCode("VE001").WithMessage("Username is required.");

                RuleFor(x => x.Password)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty().WithErrorCode("VE003").WithMessage("Password is required.")
                    .MinimumLength(8).WithErrorCode("VE004").WithMessage("Password must be at least 8 characters long.");

                RuleFor(x => x)
                    .Cascade(CascadeMode.Stop)
                    .MustAsync(VerifyCredentials)
                    .WithErrorCode("VE005")
                    .WithMessage("Incorrect username or password.");
            }

            private async Task<bool> VerifyCredentials(LoginRequest model, CancellationToken cancellationToken)
            {
                if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password) || model.Password.Length < 8)
                {
                    return false;
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username, cancellationToken);

                if (user == null)
                {
                    return false;
                }

                bool isValidPassword = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);

                return isValidPassword;
            }
        }
    }
}
