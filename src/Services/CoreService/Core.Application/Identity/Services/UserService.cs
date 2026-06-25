using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Mapster;
using BuildingBlocks.Application.Results;
using Core.Application.Identity.DTOs.User;
using Core.Application.Identity.Notifications;
using Core.Application.Abstractions;
using Core.Application.Identity.Abstractions;
using Core.Application.Identity.Interfaces;
using Core.Application.Identity.Common.Interfaces;
using Microsoft.Extensions.Options;
using Core.Application.Identity.Authorization;
using Core.Application.Identity.Services.Authorization;
using Core.Application.Identity.Common;
using Core.Application.Identity.Common.Options;
using Core.Application.Logging;
using Core.Domain.Identity.Entities;
using Core.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Core.Application.Identity.Services;

public class UserService(
    ITokenHelper tokenHelper,
    ICoreUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    INotificationService notificationService,
    IOtpCacheService otpCacheService,
    ISessionCacheService sessionCacheService,
    ICurrentRequestContext requestContext,
    IRefreshTokenService refreshTokenService,
    IValidator<CreateUserDto> createUserValidator,
    IValidator<UpdateUserDto> updateUserValidator,
    IValidator<SendOtpDto> sendOtpValidator,
    IValidator<VerifyOtpDto> verifyOtpValidator,
    IOptions<OtpOptions> otpOptions,
    IOptions<AuthSessionOptions> sessionOptions,
    IAuthorizationService authorizationService,
    IPermissionCacheService permissionCacheService,
    ILogger<UserService> logger) : IUserService
{
    #region Authentication & Sessions

    public async Task<ApiOperationResult<UserDto>> SendOtpAsync(SendOtpDto dto)
    {
        var result = new ApiOperationResult<UserDto>();
        ApplicationLog.Started(logger, "SendOtp");

        var validation = await sendOtpValidator.ValidateAsync(dto, CancellationToken.None);
        if (!validation.IsValid)
        {
            ApplicationLog.Blocked(logger, "SendOtp", "validation failed");
            return result.Failed(IdentityMessages.ValidationFailed, validation.Errors.Select(e => e.ErrorMessage).ToList(), HttpStatusCode.BadRequest);
        }

        var user = await unitOfWork.Users.GetAsync(u => u.PhoneNumber == dto.PhoneNumber, disableTracking: false);
        if (user is null)
        {
            ApplicationLog.Blocked(logger, "SendOtp", "no user registered for this phone number");
            return result.Failed(IdentityMessages.UserNotFoundByPhone, HttpStatusCode.NotFound);
        }

        var decision = await otpCacheService.CanRequestOtpAsync(dto.PhoneNumber, CancellationToken.None);
        if (!decision.Allowed)
        {
            ApplicationLog.Blocked(logger, "SendOtp", "rate limit exceeded", user.Id.ToString());
            return result.Failed(IdentityMessages.OtpRateLimited, HttpStatusCode.BadRequest);
        }

        var otpCode = otpOptions.Value.DevBypassEnabled && !string.IsNullOrWhiteSpace(otpOptions.Value.DevCode)
            ? otpOptions.Value.DevCode
            : RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        var validTime = DateTime.UtcNow.AddMinutes(Math.Max(1, otpOptions.Value.TtlMinutes));

        await otpCacheService.StoreOtpAsync(dto.PhoneNumber, otpCode, CancellationToken.None);
        await notificationService.SendOtpNotificationAsync(dto.PhoneNumber, otpCode, validTime, CancellationToken.None);

        await unitOfWork.Users.UpdateAsync(user);
        await unitOfWork.SaveChangesAsync();

        ApplicationLog.Completed(logger,
            "OTP sent to user {UserId} at phone {PhoneNumber}",
            user.Id, dto.PhoneNumber);

        return result.Succeed(IdentityMessages.OtpSent);
    }

    public async Task<ApiOperationResult<LoginDto>> VerifyOtpAsync(VerifyOtpDto dto)
    {
        var result = new ApiOperationResult<LoginDto>();
        ApplicationLog.Started(logger, "VerifyOtp");

        var validationResult = await verifyOtpValidator.ValidateAsync(dto, CancellationToken.None);
        if (!validationResult.IsValid)
        {
            ApplicationLog.Blocked(logger, "VerifyOtp", "validation failed");
            return result.Failed(IdentityMessages.ValidationFailed, validationResult.Errors.Select(error => error.ErrorMessage).ToList(), HttpStatusCode.BadRequest);
        }

        var user = await unitOfWork.Users.GetAsync(u => u.PhoneNumber == dto.PhoneNumber, disableTracking: false);
        if (user is null)
        {
            ApplicationLog.Blocked(logger, "VerifyOtp", "no user registered for this phone number");
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);
        }

        var otpValidation = await otpCacheService.ValidateOtpAsync(dto.PhoneNumber, dto.OtpCode, CancellationToken.None);
        if (!otpValidation.Success)
        {
            var reason = otpValidation.Locked ? "OTP locked" : otpValidation.Expired ? "OTP expired" : "invalid OTP code";
            ApplicationLog.Blocked(logger, "VerifyOtp", reason, user.Id.ToString());
            return BuildOtpFailure(result, otpValidation);
        }

        if (otpOptions.Value.DevBypassEnabled
            && !string.IsNullOrWhiteSpace(otpOptions.Value.SeedAdminPhone)
            && string.Equals(dto.PhoneNumber, otpOptions.Value.SeedAdminPhone, StringComparison.Ordinal)
            && user.Role != UserRole.Admin)
        {
            user.Role = UserRole.Admin;
            await permissionCacheService.RemoveUserPermissionsAsync(user.Id, CancellationToken.None);
        }

        var sessionId = Guid.NewGuid();
        var tokenData = tokenHelper.GenerateToken(
            user.Id.ToString(),
            user.PhoneNumber,
            RoleClaimMapper.ToClaimRole(user.Role),
            sessionId.ToString("N"));

        await TrimActiveSessionsForLimitAsync(user.Id, CancellationToken.None);

        await sessionCacheService.StoreSessionAsync(CreateSessionDescriptor(user.Id, sessionId), CancellationToken.None);

        user.IsPhoneVerified = true;
        user.LastLoginAt = DateTime.UtcNow;

        var refreshToken = refreshTokenService.CreateNew(
            userId: user.Id,
            sessionId: sessionId,
            familyId: Guid.NewGuid(),
            parentTokenId: null,
            rawRefreshToken: tokenData.RefreshToken,
            expiresAt: tokenData.RefreshTokenExpiration,
            requestContext: requestContext);

        await unitOfWork.RefreshTokens.AddAsync(refreshToken);
        await unitOfWork.UserSessions.AddAsync(CreateUserSession(user.Id, sessionId, refreshToken.Id));
        await unitOfWork.Users.UpdateAsync(user);
        await unitOfWork.SaveChangesAsync();

        ApplicationLog.Completed(logger,
            "User {UserId} logged in via OTP â€” session {SessionId}, role {Role}",
            user.Id, sessionId, user.Role);

        var response = new LoginDto
        {
            TokenModel = tokenData,
            User = user.Adapt<UserDto>()
        };

        return result.Succeed(IdentityMessages.LoginSucceeded, response);
    }

    public async Task<ApiOperationResult<LoginDto>> RefreshTokenAsync(RefreshTokenDto dto, string? accessToken)
    {
        var result = new ApiOperationResult<LoginDto>();
        ApplicationLog.Started(logger, "RefreshToken");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ApplicationLog.Blocked(logger, "RefreshToken", "access token is missing");
            return result.Failed(IdentityMessages.InvalidAccessToken, HttpStatusCode.BadRequest);
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            ApplicationLog.Blocked(logger, "RefreshToken", "refresh token is missing");
            return result.Failed(IdentityMessages.RefreshTokenRequired, HttpStatusCode.BadRequest);
        }

        var payload = tokenHelper.ValidateAccessToken(accessToken);
        if (payload.UserId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "RefreshToken", "access token is invalid");
            return result.Failed(IdentityMessages.InvalidAccessToken, HttpStatusCode.BadRequest);
        }

        var user = await unitOfWork.Users.GetAsync(u => u.Id == payload.UserId, disableTracking: false);
        if (user is null)
        {
            ApplicationLog.Blocked(logger, "RefreshToken", "user not found", payload.UserId.ToString());
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);
        }

        var presentedHash = refreshTokenService.Hash(dto.RefreshToken);
        var stored = await unitOfWork.RefreshTokens.GetByTokenHashAsync(presentedHash, disableTracking: false);
        if (stored is null || stored.UserId != user.Id)
        {
            ApplicationLog.Blocked(logger, "RefreshToken", "refresh token is invalid", user.Id.ToString());
            return result.Failed(IdentityMessages.InvalidRefreshToken, HttpStatusCode.BadRequest);
        }

        var now = DateTime.UtcNow;
        if (stored.RevokedAt is not null)
        {
            await InvalidateFamilyAsync(user.Id, stored.FamilyId, "reuse_detected", CancellationToken.None);

            logger.LogCritical(
                "Security audit: refresh token reuse detected for user {UserId} â€” family {FamilyId}, session {SessionId} revoked",
                user.Id, stored.FamilyId, stored.SessionId);

            return result.Failed(IdentityMessages.InvalidRefreshToken, HttpStatusCode.BadRequest);
        }

        if (stored.ExpiresAt <= now)
        {
            ApplicationLog.Blocked(logger, "RefreshToken", "refresh token expired", user.Id.ToString());
            return result.Failed(IdentityMessages.RefreshTokenExpired, HttpStatusCode.BadRequest);
        }

        var sessionId = stored.SessionId;

        if (stored.FamilyId == Guid.Empty)
        {
            stored.FamilyId = Guid.NewGuid();
            await unitOfWork.RefreshTokens.UpdateAsync(stored);
        }

        var tokenData = tokenHelper.GenerateToken(
            user.Id.ToString(),
            user.PhoneNumber,
            RoleClaimMapper.ToClaimRole(user.Role),
            sessionId.ToString("N"));

        var newRefresh = refreshTokenService.CreateNew(
            userId: user.Id,
            sessionId: sessionId,
            familyId: stored.FamilyId,
            parentTokenId: stored.Id,
            rawRefreshToken: tokenData.RefreshToken,
            expiresAt: tokenData.RefreshTokenExpiration,
            requestContext: requestContext);

        refreshTokenService.Revoke(stored, replacedByTokenId: newRefresh.Id, reason: "rotated", revokedByIp: requestContext.IpAddress);

        user.LastLoginAt = DateTime.UtcNow;

        await unitOfWork.RefreshTokens.AddAsync(newRefresh);
        await unitOfWork.RefreshTokens.UpdateAsync(stored);
        await unitOfWork.Users.UpdateAsync(user);

        var dbSession = await unitOfWork.UserSessions.GetBySessionIdAsync(sessionId, disableTracking: false);
        if (dbSession is not null && dbSession.RevokedAt is null)
        {
            dbSession.CurrentRefreshTokenId = newRefresh.Id;
            dbSession.LastActivityAt = DateTime.UtcNow;
            await unitOfWork.UserSessions.UpdateAsync(dbSession);
        }

        await unitOfWork.SaveChangesAsync();
        await sessionCacheService.UpdateLastActivityAsync(sessionId, CancellationToken.None);

        ApplicationLog.Completed(logger,
            "User {UserId} refreshed access token for session {SessionId}",
            user.Id, sessionId);

        var response = new LoginDto
        {
            TokenModel = tokenData,
            User = user.Adapt<UserDto>()
        };

        return result.Succeed(IdentityMessages.OperationSucceeded, response);
    }

    public async Task<ApiOperationResult<UserDto>> LogoutCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var userId = currentUser.UserId ?? Guid.Empty;
        var sessionId = currentUser.SessionId ?? Guid.Empty;

        ApplicationLog.Started(logger, "Logout", userId == Guid.Empty ? null : userId.ToString());

        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "Logout", "session identifiers are missing");
            return result.Failed(IdentityMessages.InvalidSessionIdentifiers, HttpStatusCode.BadRequest);
        }

        var dbSession = await unitOfWork.UserSessions.GetBySessionIdAsync(sessionId, disableTracking: false);
        if (dbSession is not null && dbSession.UserId == userId && dbSession.RevokedAt is null)
            await RevokeSessionInternalAsync(dbSession, "logout", cancellationToken);
        else
            await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} logged out from session {SessionId}",
            userId, sessionId);

        return result.Succeed(IdentityMessages.LogoutSucceeded);
    }

    public async Task<ApiOperationResult<UserDto>> RevokeSessionAsync(RevokeSessionDto dto, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var userId = currentUser.UserId ?? Guid.Empty;
        var sessionId = dto.SessionId;

        ApplicationLog.Started(logger, "RevokeSession", userId == Guid.Empty ? null : userId.ToString());

        if (userId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "RevokeSession", "user is not authenticated");
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        var dbSession = await unitOfWork.UserSessions.GetBySessionIdAsync(sessionId, disableTracking: false);
        if (dbSession is null || dbSession.RevokedAt is not null)
        {
            ApplicationLog.Completed(logger,
                "User {UserId} revoked session {SessionId} â€” already inactive",
                userId, sessionId);
            return result.Succeed(IdentityMessages.OperationSucceeded);
        }

        if (dbSession.UserId != userId)
        {
            ApplicationLog.Blocked(logger, "RevokeSession", "session belongs to another user", userId.ToString());
            return result.Failed(IdentityMessages.SessionAccessDenied, HttpStatusCode.Forbidden);
        }

        await RevokeSessionInternalAsync(dbSession, "session_revoked", cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} revoked session {SessionId}",
            userId, sessionId);

        return result.Succeed(IdentityMessages.OperationSucceeded);
    }

    public async Task<ApiOperationResult<UserDto>> RevokeAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var userId = currentUser.UserId ?? Guid.Empty;

        ApplicationLog.Started(logger, "RevokeAllSessions", userId == Guid.Empty ? null : userId.ToString());

        if (userId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "RevokeAllSessions", "user is not authenticated");
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        var revokedCount = await RevokeAllSessionsForUserInternalAsync(userId, "revoke_all_sessions", cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} revoked all active sessions ({SessionCount} session(s))",
            userId, revokedCount);

        return result.Succeed(IdentityMessages.OperationSucceeded);
    }

    public async Task<ApiOperationResult<SessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<SessionDto>();
        var userId = currentUser.UserId ?? Guid.Empty;

        ApplicationLog.Started(logger, "GetActiveSessions", userId == Guid.Empty ? null : userId.ToString());

        if (userId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "GetActiveSessions", "user is not authenticated");
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        var sessions = await unitOfWork.UserSessions.GetActiveByUserAsync(userId);
        var list = sessions.Select(s =>
        {
            var dto = s.Adapt<SessionDto>();
            dto.IsActive = s.RevokedAt is null;
            return dto;
        }).ToList();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} active session(s)",
            userId, list.Count);

        return result.Succeed(IdentityMessages.OperationSucceeded, list, list.Count);
    }

    public async Task<ApiOperationResult<OnlineUserDto>> GetOnlineUsersAsync(CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<OnlineUserDto>();
        var permissionCheck = await RequireViewOnlineAsync<OnlineUserDto>(cancellationToken);
        if (permissionCheck is not null)
            return permissionCheck;

        var windowMinutes = Math.Max(1, sessionOptions.Value.OnlineActivityWindowMinutes);
        var activitySince = DateTime.UtcNow.AddMinutes(-windowMinutes);

        var onlineSessions = await unitOfWork.UserSessions.GetAllAsync(
            s => s.RevokedAt == null && s.LastActivityAt >= activitySince,
            disableTracking: true);

        if (onlineSessions.Count == 0)
            return result.Succeed(IdentityMessages.OperationSucceeded, [], 0);

        var userIds = onlineSessions.Select(s => s.UserId).Distinct().ToList();
        var users = await unitOfWork.Users.GetAllAsync(u => !u.IsDeleted && userIds.Contains(u.Id), disableTracking: true);
        var usersById = users.ToDictionary(u => u.Id);

        var list = onlineSessions
            .GroupBy(s => s.UserId)
            .Select(group =>
            {
                if (!usersById.TryGetValue(group.Key, out var user))
                    return null;

                var orderedSessions = group.OrderByDescending(s => s.LastActivityAt).ToList();
                var latestSession = orderedSessions[0];

                return new OnlineUserDto
                {
                    UserId = user.Id,
                    PhoneNumber = user.PhoneNumber,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    ActiveSessionCount = orderedSessions.Count,
                    LastActivityAt = latestSession.LastActivityAt,
                    LatestIpAddress = latestSession.IpAddress,
                    LatestUserAgent = latestSession.UserAgent,
                    Sessions = orderedSessions.Select(s =>
                    {
                        var dto = s.Adapt<SessionDto>();
                        dto.IsActive = true;
                        return dto;
                    }).ToList()
                };
            })
            .Where(dto => dto is not null)
            .Cast<OnlineUserDto>()
            .OrderByDescending(dto => dto.LastActivityAt)
            .ToList();

        ApplicationLog.Completed(logger,
            "User {RequesterId} loaded {Count} online user(s) (window={WindowMinutes}m)",
            currentUser.UserId, list.Count, windowMinutes);

        return result.Succeed(IdentityMessages.OperationSucceeded, list, list.Count);
    }

    #endregion

    #region User Administration

    public async Task<ApiOperationResult<UserDto>> CreateAsync(CreateUserDto dto)
    {
        var result = new ApiOperationResult<UserDto>();
        ApplicationLog.Started(logger, "CreateUser");

        var validation = await createUserValidator.ValidateAsync(dto, CancellationToken.None);
        if (!validation.IsValid)
        {
            ApplicationLog.Blocked(logger, "CreateUser", "validation failed");
            return result.Failed(IdentityMessages.ValidationFailed, validation.Errors.Select(error => error.ErrorMessage).ToList(), HttpStatusCode.BadRequest);
        }

        var existPhone = await unitOfWork.Users.GetAsync(user => user.PhoneNumber == dto.PhoneNumber);
        if (existPhone is not null)
        {
            ApplicationLog.Blocked(logger, "CreateUser", "phone number already registered");
            return result.Failed(IdentityMessages.PhoneAlreadyRegistered, HttpStatusCode.Conflict);
        }

        var normalizedEmail = NormalizeEmail(dto.Email);
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var existEmail = await unitOfWork.Users.GetAsync(user => user.Email == normalizedEmail);
            if (existEmail is not null)
            {
                ApplicationLog.Blocked(logger, "CreateUser", "email already registered");
                return result.Failed(IdentityMessages.EmailAlreadyRegistered, HttpStatusCode.Conflict);
            }
        }

        var existNationalCode = string.IsNullOrWhiteSpace(dto.NationalCode)
            ? null
            : await unitOfWork.Users.GetAsync(user => user.NationalCode == dto.NationalCode);
        if (existNationalCode is not null)
        {
            ApplicationLog.Blocked(logger, "CreateUser", "national code already registered");
            return result.Failed(IdentityMessages.NationalCodeAlreadyRegistered, HttpStatusCode.Conflict);
        }

        var user = dto.Adapt<User>();
        user.Email = NormalizeEmail(dto.Email);
        user.CreatedAt = DateTime.UtcNow;
        user.Role = otpOptions.Value.DevBypassEnabled
            && !string.IsNullOrWhiteSpace(otpOptions.Value.SeedAdminPhone)
            && string.Equals(dto.PhoneNumber, otpOptions.Value.SeedAdminPhone, StringComparison.Ordinal)
            ? UserRole.Admin
            : UserRole.Applicant;

        await unitOfWork.Users.AddAsync(user);
        await unitOfWork.SaveChangesAsync();

        ApplicationLog.Completed(logger,
            "New user {UserId} registered with role {Role} and phone {PhoneNumber}",
            user.Id, user.Role, user.PhoneNumber);

        return result.Succeed(IdentityMessages.UserCreated, user.Adapt<UserDto>());
    }

    public async Task<ApiOperationResult<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "UpdateUser", "user is not authenticated");
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        ApplicationLog.Started(logger, "UpdateUser", requesterId.ToString());

        var validation = await updateUserValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            ApplicationLog.Blocked(logger, "UpdateUser", "validation failed", requesterId.ToString());
            return result.Failed(IdentityMessages.ValidationFailed, validation.Errors.Select(e => e.ErrorMessage).ToList(), HttpStatusCode.BadRequest);
        }

        var user = await unitOfWork.Users.GetAsync(u => u.Id == id && !u.IsDeleted, false);
        if (user is null)
        {
            ApplicationLog.Blocked(logger, "UpdateUser", "target user not found", requesterId.ToString());
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);
        }

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, false);
        var requestedRole = ResolveRequestedRole(dto);
        if (requestedRole.HasValue
            && user.Role != requestedRole.Value
            && (requester is null
                || !await authorizationService.AuthorizeAsync(requester, Permissions.Users_ManageRoles, cancellationToken)))
        {
            ApplicationLog.Blocked(logger, "UpdateUser", "manage roles permission denied", requesterId.ToString());
            return result.Failed(IdentityMessages.OnlyAdminCanChangeRole, HttpStatusCode.Forbidden);
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && dto.PhoneNumber != user.PhoneNumber)
        {
            var phoneExists = await unitOfWork.Users.GetAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Id != id);
            if (phoneExists is not null)
            {
                ApplicationLog.Blocked(logger, "UpdateUser", "phone number already in use", requesterId.ToString());
                return result.Failed(IdentityMessages.PhoneAlreadyRegistered, HttpStatusCode.Conflict);
            }

            user.PhoneNumber = dto.PhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
        {
            var emailExists = await unitOfWork.Users.GetAsync(u => u.Email == dto.Email && u.Id != id);
            if (emailExists is not null)
            {
                ApplicationLog.Blocked(logger, "UpdateUser", "email already in use", requesterId.ToString());
                return result.Failed(IdentityMessages.EmailAlreadyRegistered, HttpStatusCode.Conflict);
            }

            user.Email = dto.Email;
        }

        if (!string.IsNullOrWhiteSpace(dto.NationalCode) && dto.NationalCode != user.NationalCode)
        {
            var nationalCodeExists = await unitOfWork.Users.GetAsync(u => u.NationalCode == dto.NationalCode && u.Id != id);
            if (nationalCodeExists is not null)
            {
                ApplicationLog.Blocked(logger, "UpdateUser", "national code already in use", requesterId.ToString());
                return result.Failed(IdentityMessages.NationalCodeAlreadyRegistered, HttpStatusCode.Conflict);
            }

            user.NationalCode = dto.NationalCode;
        }

        if (!string.IsNullOrWhiteSpace(dto.FirstName))
            user.FirstName = dto.FirstName;

        if (!string.IsNullOrWhiteSpace(dto.LastName))
            user.LastName = dto.LastName;

        if (requestedRole.HasValue)
        {
            var previousRole = user.Role;
            user.Role = requestedRole.Value;
            if (previousRole != requestedRole.Value)
                await permissionCacheService.RemoveUserPermissionsAsync(user.Id, cancellationToken);
        }

        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        if (dto.IsPhoneVerified.HasValue) user.IsPhoneVerified = dto.IsPhoneVerified.Value;
        if (dto.LastLoginAt.HasValue) user.LastLoginAt = dto.LastLoginAt;

        await unitOfWork.Users.UpdateAsync(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {RequesterId} updated profile of user {TargetUserId} (role {Role})",
            requesterId, id, user.Role);

        return result.Succeed(IdentityMessages.UserUpdated, user.Adapt<UserDto>());
    }

    public async Task<ApiOperationResult<SessionDto>> GetUserActiveSessionsAsAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<SessionDto>();
        var permissionCheck = await RequireSessionReadAsync<SessionDto>(cancellationToken);
        if (permissionCheck is not null)
            return permissionCheck;

        var user = await unitOfWork.Users.GetAsync(u => u.Id == userId && !u.IsDeleted, disableTracking: true);
        if (user is null)
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);

        var sessions = await unitOfWork.UserSessions.GetActiveByUserAsync(userId);
        var list = sessions.Select(s =>
        {
            var dto = s.Adapt<SessionDto>();
            dto.IsActive = s.RevokedAt is null;
            return dto;
        }).ToList();

        return result.Succeed(IdentityMessages.OperationSucceeded, list, list.Count);
    }

    public async Task<ApiOperationResult<UserDto>> AdminRevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var permissionCheck = await RequireSessionRevokeAsync<UserDto>(cancellationToken);
        if (permissionCheck is not null)
            return permissionCheck;

        var user = await unitOfWork.Users.GetAsync(u => u.Id == userId && !u.IsDeleted, disableTracking: true);
        if (user is null)
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);

        var revokedCount = await RevokeAllSessionsForUserInternalAsync(userId, "admin_revoke_all", cancellationToken);

        ApplicationLog.Completed(logger,
            "User {RequesterId} revoked all sessions for user {TargetUserId} ({SessionCount} session(s))",
            currentUser.UserId, userId, revokedCount);

        return result.Succeed(IdentityMessages.UserSessionsRevokedByAdmin);
    }

    public async Task<ApiOperationResult<UserDto>> KickUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);

        var permissionCheck = await RequireSessionRevokeAsync<UserDto>(cancellationToken);
        if (permissionCheck is not null)
            return permissionCheck;

        if (userId == requesterId)
        {
            ApplicationLog.Blocked(logger, "KickUser", "cannot kick self", requesterId.ToString());
            return result.Failed(IdentityMessages.CannotKickSelf, HttpStatusCode.BadRequest);
        }

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, disableTracking: true);
        var user = await unitOfWork.Users.GetAsync(u => u.Id == userId && !u.IsDeleted, disableTracking: true);
        if (user is null)
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);

        if (IsProtectedRole(user.Role) && requester?.Role != UserRole.Admin)
        {
            ApplicationLog.Blocked(logger, "KickUser", "protected role", requesterId.ToString());
            return result.Failed(IdentityMessages.CannotDeleteProtectedUser, HttpStatusCode.Forbidden);
        }

        var revokedCount = await RevokeAllSessionsForUserInternalAsync(userId, "user_kicked", cancellationToken);

        ApplicationLog.Completed(logger,
            "User {RequesterId} kicked user {TargetUserId} out ({SessionCount} active session(s) revoked)",
            requesterId, userId, revokedCount);

        return result.Succeed(IdentityMessages.UserKickedOut);
    }

    public async Task<ApiOperationResult<UserDto>> AdminRevokeSessionAsync(Guid userId, RevokeSessionDto dto, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var permissionCheck = await RequireSessionRevokeAsync<UserDto>(cancellationToken);
        if (permissionCheck is not null)
            return permissionCheck;

        var user = await unitOfWork.Users.GetAsync(u => u.Id == userId && !u.IsDeleted, disableTracking: true);
        if (user is null)
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);

        var dbSession = await unitOfWork.UserSessions.GetBySessionIdAsync(dto.SessionId, disableTracking: false);
        if (dbSession is null || dbSession.RevokedAt is not null)
            return result.Succeed(IdentityMessages.OperationSucceeded);

        if (dbSession.UserId != userId)
            return result.Failed(IdentityMessages.SessionAccessDenied, HttpStatusCode.BadRequest);

        await RevokeSessionInternalAsync(dbSession, "admin_session_revoked", cancellationToken);

        ApplicationLog.Completed(logger,
            "User {RequesterId} revoked session {SessionId} for user {TargetUserId}",
            currentUser.UserId, dto.SessionId, userId);

        return result.Succeed(IdentityMessages.OperationSucceeded);
    }

    public async Task<ApiOperationResult<UserDto>> GetByIdAsync(Guid id)
    {
        var result = new ApiOperationResult<UserDto>();
        ApplicationLog.Started(logger, "GetUserById");

        var user = await unitOfWork.Users.GetAsync(u => u.Id == id && !u.IsDeleted);
        if (user is null)
        {
            ApplicationLog.Blocked(logger, "GetUserById", "user not found");
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);
        }

        ApplicationLog.Completed(logger, "Loaded user {UserId}", id);
        return result.Succeed(IdentityMessages.OperationSucceeded, user.Adapt<UserDto>());
    }

    public async Task<ApiOperationResult<UserDto>> GetPagedAsync(int take, int skip)
    {
        var result = new ApiOperationResult<UserDto>();
        ApplicationLog.Started(logger, "GetUsersPaged");

        var users = await unitOfWork.Users.GetPagedListAsync(take, skip, u => !u.IsDeleted);
        var total = await unitOfWork.Users.CountAsync(u => !u.IsDeleted);
        var list = users.Select(u => u.Adapt<UserDto>()).ToList();

        ApplicationLog.Completed(logger,
            "Listed users page skip={Skip}, take={Take} â€” {Count} of {Total}",
            skip, take, list.Count, total);

        return result.Succeed(IdentityMessages.OperationSucceeded, list, total);
    }

    public async Task<ApiOperationResult<UserDto>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        ApplicationLog.Started(logger, "DeleteUser");

        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "DeleteUser", "user is not authenticated");
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, disableTracking: true);
        if (requester is null || !await authorizationService.AuthorizeAsync(requester, Permissions.Users_Delete, cancellationToken))
        {
            ApplicationLog.Blocked(logger, "DeleteUser", "delete permission denied", requesterId.ToString());
            return result.Failed(IdentityMessages.UserDeleteAccessDenied, HttpStatusCode.Forbidden);
        }

        if (id == requesterId)
        {
            ApplicationLog.Blocked(logger, "DeleteUser", "cannot delete self", requesterId.ToString());
            return result.Failed(IdentityMessages.CannotDeleteSelf, HttpStatusCode.BadRequest);
        }

        var user = await unitOfWork.Users.GetAsync(u => u.Id == id && !u.IsDeleted, false);
        if (user is null)
        {
            ApplicationLog.Blocked(logger, "DeleteUser", "user not found");
            return result.Failed(IdentityMessages.UserNotFound, HttpStatusCode.NotFound);
        }

        if (IsProtectedRole(user.Role) && requester.Role != UserRole.Admin)
        {
            ApplicationLog.Blocked(logger, "DeleteUser", "protected role", requesterId.ToString());
            return result.Failed(IdentityMessages.CannotDeleteProtectedUser, HttpStatusCode.Forbidden);
        }

        var revokedCount = await RevokeAllSessionsForUserInternalAsync(user.Id, "user_deleted", cancellationToken);
        await permissionCacheService.RemoveUserPermissionsAsync(user.Id, cancellationToken);

        await unitOfWork.Users.DeleteAsync(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {RequesterId} soft-deleted user {TargetUserId} and revoked {SessionCount} session(s)",
            requesterId, id, revokedCount);

        return result.Succeed(IdentityMessages.UserDeleted);
    }

    public async Task<ApiOperationResult<UserDto>> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var result = new ApiOperationResult<UserDto>();
        var userId = currentUser.UserId ?? Guid.Empty;

        ApplicationLog.Started(logger, "GetProfile", userId == Guid.Empty ? null : userId.ToString());

        if (userId == Guid.Empty)
        {
            ApplicationLog.Blocked(logger, "GetProfile", "user is not authenticated");
            return result.Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        var user = await unitOfWork.Users.GetAsync(u => u.Id == userId && !u.IsDeleted, false);

        ApplicationLog.Completed(logger,
            "User {UserId} loaded own profile",
            userId);

        return result.Succeed(IdentityMessages.OperationSucceeded, user is null ? new UserDto() : user.Adapt<UserDto>());
    }

    #endregion

    #region Private

    private static ApiOperationResult<LoginDto> BuildOtpFailure(
        ApiOperationResult<LoginDto> result,
        OtpValidationResult otpValidation)
    {
        if (otpValidation.Locked)
            return result.Failed(IdentityMessages.OtpLocked, HttpStatusCode.BadRequest);

        if (otpValidation.Expired)
            return result.Failed(IdentityMessages.OtpExpired, HttpStatusCode.BadRequest);

        return result.Failed(IdentityMessages.InvalidOtp, HttpStatusCode.BadRequest);
    }

    private SessionDescriptor CreateSessionDescriptor(Guid userId, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        return new SessionDescriptor(
            SessionId: sessionId,
            UserId: userId,
            IpAddress: requestContext.IpAddress,
            UserAgent: requestContext.UserAgent,
            CreatedAt: now,
            ExpiresAt: now.AddDays(Math.Max(1, sessionOptions.Value.AbsoluteExpirationDays)));
    }

    private UserSession CreateUserSession(Guid userId, Guid sessionId, Guid refreshTokenId)
    {
        var now = DateTime.UtcNow;
        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            CurrentRefreshTokenId = refreshTokenId,
            DeviceId = requestContext.DeviceId,
            UserAgent = requestContext.UserAgent,
            IpAddress = requestContext.IpAddress,
            CreatedAt = now,
            LastActivityAt = now
        };
    }

    private async Task InvalidateFamilyAsync(Guid userId, Guid familyId, string reason, CancellationToken cancellationToken)
    {
        var tokens = await unitOfWork.RefreshTokens.GetAllAsync(t => t.UserId == userId && t.FamilyId == familyId, disableTracking: false);
        foreach (var token in tokens)
        {
            if (token.RevokedAt is null)
            {
                refreshTokenService.Revoke(token, replacedByTokenId: null, reason: reason, revokedByIp: requestContext.IpAddress);
                await unitOfWork.RefreshTokens.UpdateAsync(token);
            }
        }

        await RevokeAllSessionsForUserInternalAsync(userId, reason, cancellationToken);
    }

    private static bool IsProtectedRole(UserRole role)
        => role is UserRole.Admin or UserRole.Ceo;

    private static UserRole? ResolveRequestedRole(UpdateUserDto dto)
    {
        if (dto.Role.HasValue)
            return dto.Role.Value;

        if (dto.RoleNumber is int roleNumber && Enum.IsDefined(typeof(UserRole), roleNumber))
            return (UserRole)roleNumber;

        return null;
    }

    private async Task<ApiOperationResult<T>?> RequireAdminAsync<T>(CancellationToken cancellationToken)
    {
        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
            return new ApiOperationResult<T>().Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, disableTracking: true);
        if (requester is null || requester.Role != UserRole.Admin)
            return new ApiOperationResult<T>().Failed(IdentityMessages.AdminAccessDenied, HttpStatusCode.Forbidden);

        return null;
    }

    private async Task<ApiOperationResult<T>?> RequireViewOnlineAsync<T>(CancellationToken cancellationToken)
    {
        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
            return new ApiOperationResult<T>().Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, disableTracking: true);
        if (requester is null || !await authorizationService.AuthorizeAsync(requester, Permissions.Users_ViewOnline, cancellationToken))
            return new ApiOperationResult<T>().Failed(IdentityMessages.OnlineUsersAccessDenied, HttpStatusCode.Forbidden);

        return null;
    }

    private async Task<ApiOperationResult<T>?> RequireSessionReadAsync<T>(CancellationToken cancellationToken)
    {
        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
            return new ApiOperationResult<T>().Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, disableTracking: true);
        if (requester is null || !await authorizationService.AuthorizeAsync(requester, Permissions.Sessions_Read, cancellationToken))
            return new ApiOperationResult<T>().Failed(IdentityMessages.SessionRevokeAccessDenied, HttpStatusCode.Forbidden);

        return null;
    }

    private async Task<ApiOperationResult<T>?> RequireSessionRevokeAsync<T>(CancellationToken cancellationToken)
    {
        var requesterId = currentUser.UserId ?? Guid.Empty;
        if (requesterId == Guid.Empty)
            return new ApiOperationResult<T>().Failed(IdentityMessages.AuthenticationRequired, HttpStatusCode.Unauthorized);

        var requester = await unitOfWork.Users.GetAsync(u => u.Id == requesterId && !u.IsDeleted, disableTracking: true);
        if (requester is null || !await authorizationService.AuthorizeAsync(requester, Permissions.Sessions_Revoke, cancellationToken))
            return new ApiOperationResult<T>().Failed(IdentityMessages.SessionRevokeAccessDenied, HttpStatusCode.Forbidden);

        return null;
    }

    private int MaxActiveSessions =>
        Math.Max(1, sessionOptions.Value.MaxActiveSessions);

    private async Task TrimActiveSessionsForLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var max = MaxActiveSessions;
        var active = await unitOfWork.UserSessions.GetAllAsync(
            s => s.UserId == userId && s.RevokedAt == null,
            disableTracking: false);

        var excess = active.Count - max + 1;
        if (excess <= 0)
            return;

        foreach (var session in active.OrderBy(s => s.LastActivityAt).Take(excess))
            await RevokeSessionInternalAsync(session, "session_limit_exceeded", cancellationToken);
    }

    private async Task RevokeSessionInternalAsync(UserSession dbSession, string reason, CancellationToken cancellationToken)
    {
        if (dbSession.RevokedAt is not null)
            return;

        dbSession.RevokedAt = DateTime.UtcNow;
        dbSession.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.UserSessions.UpdateAsync(dbSession);

        var tokens = await unitOfWork.RefreshTokens.GetAllAsync(
            t => t.UserId == dbSession.UserId && t.SessionId == dbSession.SessionId && t.RevokedAt == null,
            disableTracking: false);
        foreach (var token in tokens)
        {
            refreshTokenService.Revoke(token, replacedByTokenId: null, reason: reason, revokedByIp: requestContext.IpAddress);
            await unitOfWork.RefreshTokens.UpdateAsync(token);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await sessionCacheService.RevokeSessionAsync(dbSession.SessionId, cancellationToken);
    }

    private async Task<int> RevokeAllSessionsForUserInternalAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var sessions = await unitOfWork.UserSessions.GetAllAsync(s => s.UserId == userId && s.RevokedAt == null, disableTracking: false);
        foreach (var session in sessions)
            await RevokeSessionInternalAsync(session, reason, cancellationToken);

        await sessionCacheService.RevokeAllSessionsAsync(userId, cancellationToken);
        return sessions.Count;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return email.Trim();
    }

    #endregion
}
