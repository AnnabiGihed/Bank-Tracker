# ADR-0002: Adopt the shared analyzer set with documented local deviations

- Status: Proposed
- Date: 2026-11-09
- Deciders: Gihed Annabi

## Context

The project now follows the installed *Static Analysis and Quality Gates* rules. They require:

- one analyzer set declared in `Directory.Build.props`: the .NET SDK analyzers, `SonarAnalyzer.CSharp`,
  `StyleCop.Analyzers` (naming, layout and documentation rules only) and `Meziantou.Analyzer` (R1);
- versions pinned centrally in `Directory.Packages.props` (R2);
- warnings as errors, nullable reference types and code style in the build (R6–R8).

The user chose the full set over a lighter one. This conflicts with the earlier "no NuGet packages" rule in
`CLAUDE.md`, which ADR-0001 already relaxed.

Forces:

- Most of the code was written before these rules. The domain, application and infrastructure layers were
  rewritten when they were split out of the web host and can meet them now. The web host's pages and `Program.cs`
  are retired when the Android app exists (ADR-0003) and would need a large clean-up first.
- Some StyleCop rules contradict the house style that the installed C# skills prescribe: field prefixes, region
  layout and constructor wording.
- The repository indented C# with 4 spaces, while the rules' sample `.editorconfig` uses tabs for Pivot.Framework.
  Since ADR-0003 the project is built on Pivot.Framework and follows its tabs.
- Restore used the machine-wide NuGet sources, which include an Azure DevOps feed that needs work credentials.
- There is no CI, and no SonarCloud project yet.

## Decision

We adopt the full analyzer set for every project, pinned centrally. The pinned versions are SonarAnalyzer.CSharp
10.35.0.4138, StyleCop.Analyzers 1.3.151-beta and Meziantou.Analyzer 3.0.317, each at least two weeks old when
pinned. We also adopt warnings as errors, code style in the build, `AnalysisLevel` `latest-recommended` and XML
documentation files. The domain, application and infrastructure projects and all test projects build with zero
findings.

We restrict the package sources in a repository `nuget.config` to nuget.org, plus the Pivot.Framework feed for
`Pivot.Framework.*` packages only (ADR-0003), so restore does not depend on work feeds.

We accept these deviations:

| Rule | Deviation | Reason | Expires |
| --- | --- | --- | --- |
| R6 | `TreatWarningsAsErrors` is `false` for the web host (`src/Containers/UI/Hosting/KbcTracker.Web`); its remaining findings, in the page classes (`Features/*/…Page.cs`, `PageKit.cs`) and `Program.cs`, are warnings. | Legacy page code written before the analyzers. The web host stays as a client of the API (ADR-0004), so the pages are to be brought up to the analyzers rather than retired. Every type in it is documented and regioned, and its API-client code (`WebEndpoints`, the middleware, `CsvExport`) builds without findings. | Pages brought up to the analyzers, at the latest 2027-06-30 |
| StyleCop SA1309 | Off. | The higher-priority house rule wins: clean-code-naming and the C# skills name fields `_camelCase` (`_logger`). | Review when an organizational `.editorconfig` is adopted, at the latest 2027-06-30 |
| StyleCop SA1514 | Off. | The higher-priority house rule wins: `csharp-regions` forbids a blank line directly after `#region`, where the first member's documentation starts. | Review when an organizational `.editorconfig` is adopted, at the latest 2027-06-30 |
| StyleCop SA1642 | Off. | The higher-priority house rule wins: `csharp-xml-documentation` has constructors read "Initialises a new …". | Review when an organizational `.editorconfig` is adopted, at the latest 2027-06-30 |
| CA1716 | Off. | The higher-priority Pivot layout rule (`dotnet-ddd-cqrs-conventions`) requires `Features/Shared/` folders, and namespaces follow folders; `Shared` is a keyword only in Visual Basic, which never consumes this code. | Review when an organizational `.editorconfig` is adopted, at the latest 2027-06-30 |
| R11 | Tests turn off CA1707, MA0002 and CA1861 (`test/.editorconfig`). | Test names use `Method_Condition_ExpectedResult`; string asserts and inline expected arrays are clearer as written. Documentation rules stay on. | None |
| R9, R12–R19 | No CI format check, SonarCloud project or quality gate yet. | A single-user personal project without CI. Add them when the Android app is published. | First published release of the Android app |
| R25 (Aspire ASPIRE010) | Off in the AppHost (`NoWarn` in `src/Containers/Aspire/Hosting/KbcTracker.AppHost/KbcTracker.AppHost.csproj`), added 2026-11-11. | The AppHost takes the dashboard and orchestrator from NuGet (`AspireUseCliBundle=false`, ADR-0005) so `dotnet run` and Visual Studio start it without installing the Aspire CLI; ASPIRE010 only reports that choice and has no code fix. | Review when the Aspire CLI is installed on the PC or the app moves to Docker Compose, at the latest 2027-06-30 |
| Docs quality gates R5, R6, R12, R23, R24; Documentation as Code R18 | No Markdownlint, link check or docs pipeline, and no published docs site. Markdown is checked by hand (line length, links). | No CI yet; the docs are read in the repository. Add the docs gates with the CI above. | First published release of the Android app |

These suppressions sit at the point of use, with a justification (R22):

- `S5542` in `EnableBankingClient.CreateJwt`. RS256 is RSASSA-PKCS1-v1_5 by definition, and it is the only signing
  algorithm Enable Banking accepts.
- `CA5350` and `S4790` in `EnableBankingMapper.ContentId`. SHA-1 only fingerprints a transaction's content to build
  its id. Ids already stored were made with it, so changing it would duplicate the history.

## Consequences

**Positive**

- The shared core is held to the full rule set from the start, before the Android app is built on it.
- New findings fail the build instead of piling up.
- The documentation the commit gate checks is also enforced by the compiler (CS1591, SA1600).
- Restore works on any machine, without work credentials.

**Negative**

- Three more build-time packages, and a slower build.
- Analyzer updates can add new findings that block the build; versions are bumped deliberately.
- The web host keeps about 200 warnings until it is retired, which hides any new warning there.
- Without CI and SonarCloud, the rules are only enforced on the developer's machine.

## Alternatives considered

- **Strict settings on the new projects only, without third-party analyzers** - rejected by the user in favour of the
  full set.
- **Waive the analyzer rules for this personal project** - rejected: the shared core will outlive the web host and
  should meet the standard.
- **Clean up the web host now** - rejected: it is retired in step 8, so the effort would be thrown away.
