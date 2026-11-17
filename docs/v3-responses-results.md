# v3 enforced stateless Responses: cross-model results

See [configuration, pricing, audit and gates](v3-enforced-cohorts.md). Historical cohorts and isolated smoke pairs are excluded.
Five measured pairs per completed scope. Correct = Exact/WithinTolerance. All measured outcomes remain in medians; warm-ups excluded.
Output/total tokens include reasoning once; R = median reasoning, V = median visible output, C = median cached input.
Analysis excludes recorded retry wait; the unchanged 120-second deadline includes waiting. Cost uses per-request Azure retail cache/context rates, not an invoice.
Deltas compare Sol to gpt-4o for the same scope/mode, including all outcomes. These are descriptive, not efficiency wins when answers are not equally correct.

| Cohort | Scope | Mode | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens (R / V / C) | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis vs 4o |
|---|---|---|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---|
| gpt-4o-v3-enforced | n1 | classic | 0/5 | 0 | 5 | 0 | 0 | 0 | 141,077 (0 / 1,179 / 137,856) | 0.1892150 | 67.891 | 0.000 | -- |
| gpt-6.1-sol-v3 | n1 | classic | 5/5 | 0 | 0 | 0 | 0 | 0 | 36,694 (1,949 / 2,244 / 32,733) | 0.0452393 | 94.098 | 0.000 | -74.0% / -76.1% / +38.6% |
| gpt-4o-v3-enforced | n1 | codeact | 2/5 | 3 | 0 | 0 | 0 | 0 | 15,794 (0 / 2,557 / 9,984) | 0.0461825 | 22.908 | 0.000 | -- |
| gpt-6.1-sol-v3 | n1 | codeact | 5/5 | 0 | 0 | 0 | 0 | 0 | 14,874 (29 / 1,787 / 9,957) | 0.0273612 | 33.013 | 0.000 | -5.8% / -40.8% / +44.1% |
| gpt-4o-v3-enforced | n3 | classic | 0/5 | 1 | 4 | 0 | 0 | 0 | 253,431 (0 / 1,161 / 246,144) | 0.3346050 | 65.573 | 0.000 | -- |
| gpt-6.1-sol-v3 | n3 | classic | 0/0 INVALID | 0 | unavailable | unavailable | unavailable | unavailable | unavailable (unavailable / unavailable / unavailable) | unavailable | unavailable | unavailable | unavailable / unavailable / unavailable |
| gpt-4o-v3-enforced | n3 | codeact | 0/5 | 5 | 0 | 0 | 0 | 0 | 7,594 (0 / 723 / 5,888) | 0.0170475 | 14.949 | 0.000 | -- |
| gpt-6.1-sol-v3 | n3 | codeact | 0/0 INVALID | 0 | unavailable | unavailable | unavailable | unavailable | unavailable (unavailable / unavailable / unavailable) | unavailable | unavailable | unavailable | unavailable / unavailable / unavailable |
| gpt-4o-v3-enforced | full-small | classic | 0/5 | 0 | 5 | 0 | 0 | 0 | 422,918 (0 / 2,278 / 415,232) | 0.5553400 | 78.515 | 0.000 | -- |
| gpt-6.1-sol-v3 | full-small | classic | not run | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
| gpt-4o-v3-enforced | full-small | codeact | 0/5 | 4 | 0 | 0 | 0 | 0 | 20,755 (0 / 2,369 / 15,360) | 0.0506150 | 25.173 | 0.000 | -- |
| gpt-6.1-sol-v3 | full-small | codeact | not run | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
