# SDK Findings — Microsoft Agent Framework & Hyperlight CodeAct

> Phase 0 deliverable. Records the **verified** APIs, package versions, source links,
> preview limitations, and implementation decisions that the rest of this repository
> depends on. Nothing in this file is invented; every claim was confirmed against the
> published NuGet packages and the `microsoft/agent-framework` source, or a local
> compilation/restore probe (`codeact-poc/_probe`).

Original verification: 2026-08-31. Pinned cancellation investigation added 2026-11-16.

## Shared output-buffer investigation (November 17, 2026)

The same pinned Hyperlight provider 1.19.0-preview.260822.1 / sandbox .NET API and
PInvoke 0.4.0 expose heap/stack sizes, not a shared output-buffer size.
`WithOutputDir` is a filesystem setting, not a buffer override. Actual public
members/FFI fields and assembly hashes were captured by the committed
`Diagnosis sandbox-capacity` helper.

A supported real-guest offline test prints 32,768 ASCII characters plus newline.
The guest aborts while serializing its result (required 32,856, available 16,376
buffer bytes). The provider catches the error and returns empty stdout, explicit
stderr, exit_code -1, success false to the scripted model client; the host survives
and that client continues. It is not silent truncation.

Canonical Medium/Large oracle reports measure 64,764 / 196,685 UTF-8 bytes.
There is no supported setting for the requested >=393,370-byte buffer, so scaling
controls stop pending an approved SDK update or output-protocol redesign.
No heap/stack hack, dependency change or Medium/Large guest run was attempted.
See the [full investigation, evidence, tests and options](infrastructure-gate-20261117.md).

## Native execution cancellation (November 16, 2026)

Verified against the installed **Microsoft.Agents.AI.Hyperlight
1.19.0-preview.260822.1** net9.0 binary/XML and **Hyperlight.HyperlightSandbox.Api /
PInvoke 0.4.0**, not inferred from newer documentation:

- The provider's `Internal.ExecuteCodeFunction.InvokeCoreAsync` forwards the token to
  `SandboxExecutor.ExecuteAsync`. Decompilation of that exact binary shows the executor using
  it only for `SemaphoreSlim.WaitAsync`, then calling synchronous `Sandbox.Run(code)`.
  Neither the public provider options nor the sandbox API exposes an execution timeout,
  interrupt or kill method.
- The host package's nuspec identifies source commit
  `3d8171e7a9e706da0f2a5837c0bcf2ed942643af`.
  [Sandbox.RunAsync documentation and implementation, lines 339-357](https://github.com/hyperlight-dev/hyperlight-sandbox/blob/3d8171e7a9e706da0f2a5837c0bcf2ed942643af/src/sdk/dotnet/core/Api/Sandbox.cs#L339-L357)
  explicitly state that cancellation only prevents scheduling and cannot cancel in-progress FFI.
  The provider calls `Run`, not this async wrapper.
- [Sandbox.Run, lines 309-336](https://github.com/hyperlight-dev/hyperlight-sandbox/blob/3d8171e7a9e706da0f2a5837c0bcf2ed942643af/src/sdk/dotnet/core/Api/Sandbox.cs#L309-L336)
  and [Dispose, lines 530-545](https://github.com/hyperlight-dev/hyperlight-sandbox/blob/3d8171e7a9e706da0f2a5837c0bcf2ed942643af/src/sdk/dotnet/core/Api/Sandbox.cs#L530-L545)
  acquire the same lock. Disposing from a cancellation callback cannot interrupt an infinite
  run. [SandboxBuilder](https://github.com/hyperlight-dev/hyperlight-sandbox/blob/3d8171e7a9e706da0f2a5837c0bcf2ed942643af/src/sdk/dotnet/core/Api/SandboxBuilder.cs)
  and [FFISandboxOptions](https://github.com/hyperlight-dev/hyperlight-sandbox/blob/3d8171e7a9e706da0f2a5837c0bcf2ed942643af/src/sdk/dotnet/core/PInvoke/SandboxOptions.cs)
  provide memory/backend/filesystem settings, not execution deadlines.

Binary SHA-256 evidence:

| Binary | SHA-256 |
| --- | --- |
| Microsoft.Agents.AI.Hyperlight.dll (net9.0) | `7f69060fb7678e57a2a25cbe55ad14f81c86066570500482215cb8366eaed363` |
| HyperlightSandbox.Api.dll (net8.0) | `e173db75007b53718ced87fe0be94e1f8e56b1ef1a9e830426efce2c8e9938dd` |

On the VM, `while True: pass` exceeded the existing test's five-second cancellation and ran
for over six minutes until the test process was externally terminated. All original assertions
remain. The test additionally carries `HyperlightKnownIssue`; the runtime gate explicitly
excludes that category and requires **five actual passes**, never six.

The approved benchmark workaround is supervised process isolation for both modes, warm-ups
and Classic batches. At the configured per-run wall-time deadline the supervisor kills/reaps
the worker process tree, appends explicit interruption/failure events and saves `TimedOut`,
source and usage. Interrupted final journal bytes are retained separately with an explicit
marker. This does not make SDK cancellation work. It does not protect standalone API hosting.
A real infinite-Python probe using this watchdog was killed/reaped in **5.095 seconds** for a
five-second probe deadline; the benchmark's configured limit remains 120 seconds.

Exact binary decompilation, public source snapshots, hashes and probe logs are retained in the
ignored `artifacts/phase-b-20261116-1721/` evidence directory. No dependency was upgraded.

---

## 1. Environment

| Item | Value | How verified |
| --- | --- | --- |
| Machine | Windows on ARM64 | `dotnet --info` |
| Pre-existing runtime | `Microsoft.NETCore.App 8.0.30` (runtime only, **no SDK**) | `dotnet --list-sdks` returned empty |
| Installed SDK | `.NET SDK 9.0.317` (user-local `%USERPROFILE%\.dotnet`, no admin) | `dotnet-install.ps1` |
| App target framework | `net9.0` | probe restore + build succeeded |
| NuGet feed | internal proxy `<NUGET_FEED_HOST>` (`api.nuget.org` blocked on this host; private hostname redacted) | `dotnet package search` |

The samples in `microsoft/agent-framework` target `net10.0`, but the **published packages
restore and compile cleanly on `net9.0`**, which is the newest SDK installable here without
admin rights. See `decisions.md` ADR-0002.

> **Version note:** the core `Microsoft.Agents.AI` / `.OpenAI` packages have a **stable**
> `1.19.0` release; the `-preview.260822.1` suffix exists **only** for
> `Microsoft.Agents.AI.Hyperlight`. Pinning core to stable `1.19.0` avoids `NU1603`
> nearest-version warnings (which become errors under `TreatWarningsAsErrors`). The Hyperlight
> preview builds correctly alongside the stable core packages (verified via probe).

---

## 2. Verified package versions (pinned centrally)

All versions are pinned in `Directory.Packages.props` (central package management). No
floating versions.

| Package | Version | Notes |
| --- | --- | --- |
| `Microsoft.Agents.AI` | `1.19.0` | Core agent abstractions, `AsAIAgent`, `ChatClientAgentOptions` (stable) |
| `Microsoft.Agents.AI.OpenAI` | `1.19.0` | Azure/OpenAI chat-client bridge (stable) |
| `Microsoft.Agents.AI.Hyperlight` | `1.19.0-preview.260822.1` | **Preview-only** — `HyperlightCodeActProvider`; no stable release exists |
| `Microsoft.Extensions.AI` | `10.9.0` | `AIFunction`, `AIFunctionFactory`, `IChatClient` |
| `Microsoft.Extensions.AI.Abstractions` | `10.9.0` | `ChatOptions`, `ChatMessage`, `AIContent` |
| `Azure.AI.OpenAI` | `2.1.0` | `AzureOpenAIClient` |
| `Azure.Identity` | `1.13.1` | `DefaultAzureCredential` |

Transitive versions observed in the probe lock file (for reference; not pinned directly):
`Microsoft.Extensions.Logging.Abstractions 10.0.11`,
`Microsoft.Extensions.DependencyInjection.Abstractions 10.0.11`,
`Azure.Core 1.44.1`, `System.Text.Json 10.0.11`.

---

## 3. Agent creation (Classic path) — VERIFIED

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

IChatClient chatClient =
    new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetChatClient(deploymentName)
        .AsIChatClient();

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "classic",
    Description = "...",
    ChatOptions = new ChatOptions
    {
        ModelId = deploymentName,
        Instructions = systemPrompt,   // shared, hashed
        Temperature = 0f,              // when supported by deployment
        Tools = tools,                 // IList<AITool> from AIFunctionFactory.Create(...)
        ToolMode = ChatToolMode.Auto
    }
});
```

- Tools are built with `AIFunctionFactory.Create(Delegate, name: ..., description: ...)`.
- The same `IChatClient` abstraction is used by both apps; only the wiring differs.

## 4. CodeAct path — VERIFIED

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hyperlight;

var codeAct = new HyperlightCodeActProvider(
    HyperlightCodeActProviderOptions.CreateForWasm(guestModulePath)
    {
        Tools = providerOwnedTools   // IEnumerable<AIFunction>, the SAME seven tools
    });

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "codeact",
    ChatOptions = new ChatOptions { ModelId = deploymentName, Instructions = systemPrompt, Temperature = 0f },
    AIContextProviders = [codeAct]   // provider injects the execute_code tool
});
```

### `HyperlightCodeActProvider`

- Type: `AIContextProvider`, `IDisposable`. Dispose is required (owns sandbox resources).
- `StateKeys` exposes a single fixed key `"HyperlightCodeActProvider"` → **one provider per
  agent** (cannot register two CodeAct providers on the same agent).

### `HyperlightCodeActProviderOptions`

| Member | Meaning |
| --- | --- |
| `CreateForWasm(modulePath)` | factory for a WASM guest at `modulePath` |
| `CreateForJavaScript()` / `new()` | JavaScript guest (default) |
| `Tools` (`IEnumerable<AIFunction>`) | provider-owned tools, reachable via `call_tool(...)` |
| `FileMounts` | read-only host directory mounts (avoid unless required) |
| `AllowedDomains` | outbound allow-list (empty ⇒ network denied) |
| `HostInputDirectory` / `WorkspaceRoot` | host paths |
| `ApprovalMode` (`CodeActApprovalMode`) | default `NeverRequire` |
| `HeapSize` / `StackSize` | guest memory limits |
| `ModulePath` / `Backend` | guest module + backend selection |
| CRUD helpers | `AddTools/GetTools/RemoveTools/ClearTools`, `AddFileMounts/GetFileMounts`, `AllowedDomains` CRUD |

### `execute_code` tool (model-facing)

- Schema: `{"type":"object","properties":{"code":{"type":"string"}},"required":["code"]}`.
- Returns JSON: `{ stdout, stderr, exit_code, success }`.
- Snapshot/restore gives a **clean sandbox state per `execute_code` call**; a warm snapshot is
  taken after a first no-op init.

### `call_tool(...)` (inside the sandbox)

- Guest code calls `call_tool("<tool_name>", **kwargs)` to reach provider-owned tools.
- The provider marshals the call back to the host `AIFunction` and returns bounded JSON.

### Security defaults

- **Network denied by default**; opened only per `AllowedDomains` allow-list.
- Filesystem mounts are read-only (`FileMounts`); none mounted unless technically required.
- Generated code executes **in the Hyperlight micro-VM, never in the host process**.

---

## 5. Gated integration-test pattern — VERIFIED (`CodeActEndToEndTests`)

> **Version correction (1.19.0):** the `AIContextProvider` base method in this SDK is
> `ProvideAIContextAsync(...)` (there is **no** `InvokingAsync`/`InvokingContext` type as in older
> previews). The Hyperlight assembly ships `lib/net9.0`, `net8.0`, and `net10.0` assets, so a
> `net9.0` app can reference it. Because a Hyperlight guest + hardware virtualization are unavailable
> in this environment, the repository's gated test (`HyperlightRuntimeTests`) verifies **provider
> construction with the seven provider-owned tools and clean disposal**, and **skips with an explicit
> reason** (`Xunit.SkippableFact` → `Skip.If`) when `HYPERLIGHT_PYTHON_GUEST_PATH` is unset. The
> deeper `execute_code`/`call_tool`/isolation/denied-network E2E requires a live guest and follows the
> official pattern below; it is never faked. See `decisions.md` ADR-0013.

```csharp
[SkippableFact]
public void Provider_constructs_and_disposes()
{
    var guest = Environment.GetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH");
    Skip.If(string.IsNullOrWhiteSpace(guest), "HYPERLIGHT_PYTHON_GUEST_PATH not set.");

    var options = HyperlightCodeActProviderOptions.CreateForWasm(guest!);
    options.Tools = providerOwnedTools;      // the SAME seven tools
    using var provider = new HyperlightCodeActProvider(options);
    // With a live guest, drive execute_code via the provider's ProvideAIContextAsync surface.
}
```

This lets Hyperlight tests exercise `execute_code` and `call_tool(...)` **without** a live
model, using `HYPERLIGHT_PYTHON_GUEST_PATH` to locate the guest.

---

## 6. Usage / telemetry metadata — VERIFIED shape

- Responses expose usage via `Microsoft.Extensions.AI` `UsageDetails`
  (`InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`), surfaced on
  `ChatResponse.Usage` / streaming updates.
- **Missing usage must remain `null`/unknown, never coerced to 0** (see benchmark rules).
- OpenTelemetry: the framework emits Activities/metrics; we add our own stable
  `ActivitySource`/`Meter` names in `SterlingVale.Telemetry` rather than relying on internal
  source names.

---

## 7. Preview limitations & risks

1. `Microsoft.Agents.AI.Hyperlight` is **preview-only** (no stable release at pin time).
   API surface may shift; all usage is isolated behind `SterlingVale.AgentShared` adapters.
2. Hyperlight requires hardware virtualization + a guest module path
   (`HYPERLIGHT_PYTHON_GUEST_PATH`). Absent that, CodeAct runtime tests **skip with a reason**;
   they are never faked.
3. Whether the SDK safely exposes the *generated code string* as an artifact is
   version-dependent. When unavailable, `generated-code.txt` is written with an explicit
   "unavailable" marker (never fabricated).
4. "One `execute_code` call" is **not** guaranteed; the harness measures the actual count.
5. `net10.0` samples vs `net9.0` app target — see ADR-0002.

---

## 8. Source references

- `microsoft/agent-framework` (GitHub) — provider, options, `CodeActEndToEndTests`, samples.
- NuGet (internal proxy) — package versions and target frameworks.
- Local probe `codeact-poc/_probe` — restore + compile verification on `net9.0`.
