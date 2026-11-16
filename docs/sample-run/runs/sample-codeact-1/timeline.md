# Codeact full-Small pair 1: condensed recorded timeline

Derived from captured evidence, not a replay. Original ordering, sequence numbers and UTC timestamps are retained.
The 2824 granular tool invocations' individual TOOL_CALL_* rows are omitted for size; the [invocation excerpt](tool-invocations.jsonl) contains 36 first-unique H0001 input retrievals.
Invocation identifiers, traces, correlations and configuration payloads are omitted. Gaps in sequence numbers are intentional.

| Source sequence | UTC timestamp | Event | Status | Duration ms | Code reference |
|---:|---|---|---|---:|---|
| 1 | 2026-11-16T18:42:04.485882+00:00 | RUN_STARTED | ok |  |  |
| 2 | 2026-11-16T18:42:04.502489+00:00 | EXECUTION_CONFIGURATION | ok |  |  |
| 3 | 2026-11-16T18:42:04.5705656+00:00 | SANDBOX_CONFIGURATION | ok |  |  |
| 4 | 2026-11-16T18:42:04.6205826+00:00 | MODEL_REQUEST_STARTED | ok |  |  |
| 5 | 2026-11-16T18:42:09.0419666+00:00 | MODEL_RESPONSE_RECEIVED | ok | 4421.172 |  |
| 6 | 2026-11-16T18:42:09.0477216+00:00 | MODEL_USAGE_RECORDED | ok |  |  |
| 7 | 2026-11-16T18:42:09.060364+00:00 | MODEL_TOOL_CALL_STARTED | ok |  | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 8 | 2026-11-16T18:42:09.063359+00:00 | CODE_GENERATED | ok |  | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 9 | 2026-11-16T18:42:09.06667+00:00 | EXECUTE_CODE_CALL_STARTED | ok |  | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 10 | 2026-11-16T18:42:09.0670305+00:00 | CODE_EXECUTION_STARTED | ok |  | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 1847 | 2026-11-16T18:42:09.8625606+00:00 | CODE_EXECUTION_STDOUT | ok |  |  |
| 1848 | 2026-11-16T18:42:09.863627+00:00 | CODE_EXECUTION_STDERR | ok |  |  |
| 1849 | 2026-11-16T18:42:09.8639321+00:00 | MODEL_TOOL_CALL_COMPLETED | ok |  | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 1850 | 2026-11-16T18:42:09.8698639+00:00 | EXECUTE_CODE_CALL_COMPLETED | ok |  | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 1851 | 2026-11-16T18:42:09.8701239+00:00 | CODE_EXECUTION_FAILED | failed | 796.531 | [codeact/generated-code-001.py](codeact/generated-code-001.py) |
| 1852 | 2026-11-16T18:42:09.8745091+00:00 | MODEL_REQUEST_STARTED | ok |  |  |
| 1853 | 2026-11-16T18:42:13.9055057+00:00 | MODEL_RESPONSE_RECEIVED | ok | 4030.736 |  |
| 1854 | 2026-11-16T18:42:13.9072436+00:00 | MODEL_USAGE_RECORDED | ok |  |  |
| 1855 | 2026-11-16T18:42:13.9090665+00:00 | MODEL_TOOL_CALL_STARTED | ok |  | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 1856 | 2026-11-16T18:42:13.9107062+00:00 | CODE_GENERATED | ok |  | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 1857 | 2026-11-16T18:42:13.9110034+00:00 | EXECUTE_CODE_CALL_STARTED | ok |  | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 1858 | 2026-11-16T18:42:13.9112275+00:00 | CODE_EXECUTION_STARTED | ok |  | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 3695 | 2026-11-16T18:42:14.3483169+00:00 | CODE_EXECUTION_STDOUT | ok |  |  |
| 3696 | 2026-11-16T18:42:14.348599+00:00 | CODE_EXECUTION_STDERR | ok |  |  |
| 3697 | 2026-11-16T18:42:14.3486813+00:00 | MODEL_TOOL_CALL_COMPLETED | ok |  | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 3698 | 2026-11-16T18:42:14.3491416+00:00 | EXECUTE_CODE_CALL_COMPLETED | ok |  | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 3699 | 2026-11-16T18:42:14.3492674+00:00 | CODE_EXECUTION_COMPLETED | returned | 437.242 | [codeact/generated-code-002.py](codeact/generated-code-002.py) |
| 3700 | 2026-11-16T18:42:14.3501278+00:00 | MODEL_REQUEST_STARTED | ok |  |  |
| 3701 | 2026-11-16T18:42:17.8899012+00:00 | MODEL_RESPONSE_RECEIVED | ok | 3539.572 |  |
| 3702 | 2026-11-16T18:42:17.8900436+00:00 | MODEL_USAGE_RECORDED | ok |  |  |
| 3703 | 2026-11-16T18:42:17.8902489+00:00 | MODEL_TOOL_CALL_STARTED | ok |  | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 3704 | 2026-11-16T18:42:17.8907349+00:00 | CODE_GENERATED | ok |  | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 3705 | 2026-11-16T18:42:17.8908199+00:00 | EXECUTE_CODE_CALL_STARTED | ok |  | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 3706 | 2026-11-16T18:42:17.8908982+00:00 | CODE_EXECUTION_STARTED | ok |  | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 5681 | 2026-11-16T18:42:18.0726002+00:00 | CODE_EXECUTION_STDOUT | ok |  |  |
| 5682 | 2026-11-16T18:42:18.0727532+00:00 | CODE_EXECUTION_STDERR | ok |  |  |
| 5683 | 2026-11-16T18:42:18.0727896+00:00 | MODEL_TOOL_CALL_COMPLETED | ok |  | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 5684 | 2026-11-16T18:42:18.0731465+00:00 | EXECUTE_CODE_CALL_COMPLETED | ok |  | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 5685 | 2026-11-16T18:42:18.0732+00:00 | CODE_EXECUTION_COMPLETED | returned | 181.738 | [codeact/generated-code-003.py](codeact/generated-code-003.py) |
| 5686 | 2026-11-16T18:42:18.0736254+00:00 | MODEL_REQUEST_STARTED | ok |  |  |
| 5687 | 2026-11-16T18:42:21.4144145+00:00 | MODEL_RESPONSE_RECEIVED | ok | 3340.650 |  |
| 5688 | 2026-11-16T18:42:21.4145063+00:00 | MODEL_USAGE_RECORDED | ok |  |  |
| 5689 | 2026-11-16T18:42:21.4146307+00:00 | MODEL_TOOL_CALL_STARTED | ok |  | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5690 | 2026-11-16T18:42:21.4151622+00:00 | CODE_GENERATED | ok |  | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5691 | 2026-11-16T18:42:21.4152461+00:00 | EXECUTE_CODE_CALL_STARTED | ok |  | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5692 | 2026-11-16T18:42:21.4153212+00:00 | CODE_EXECUTION_STARTED | ok |  | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5695 | 2026-11-16T18:42:21.4217421+00:00 | CODE_EXECUTION_STDOUT | ok |  |  |
| 5696 | 2026-11-16T18:42:21.421909+00:00 | CODE_EXECUTION_STDERR | ok |  |  |
| 5697 | 2026-11-16T18:42:21.4219983+00:00 | MODEL_TOOL_CALL_COMPLETED | ok |  | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5698 | 2026-11-16T18:42:21.4222595+00:00 | EXECUTE_CODE_CALL_COMPLETED | ok |  | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5699 | 2026-11-16T18:42:21.4222917+00:00 | CODE_EXECUTION_COMPLETED | returned | 6.583 | [codeact/generated-code-004.py](codeact/generated-code-004.py) |
| 5700 | 2026-11-16T18:42:21.4228742+00:00 | MODEL_REQUEST_STARTED | ok |  |  |
| 5701 | 2026-11-16T18:42:24.9083426+00:00 | MODEL_RESPONSE_RECEIVED | ok | 3485.227 |  |
| 5702 | 2026-11-16T18:42:24.908478+00:00 | MODEL_USAGE_RECORDED | ok |  |  |
| 5703 | 2026-11-16T18:42:25.0177638+00:00 | TELEMETRY_RECONCILIATION_COMPLETED | ok |  |  |
| 5704 | 2026-11-16T18:42:25.1024672+00:00 | JSON_VALIDATION_COMPLETED | ok |  |  |
| 5705 | 2026-11-16T18:42:25.1028475+00:00 | RUN_COMPLETED | Completed | 20329.038 |  |

Execution 001 returned success=false, exitCode=1; captured stderr: `NameError: name 'json' is not defined`.
Executions 002-004 returned success=true, exitCode=0. The final slice 10:15 is empty for ten households.
All four actual programs are retained unchanged below codeact/. An execution success is not an oracle pass.
