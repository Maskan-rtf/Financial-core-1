using System.Text.Json.Serialization;
using Core.Application.Identity.Serialization;
using Core.Application.Identity.Tokens;
using Core.Domain.Identity;


namespace Core.Application.Identity.DTOs.User;

public class SendOtpDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? CaptchaId { get; set; }
    public string? CaptchaCode { get; set; }
    public string? ResendToken { get; set; }
}

public class SendOtpResultDto
{
    public string ResendToken { get; set; } = string.Empty;
}

public class CaptchaChallengeDto
{
    public string CaptchaId { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
}

public class LoginWithPasswordDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? CaptchaId { get; set; }
    public string? CaptchaCode { get; set; }
}

public class SetPasswordDto
{
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? CurrentPassword { get; set; }
}

public class AdminSetPasswordDto
{
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyOtpDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? NationalCode { get; set; }
    public UserRole Role { get; set; }
    public int RoleNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool HasPassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class LoginDto
{
    public required GeneratedTokenModel TokenModel { get; set; }
    public required UserDto User { get; set; }
}

public class CreateUserDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? NationalCode { get; set; }
    public string? Password { get; set; }
}

public class UpdateUserDto
{
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? NationalCode { get; set; }
    [JsonConverter(typeof(NullableUserRoleJsonConverter))]
    public UserRole? Role { get; set; }
    public int? RoleNumber { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsPhoneVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class RegisterDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NationalCode { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Applicant;
}