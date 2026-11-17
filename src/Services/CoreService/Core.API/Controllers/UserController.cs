using Asp.Versioning;
using BuildingBlocks.Application.Results;
using Core.API.Http;
using Core.Application.Identity.DTOs.User;
using Core.Application.Identity.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.API.Controllers;

/// <summary>Panel users — OTP login, JWT sessions, and profile.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/users")]
public sealed class UserController(IUserService userService) : ApiControllerBase
{
    [HttpGet("captcha")]
    [AllowAnonymous]
    public Task<IActionResult> Captcha(CancellationToken ct)
        => Execute(userService.CreateCaptchaAsync());

    [HttpPost("send-otp")]
    [AllowAnonymous]
    public Task<IActionResult> SendOtp([FromBody] SendOtpDto dto, CancellationToken ct)
        => Execute(userService.SendOtpAsync(dto));

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto, CancellationToken ct)
        => Execute(userService.VerifyOtpAsync(dto));

    [HttpPost("login-password")]
    [AllowAnonymous]
    public Task<IActionResult> LoginWithPassword([FromBody] LoginWithPasswordDto dto, CancellationToken ct)
        => Execute(userService.LoginWithPasswordAsync(dto));

    [HttpPost("password")]
    [Authorize]
    public Task<IActionResult> SetPassword([FromBody] SetPasswordDto dto, CancellationToken ct)
        => Execute(userService.SetPasswordAsync(dto, ct));

    [HttpPost("{id:guid}/password")]
    [Authorize(Policy = "AdminOnly")]
    public Task<IActionResult> AdminSetPassword(Guid id, [FromBody] AdminSetPasswordDto dto, CancellationToken ct)
        => Execute(userService.AdminSetPasswordAsync(id, dto, ct));

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto, CancellationToken ct)
    {
        var bearer = Request.Headers.Authorization.ToString();
        var accessToken = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? bearer["Bearer ".Length..].Trim()
            : bearer.Trim();

        return Execute(userService.RefreshTokenAsync(dto, accessToken));
    }

    [HttpPost("logout")]
    [Authorize]
    public Task<IActionResult> Logout(CancellationToken ct)
        => Execute(userService.LogoutCurrentSessionAsync(ct));

    [HttpGet("sessions")]
    [Authorize]
    public Task<IActionResult> GetActiveSessions(CancellationToken ct)
        => Execute(userService.GetActiveSessionsAsync(ct));

    [HttpGet("online")]
    [Authorize(Policy = "Users.ViewOnline")]
    public Task<IActionResult> GetOnlineUsers(CancellationToken ct)
        => Execute(userService.GetOnlineUsersAsync(ct));

    [HttpPost("sessions/revoke")]
    [Authorize]
    public Task<IActionResult> RevokeSession([FromBody] RevokeSessionDto dto, CancellationToken ct)
        => Execute(userService.RevokeSessionAsync(dto, ct));

    [HttpPost("sessions/revoke-all")]
    [Authorize]
    public Task<IActionResult> RevokeAllSessions(CancellationToken ct)
        => Execute(userService.RevokeAllSessionsAsync(ct));

    [HttpGet("{id:guid}/sessions")]
    [Authorize(Policy = "Sessions.Read")]
    public Task<IActionResult> GetUserSessions(Guid id, CancellationToken ct)
        => Execute(userService.GetUserActiveSessionsAsAdminAsync(id, ct));

    [HttpPost("{id:guid}/sessions/revoke-all")]
    [Authorize(Policy = "Sessions.Revoke")]
    public Task<IActionResult> AdminRevokeAllSessions(Guid id, CancellationToken ct)
        => Execute(userService.AdminRevokeAllSessionsAsync(id, ct));

    [HttpPost("{id:guid}/sessions/revoke")]
    [Authorize(Policy = "Sessions.Revoke")]
    public Task<IActionResult> AdminRevokeSession(Guid id, [FromBody] RevokeSessionDto dto, CancellationToken ct)
        => Execute(userService.AdminRevokeSessionAsync(id, dto, ct));

    /// <summary>Force-logout an online user by revoking all active sessions.</summary>
    [HttpPost("{id:guid}/kick")]
    [Authorize(Policy = "Sessions.Revoke")]
    public Task<IActionResult> KickUser(Guid id, CancellationToken ct)
        => Execute(userService.KickUserAsync(id, ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Execute(userService.GetByIdAsync(id));

    [HttpGet("profile")]
    [Authorize]
    public Task<IActionResult> Profile(CancellationToken ct)
        => Execute(userService.GetProfileAsync(ct));

    [HttpGet]
    [Authorize]
    public Task<IActionResult> GetPaged([FromQuery] int take = 10, [FromQuery] int skip = 0, CancellationToken ct = default)
        => Execute(userService.GetPagedAsync(take, skip));

    [HttpPost]
    [AllowAnonymous]
    public Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken ct)
        => Execute(userService.CreateAsync(dto));

    [HttpPut("{id:guid}")]
    [Authorize]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken ct)
        => Execute(userService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Users.Delete")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => Execute(userService.DeleteAsync(id, ct));

    private async Task<IActionResult> Execute<T>(Task<ApiOperationResult<T>> action)
        => Respond(await action.ConfigureAwait(false));
}
