using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JustFlip.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : Controller
    {
        private readonly JustFlipDbContext _context;
        private readonly IConfiguration _configuration;

        public UserController(JustFlipDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            try
            {
                // 1. FluentValidation Execution
                var validator = new CreateUserValidator(_context);
                var validationResult = await validator.ValidateAsync(dto);

                if (!validationResult.IsValid)
                {
                    var firstError = validationResult.Errors.First();

                    return BadRequest(new ErrorResponse(
                        statusCode: 400,
                        errorType: firstError.ErrorCode ?? "VALIDATION_ERROR",
                        message: firstError.ErrorMessage
                    ));
                }

                string secureHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

                var now = DateTime.UtcNow;
                var cleanCreatedAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);

                var newUser = new User
                {
                    Username = dto.Username,
                    Email = dto.Email,
                    PasswordHash = secureHash,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    RoleId = 1,
                    IsActive = true,
                    CreatedAt = cleanCreatedAt,
                    PhoneNumber = dto.PhoneNumber,
                    IsTwoFactorEnabled = true,
                    BranchId = dto.BranchId
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Admin user successfully created via secure API!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse(500, "DATABASE_ERROR", "An internal error occurred while saving."));
            }
        }
    }
}
