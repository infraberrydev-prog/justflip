using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace JustFlip.DTO
{
    public class CreateUserDto
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsTwoFactorEnabled { get; set; } = true;
        public int BranchId { get; set; }
    }

    public class CreateUserValidator : AbstractValidator<CreateUserDto>
    {
        private readonly JustFlipDbContext _context;
        public CreateUserValidator(JustFlipDbContext context)
        {
            _context = context;

            RuleFor(x => x.Username)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode("VE001").WithMessage("Username is required.")
                .MustAsync(BeAUniqueUsername).WithErrorCode("VE002").WithMessage("Username is already registered.");

            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode("VE003").WithMessage("Email address is required.")
                .MustAsync(BeAUniqueEmail).WithErrorCode("VE004").WithMessage("Email address is already registered.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode("VE005").WithMessage("Password is required.")
                .MinimumLength(8).WithErrorCode("VE006").WithMessage("Password must be at least 8 characters long.");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithErrorCode("VE007").WithMessage("First Name is required.");

            RuleFor(x => x.LastName)
                .NotEmpty().WithErrorCode("VE008").WithMessage("Last Name is required.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithErrorCode("VE009").WithMessage("Phone number is required.");
        }

        private async Task<bool> BeAUniqueEmail(string email, CancellationToken cancellationToken)
        {
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

            return !emailExists;
        }

        private async Task<bool> BeAUniqueUsername(string username, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(username)) return true;

            var usernameExists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);

            return !usernameExists;
        }
    }
}
