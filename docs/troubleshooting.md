# Troubleshooting

## Troubleshooting

| Symptom | Check/action |
|---|---|
| Azure 404 | Use the supported account base endpoint, not `/openai/v1` or a project URL; verify exact deployment/API mode. Let the adapter append its route. |
| Azure 401/403 | Check actual SDK identity/key, resource-scoped OpenAI User role, metadata-read permissions, propagation and network policy. An inference key alone does not supply management metadata access. |
| Azure configured but fake selected | Safe default: explicitly pass `--model live`; APIs use that flag or `MODEL_MODE=live`. No live-to-fake fallback. |
| Changed variables ignored in VS Code | An existing editor/Remote-SSH server, task or extension host may retain stale environment. Export in the actual launcher; reconnect/restart the relevant host. Inspect non-secret policy values/presence, never tokens or the whole environment. `.env.example` is not auto-loaded. |
| PowerShell surprises | Use `$env:NAME=...`, not Bash `export`; do not paste heredocs or `setsid nohup` into PowerShell. Quote paths/filters; check `$LASTEXITCODE` after native commands. Use `Get-Content -LiteralPath` for bracketed TRX names, resetting XML variables before recounting. Never bypass execution policy. |
| SSH timeout | Verify running/not auto-deallocated VM, current public IP, corporate VPN route and BOTH NSGs. Policy may delete CorpNetPublic allows; recreate only approved rules through an authorized operator. A timeout differs from `Permission denied (publickey)`. |
| SSH key failure | Check SSH user, intended client key and trusted host fingerprint; repository deploy key is separate from VM login authentication. Never copy private keys into the repository. |
| `/dev/kvm` denied | Confirm x64/nested virtualization and `kvm` membership; disconnect all old sessions and reconnect. Do not globally loosen device permissions. |
| Guest not found / skipped tests | Use the absolute bootstrap guest path and validate it in the test shell. Skips are not runtime passes. Exclude only the documented native-cancellation known issue. |
| Sandbox overflow | Inspect required/available bytes and matching result/continuation evidence under the canonical validity rules. Do not invent a buffer setting, drop fields or silently truncate reports. |
| Runner refuses a profile | Check clean committed source, required environment, default reference (or explicit comparison AND sibling metadata), and existing profile/log paths. Never erase/reuse an active/failed attempt. |
| Missing data / API 503 | Generate data first; use absolute roots. API-relative roots resolve under the project content root. |
| VALID but incorrect / INVALID | Read the [canonical validity rules](benchmark-methodology.md#validity-rules) and inspect oracle scores versus infrastructure evidence. Accounting is not analytical success. |
| Controller stops after a profile | Check checker, report generation, commit identity, deploy-key write permission and push result. No next profile should launch after publication failure. |
| Old dashboard/counts disagree | Check comparison/source commit, cohort, scope and execution kind. Historical docs/reports are not automatically regenerated. |
| Locked restore fails | Verify selected SDK and approved package feed; do not regenerate locks merely to hide failure. |

## Remote-SSH server reset

If a new terminal still inherits stale variables after an ordinary reconnect, first save
work and coordinate with anyone sharing that SSH user: a server reset disconnects their
VS Code sessions too. In the local VS Code Command Palette run
**Remote-SSH: Kill VS Code Server on Host...**, select the intended host, then reconnect.
If offered, **Remote-SSH: Uninstall VS Code Server from Host...** is a last-resort
reinstallation after confirming no dependent session or task is active.

Do not recursively delete the user's home, checkout or server directories, or terminate
unrelated processes. A VS Code reset is not proof that a benchmark stopped: check its
recorded PID and completion/publication evidence before starting anything else.
Restore required variables in the actual launcher shell, checking only non-secret policy
values or whether credentials are present. See [running experiments](running-experiments.md)
and [VM networking and access](vm-setup.md).
