# KbcTracker.ApiService

The backend. It serves the API the user interfaces use (history, categories, the workspace, decisions, bank
consent, connection status and sync), runs the automatic sync, and offers the maintenance commands `--migrate`,
`--import-json`, `--initialize` and `--sync`.

| Path | Responsibility |
| --- | --- |
| `Program.cs` | Configuration, the loopback listener and the request pipeline. |
| `Features/Shared/BackendController.cs` | History, categories, connection status, manual sync and health. |
| `Features/Workspace/WorkspaceController.cs` | The workspace and the decisions. |
| `Features/BankConnection/BankConsentController.cs` | The bank list and the consent flow. |
| `Features/BankConnection/AutomaticBankSync.cs` | Syncs every `AutoSyncHours` while the backend runs. |
| `Features/Shared/BackendAdministration.cs` | The maintenance commands. |
| `Features/Shared/Security/TailnetAccessMiddleware.cs` | The loopback and owner-identity gate. |

See [Run the backend](../../../../docs/how-to/run-the-backend.md) for configuration, setup order and access
restrictions, and the [OpenAPI contract](../../../../docs/api/openapi.json) for the endpoints.
Tests: `test/Containers/API/KbcTracker.ApiService.Tests`.
