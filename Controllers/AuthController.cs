using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using static JustFlip.DTO.LoginDto;

namespace JustFlip.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly JustFlipDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly GenerateTokenJwt _generateToken;
        private readonly SendEmailOtp _sendOTP;
        private readonly IOutputCacheStore _cacheStore;

        public AuthController(JustFlipDbContext context, IConfiguration configuration, IOutputCacheStore cacheStore)
        {
            _context = context;
            _configuration = configuration;
            _sendOTP = new SendEmailOtp(configuration);
            _generateToken = new GenerateTokenJwt(context, configuration);
            _cacheStore = cacheStore;
        }

        [HttpPost("login")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(LoginSuccessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var validator = new LoginValidator(_context);
            var validationResult = await validator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                var firstError = validationResult.Errors.First();
                return BadRequest(new ErrorResponse(
                    statusCode: 400,
                    errorType: firstError.ErrorCode,
                    message: firstError.ErrorMessage
                ));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                return Unauthorized(new ErrorResponse(401, "INVALID_CREDENTIALS", "Invalid username or password."));
            }

            if (user.IsTwoFactorEnabled)
            {
                string generatedOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

                var newOtp = new UserOTP
                {
                    UserId = user.Id,
                    OtpCode = generatedOtp,
                    ExpiryTime = DateTime.Now.AddMinutes(5),
                    IsUsed = false
                };

                _context.UserOtps.Add(newOtp);
                await _context.SaveChangesAsync();

                await _sendOTP.SendOTP(user.Email, generatedOtp);

                int atIndex = user.Email.IndexOf("@");
                string maskedEmail = atIndex > 2
                    ? string.Concat(user.Email.AsSpan(0, 2), "******", user.Email.AsSpan(atIndex))
                    : string.Concat("*", user.Email.AsSpan(atIndex));

                return Ok(new LoginSuccessResponse
                {
                    Message = "OTP sent to your registered email.",
                    EmailHint = maskedEmail,
                    UserId = user.Id
                });
            }

            var token = _generateToken.GenerateJwtToken(user);
            return Ok(new { Requires2FA = false, Token = token });
        }

        [Authorize]
        [HttpPost("logout")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Invalid session or token."));
            }

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new ErrorResponse(404, "USER_NOT_FOUND", "User record not found."));
            }

            user.RefreshToken = string.Empty;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1);

            await _context.SaveChangesAsync();

            await _cacheStore.EvictByTagAsync($"user-{userId}", default);

            return Ok(new LogoutResponse
            {
                message = "Successfully logged out!"
            });
        }

        [HttpPost("verify-otp")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(VerifyOtpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            var validator = new VerifyOtpValidator(_context);
            var validationResult = await validator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                var firstError = validationResult.Errors.First();
                return BadRequest(new ErrorResponse(
                    statusCode: 400,
                    errorType: firstError.ErrorCode,
                    message: firstError.ErrorMessage
                ));
            }

            var currentUtcTime = DateTime.Now;

            var latestOtp = await _context.UserOtps
                .Where(o => o.UserId == request.UserId && !o.IsUsed && o.ExpiryTime >= currentUtcTime)
                .OrderByDescending(o => o.ExpiryTime)
                .FirstOrDefaultAsync();

            if (latestOtp != null)
            {
                latestOtp.IsUsed = true;
            }
            else
            {
                return NotFound(new ErrorResponse(404, "INVALID_OTP", "The OTP code provided is invalid or has expired."));
            }

            var user = await _context.Users.FindAsync(request.UserId);

            if (user == null)
            {
                return NotFound(new ErrorResponse(404, "USER_NOT_FOUND", "User record not found."));
            }

            var accessToken = _generateToken.GenerateJwtToken(user);
            var refreshToken = GenerateToken.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            return Ok(new VerifyOtpResponse()
            {
                message = "Login Successful!",
                accessToken = accessToken,
                refreshToken = refreshToken
            });
        }

        [HttpPost("resend-otp")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(ResendOtpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        {
            if (request == null || request.UserId <= 0)
            {
                return BadRequest(new ErrorResponse(400, "INVALID_REQUEST", "User ID is required."));
            }

            var user = await _context.Users.FindAsync(request.UserId);

            if (user == null)
            {
                return NotFound(new ErrorResponse(404, "USER_NOT_FOUND", "User not found."));
            }

            if (!user.IsTwoFactorEnabled)
            {
                return BadRequest(new ErrorResponse(400, "2FA_NOT_ENABLED", "Two-Factor Authentication is not enabled for this user."));
            }

            var pendingOtps = await _context.UserOtps
                .Where(o => o.UserId == user.Id && !o.IsUsed)
                .ToListAsync();

            foreach (var otp in pendingOtps)
            {
                otp.IsUsed = true;
            }

            string generatedOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var newOtp = new UserOTP
            {
                UserId = user.Id,
                OtpCode = generatedOtp,
                ExpiryTime = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false
            };

            _context.UserOtps.Add(newOtp);
            await _context.SaveChangesAsync();

            await _sendOTP.SendOTP(user.Email, generatedOtp);

            int atIndex = user.Email.IndexOf("@");
            string maskedEmail = atIndex > 2
                ? string.Concat(user.Email.AsSpan(0, 2), "******", user.Email.AsSpan(atIndex))
                : string.Concat("*", user.Email.AsSpan(atIndex));

            return Ok(new ResendOtpResponse
            {
                Message = "A new OTP code has been sent to your registered email.",
                EmailHint = maskedEmail,
                UserId = user.Id
            });
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(TokenRequestDtoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] TokenRequestDto tokenDto)
        {
            if (tokenDto == null || string.IsNullOrWhiteSpace(tokenDto.RefreshToken))
            {
                return BadRequest(new ErrorResponse(400, "INVALID_REQUEST", "Invalid client request."));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == tokenDto.RefreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return Unauthorized(new ErrorResponse(401, "SESSION_EXPIRED", "Session expired. Please log in again."));
            }

            var newAccessToken = _generateToken.GenerateJwtToken(user);
            var newRefreshToken = GenerateToken.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            return Ok(new TokenRequestDtoResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }
    }
}

