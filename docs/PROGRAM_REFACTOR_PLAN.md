# Program.cs optimization and refactor plan

This document proposes concrete, incremental changes to shrink Program.cs, separate concerns, improve testability, and add resilience—without changing behavior.

## Goals

- Reduce size and complexity of `Program.cs`
- Separate responsibilities (CLI parsing, orchestration, I/O, API calls, formatting)
- Improve testability with small units and interfaces
- Keep current behavior and outputs stable while enabling future features

## Current pain points (observed)

- God class: `Program.cs` owns CLI parsing, orchestration, API polling, result parsing, file I/O, and logging
- Manual `--mode`/flag parsing is brittle and hard to extend
- Repeated logic (content-type mapping, result export, result parsing)
- Heavy use of `JsonElement` with ad-hoc parsing; hard to validate and test
- No cancellation token threading; long polls can’t be canceled gracefully
- Error handling mixed with orchestration; limited retry/backoff semantics

## Target architecture (layers and folders)

- cli/
  - System.CommandLine-based entrypoint with subcommands instead of `--mode`
  - Commands: `AnalyzeCommand`, `ClassifyCommand`, `ClassifyDirCommand`, `ListAnalyzersCommand`, `CreateAnalyzerCommand`, `ListClassifiersCommand`, `CreateClassifierCommand`, `CheckOperationCommand`, `HealthCommand`
- orchestration/
  - `AnalysisOrchestrator` (submit + poll + summarize + export)
  - `ClassificationOrchestrator` (per-file and directory; sequential with optional DOP later)
- services/
  - Keep `ContentUnderstandingService` focused on HTTP calls
- io/
  - `ResultExporter` (JSON and formatted text; naming, timestamps, paths)
  - `BatchSummaryWriter` (JSON-only; one method)
  - `PathResolver` (project root, sample docs path, output path)
  - `MimeTypeProvider` (extension -> content type)
- parsing/
  - `FieldExtractor` (maps result JSON to readable strings)
  - Optional: strongly-typed DTOs + `System.Text.Json` with `JsonSerializerContext` source generators
- common/
  - `AppOptions` (timeout, polling interval, output dir, etc.) via `IOptions<AppOptions>`
  - `ConsoleTheme`/`LoggingExtensions` (scopes, structured logs)

## Framework and plumbing

- Use `Host.CreateDefaultBuilder(args)` to set up DI, config, logging, and lifetime; keep `Program.cs` minimal
- Use `System.CommandLine` to define commands, options, and handlers
- Thread `CancellationToken` throughout orchestration and polling
- Add resilience with Polly (retry on 5xx/429 + jittered backoff)

## Incremental migration plan (safe order)

1) Host + DI bootstrap
- Introduce a `HostBuilder` with `ConfigureServices` (reuse existing registrations)
- Keep existing `Main` path, but move towards `host.Services.GetRequiredService<...>()`

2) Extract small utilities
- `MimeTypeProvider.GetContentType(ext)`
- `PathResolver.GetSampleDocsDir()/GetOutputDir()`
- `TimestampProvider` or util method for consistent timestamps

3) Move result export and formatting
- Create `ResultExporter` containing `ExportAnalysisResultsAsync` and `CreateFormattedResultsAsync`
- `Program.cs` calls exporter instead of owning the methods

4) Move directory classification orchestration
- `ClassifyDirectoryCommand` or `ClassificationOrchestrator.ClassifyDirectoryAsync`
- Move `BatchSummaryRow` and `ExportBatchSummaryAsync` into `io/BatchSummaryWriter`

5) Adopt System.CommandLine gradually
- Keep `--mode` for now; introduce parallel subcommands
- Once stable, deprecate `--mode` in favor of `cli subcommands` (help, analyze, classify, classify-dir, etc.)

6) Optional: replace `JsonElement` parsing with typed models
- Create DTOs and `JsonSerializerContext`
- Centralize field extraction in `FieldExtractor`

7) Tests
- Unit tests for `MimeTypeProvider`, `PathResolver`, `ResultExporter`, `FieldExtractor`
- Use `System.IO.Abstractions` to mock file I/O

## Quick wins (low risk, small PRs)

- Extract MIME mapping into `MimeTypeProvider`
- Extract Output directory + timestamp filename generation into `ResultExporter`
- Move `BatchSummaryRow` + JSON writer to `BatchSummaryWriter`
- Introduce `CancellationToken` parameters and pass `CancellationToken.None` initially
- Convert repeated string literals (e.g., folder names) to constants

## Resilience and UX enhancements (opt-in, later)

- Polly retry for 429/5xx with exponential backoff and jitter
- Add `--timeout` and `--poll-interval` options bound to `AppOptions`
- Add `--output` to customize Output directory
- Add progress indicator for long polls
- Add `--max-files`, `--recursive`, `--dop` (degree of parallelism) for directory runs

## Logging and telemetry

- Use `ILogger.BeginScope` to add `OperationId` and `DocumentName` to a scope
- Optionally add OpenTelemetry traces with console exporter (keep off by default)

## Example skeletons (illustrative only)

Program.cs (final target):

- Build host
- Wire up `System.CommandLine` root + subcommands
- Invoke handler for chosen command

ResultExporter (sample public surface):

- `Task<(string jsonPath, string textPath)> ExportAsync(string json, string documentName, string operationId, CancellationToken ct)`
- Internally handles Output dir, filenames, pretty JSON

BatchSummaryWriter (sample):

- `Task<string> WriteJsonAsync(string directoryName, string classifierName, IEnumerable<BatchSummaryRow> rows, CancellationToken ct)`

## Risk & mitigation

- Behavior drift: keep existing logs and filenames; write regression tests for filenames and summary output
- CLI change risk: run `--mode` and new subcommands side-by-side before deprecating
- Coupling to `ContentUnderstandingService`: keep it unchanged; orchestration composes it

## Expected outcomes

- `Program.cs` reduced to ~30–50 lines
- Clear command structure, faster iteration on new modes/features
- Focused, testable components for export, parsing, and orchestration
- Easier to add resilience and better UX without touching core logic
