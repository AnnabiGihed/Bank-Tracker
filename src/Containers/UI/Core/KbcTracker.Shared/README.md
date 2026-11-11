# KbcTracker.Shared

What the user interfaces share. Today that is `BackendClient`, the typed client of the backend's API: it reads the
workspace and the bank list, sends decisions, starts and completes the bank consent, and asks for a sync. Failed
actions come back as a `BackendReply` with the API's status code and its safe message; a refused read (workspace,
bank list) throws a `BackendRefusedException` carrying the same status and message, and an unreachable backend an
`HttpRequestException`. The client holds no data and no bank key.

The web app uses it now; the Android app will use it too. The endpoints are described by the
[OpenAPI contract](../../../../../docs/api/openapi.json).
Tests: `test/Containers/UI/Core/KbcTracker.Shared.Tests`, and through the web app in `test/KbcTracker.E2E.Tests`.
