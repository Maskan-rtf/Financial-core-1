using BuildingBlocks.Application.Results;
using Core.Application.Identity.DTOs.User;

namespace Core.Application.Identity.Interfaces;

public interface IUserService
{
    Task<ApiOperationResult<SendOtpResultDto>> SendOtpAsync(SendOtpDto dto);
    Task<ApiOperationResult<CaptchaChallengeDto>> CreateCaptchaAsync();
    Task<ApiOperationResult<LoginDto>> VerifyOtpAsync(VerifyOtpDto dto);
    Task<ApiOperationResult<LoginDto>> LoginWithPasswordAsync(LoginWithPasswordDto dto);
    Task<ApiOperationResult<UserDto>> SetPasswordAsync(SetPasswordDto dto, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> AdminSetPasswordAsync(Guid id, AdminSetPasswordDto dto, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<LoginDto>> RefreshTokenAsync(RefreshTokenDto dto, string? accessToken);
    Task<ApiOperationResult<UserDto>> LogoutCurrentSessionAsync(CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> RevokeSessionAsync(RevokeSessionDto dto, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> RevokeAllSessionsAsync(CancellationToken cancellationToken = default);
    Task<ApiOperationResult<SessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken = default);
    Task<ApiOperationResult<OnlineUserDto>> GetOnlineUsersAsync(CancellationToken cancellationToken = default);
    Task<ApiOperationResult<SessionDto>> GetUserActiveSessionsAsAdminAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> AdminRevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> AdminRevokeSessionAsync(Guid userId, RevokeSessionDto dto, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> KickUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> GetByIdAsync(Guid id);
    Task<ApiOperationResult<UserDto>> GetPagedAsync(int take, int skip);
    Task<ApiOperationResult<UserDto>> CreateAsync(CreateUserDto dto);
    Task<ApiOperationResult<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiOperationResult<UserDto>> GetProfileAsync(CancellationToken cancellationToken = default);
}
