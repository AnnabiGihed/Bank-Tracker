# KbcTracker.ServiceDefaults

What every runnable host shares when Aspire runs it ([ADR-0005](../../../../../docs/adr/0005-run-the-app-on-the-pc-with-aspire.md)).

| Path | Responsibility |
| --- | --- |
| `Features/Telemetry/ServiceDefaultsExtensions.cs` | `AddServiceDefaults`: OpenTelemetry logs, traces and metrics, sent to the Aspire dashboard only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. |

The API and the web app call `builder.AddServiceDefaults()`. It deliberately adds no health endpoints (the API has
`/health`) and no HTTP retry handler (retries would replay the bank's one-time code). No test project: it only
registers OpenTelemetry, which a run under the AppHost shows in the dashboard.
