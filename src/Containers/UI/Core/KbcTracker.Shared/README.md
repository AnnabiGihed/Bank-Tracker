# KbcTracker.Shared

What the user interfaces share. Today that is `BackendClient`, the typed client of the backend's API: it reads the
workspace and the bank list, sends decisions, starts and completes the bank consent, and asks for a sync. Failed
actions come back as a `BackendReply` with the API's status code and its safe message; a refused read (workspace,
bank list) throws a `BackendRefusedException` carrying the same status and message, and an unreachable backend an
`HttpRequestException`. The client holds no data and no bank key.

`AddBackendClient(services, BackendOptions)` registers it for either host: the base address must be absolute HTTP(S)
without user info, and the timeout is 5 minutes. The client sends no credentials itself: each host adds a
`DelegatingHandler` that sets the access token, through `WithAccessTokenHandler<THandler>()` on the returned builder
([ADR-0009](../../../../../docs/adr/0009-authenticate-the-owner-with-external-openid-connect-providers.md)).

The web app and the Android app both use it. The sign-in logic of the Android app, free of any platform, is in
`Features/Authentication`: `PublicClientTokenService` (authorize address, code exchange, in-memory access token,
rotating refresh token in an `IRefreshTokenStore`, refresh before expiry, revocation at sign-out), `PkceChallenge`,
`TokenResponse` and `AuthorizationCallback`; its tests need no Android. The endpoints are described by the
[OpenAPI contract](../../../../../docs/api/openapi.json).
The view models on top of it are in [KbcTracker.ViewModels](../KbcTracker.ViewModels/README.md).
Tests: `test/Containers/UI/Core/KbcTracker.Shared.Tests`, and through the web app in `test/KbcTracker.E2E.Tests`.
