namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries the setting both UI hosts need to reach the backend: where it listens. Who the caller is
///              comes from the access token each host adds through its own message handler (ADR-0009).
/// </summary>
/// <param name="BaseUrl">The backend's base address: absolute HTTP or HTTPS, without user info, ending in a slash.</param>
public sealed record BackendOptions(string? BaseUrl);
