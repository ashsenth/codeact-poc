# v3 enforced stateless Responses: results

See [configuration, pricing, audit and gates](v3-enforced-cohorts.md). Historical cohorts and isolated smoke pairs are excluded.
Five measured pairs per completed scope. Correct = Exact/WithinTolerance. All measured outcomes remain in medians; warm-ups excluded.
Output/total tokens include reasoning once; R = median reasoning, V = median visible output, C = median cached input.
Analysis excludes recorded retry wait; the unchanged 120-second deadline includes waiting. Cost uses per-request Azure retail cache/context rates, not an invoice.
Local function invocation is sequential within each response in both modes and APIs, as verified by the parallel-call audit.
An explicit policy fingerprint marker was added after the initial profiles without changing that behavior. Original fingerprints/evidence are retained; profile rows remain separate.
Correctness-equivalent means equal correct counts, not necessarily the same correct pairs or an efficiency win. In particular, equal zero counts do not establish useful performance.

## Primary: within-model CodeAct versus Classic

Every arrow is Classic -> CodeAct. Deltas are (CodeAct / Classic - 1), including every measured outcome.

| Cohort | Scope | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis | Correctness context |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| gpt-4o-v3-enforced | n1 | 0/5 -> 2/5 | 0 -> 3 | 5 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 141,077 -> 15,794 | 0.1892150 -> 0.0461825 | 67.891 -> 22.908 | 0.000 -> 0.000 | -88.8% / -75.6% / -66.3% | not correctness-equivalent: correct counts differ |
| gpt-6.1-sol-v3 | n1 | 5/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 36,694 -> 14,874 | 0.0452393 -> 0.0273612 | 94.098 -> 33.013 | 0.000 -> 0.000 | -59.5% / -39.5% / -64.9% | correctness-equivalent by count (5/5 each) |
| gpt-4o-v3-enforced | n3 | 0/5 -> 0/5 | 1 -> 5 | 4 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 253,431 -> 7,594 | 0.3346050 -> 0.0170475 | 65.573 -> 14.949 | 0.000 -> 0.000 | -97.0% / -94.9% / -77.2% | correctness-equivalent by count (0/5 each); neither mode correct, not an efficiency win |
| gpt-6.1-sol-v3 | n3 | 0/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 1 | 0 -> 0 | 121,483 -> 30,504 | 0.1108500 -> 0.0539617 | 120.028 -> 50.546 | 0.000 -> 0.000 | -74.9% / -51.3% / -57.9% | not correctness-equivalent: correct counts differ |
| gpt-4o-v3-enforced | full-small | 0/5 -> 0/5 | 0 -> 4 | 5 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 422,918 -> 20,755 | 0.5553400 -> 0.0506150 | 78.515 -> 25.173 | 0.000 -> 0.000 | -95.1% / -90.9% / -67.9% | correctness-equivalent by count (0/5 each); neither mode correct, not an efficiency win |
| gpt-6.1-sol-v3 | full-small | 0/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 1 | 0 -> 0 | 81,439 -> 44,606 | 0.0348349 -> 0.1132577 | 120.033 -> 100.498 | 0.000 -> 0.000 | -45.2% / +225.1% / -16.3% | not correctness-equivalent: correct counts differ |

## Secondary: cross-model comparison

Deltas compare Sol to gpt-4o for the same scope/mode, including all outcomes. These are descriptive, not efficiency wins when answers are not equally correct.

| Cohort | Scope | Mode | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens (R / V / C) | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis vs 4o |
|---|---|---|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---|
| gpt-4o-v3-enforced | n1 | classic | 0/5 | 0 | 5 | 0 | 0 | 0 | 141,077 (0 / 1,179 / 137,856) | 0.1892150 | 67.891 | 0.000 | -- |
| gpt-6.1-sol-v3 | n1 | classic | 5/5 | 0 | 0 | 0 | 0 | 0 | 36,694 (1,949 / 2,244 / 32,733) | 0.0452393 | 94.098 | 0.000 | -74.0% / -76.1% / +38.6% |
| gpt-4o-v3-enforced | n1 | codeact | 2/5 | 3 | 0 | 0 | 0 | 0 | 15,794 (0 / 2,557 / 9,984) | 0.0461825 | 22.908 | 0.000 | -- |
| gpt-6.1-sol-v3 | n1 | codeact | 5/5 | 0 | 0 | 0 | 0 | 0 | 14,874 (29 / 1,787 / 9,957) | 0.0273612 | 33.013 | 0.000 | -5.8% / -40.8% / +44.1% |
| gpt-4o-v3-enforced | n3 | classic | 0/5 | 1 | 4 | 0 | 0 | 0 | 253,431 (0 / 1,161 / 246,144) | 0.3346050 | 65.573 | 0.000 | -- |
| gpt-6.1-sol-v3 | n3 | classic | 0/5 | 0 | 0 | 0 | 0 | 0 | 121,483 (102 / 6,020 / 100,003) | 0.1108500 | 120.028 | 0.000 | -52.1% / -66.9% / +83.0% |
| gpt-4o-v3-enforced | n3 | codeact | 0/5 | 5 | 0 | 0 | 0 | 0 | 7,594 (0 / 723 / 5,888) | 0.0170475 | 14.949 | 0.000 | -- |
| gpt-6.1-sol-v3 | n3 | codeact | 5/5 | 0 | 0 | 0 | 1 | 0 | 30,504 (52 / 3,527 / 18,659) | 0.0539617 | 50.546 | 0.000 | +301.7% / +216.5% / +238.1% |
| gpt-4o-v3-enforced | full-small | classic | 0/5 | 0 | 5 | 0 | 0 | 0 | 422,918 (0 / 2,278 / 415,232) | 0.5553400 | 78.515 | 0.000 | -- |
| gpt-6.1-sol-v3 | full-small | classic | 0/5 | 0 | 0 | 0 | 0 | 0 | 81,439 (209 / 1,874 / 76,824) | 0.0348349 | 120.033 | 0.000 | -80.7% / -93.7% / +52.9% |
| gpt-4o-v3-enforced | full-small | codeact | 0/5 | 4 | 0 | 0 | 0 | 0 | 20,755 (0 / 2,369 / 15,360) | 0.0506150 | 25.173 | 0.000 | -- |
| gpt-6.1-sol-v3 | full-small | codeact | 5/5 | 0 | 0 | 0 | 1 | 0 | 44,606 (81 / 7,645 / 23,302) | 0.1132577 | 100.498 | 0.000 | +114.9% / +123.8% / +299.2% |
