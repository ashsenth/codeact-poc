# Security Policy

Sterling Vale CodeAct is a **reference/benchmark** application that analyzes **synthetic** data
only. It produces synthetic analysis, never investment advice, and processes no real personal or
financial data.

## Reporting a vulnerability

This is a demonstration repository. If you find a security issue, open a private report to the
maintainers rather than a public issue. Do not include secrets or real credentials in reports.

## Handling of secrets

- No secrets are committed. `.env` is git-ignored; only `.env.example` (placeholders) is tracked.
- Azure authentication prefers `DefaultAzureCredential` (managed identity / developer credentials);
  an API key is used only if explicitly provided.
- CI runs a secret scan (gitleaks) and a dependency vulnerability scan on every push/PR.

## Untrusted code execution (CodeAct)

The CodeAct mode executes model-generated code. See [docs/security-model.md](docs/security-model.md)
for the full trust model. In short: generated code runs only inside a Hyperlight micro-VM (never the
host), outbound network is denied, no host filesystem is mounted, each run uses a fresh sandbox, and
the sandbox is disposed after each run.

## Data & telemetry

- All datasets are synthetic and reproducible from a seed.
- Telemetry never records prompts, portfolio data, tool results, secrets, or generated code — only
  non-sensitive identifiers, counts, durations, and hashes.
