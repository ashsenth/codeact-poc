# Command results (audit)
```
== dotnet --info ==
.NET SDK:
 Version:           9.0.317
 Commit:            26570c2743
 Workload version:  9.0.300-manifests.be694c2a
 MSBuild version:   17.14.51+25f168cee

Runtime Environment:
 OS Name:     Windows
 OS Version:  10.0.26200
 OS Platform: Windows
 RID:         win-arm64
 Base Path:   C:\Users\ashwinse\.dotnet\sdk\9.0.317\

.NET workloads installed:
There are no installed workloads to display.
Configured to use loose manifests when installing new manifests.

Host:
  Version:      9.0.19
  Architecture: arm64
  Commit:       8381bdb01f

.NET SDKs installed:
  9.0.317 [C:\Users\ashwinse\.dotnet\sdk]

.NET runtimes installed:
  Microsoft.AspNetCore.App 9.0.19 [C:\Users\ashwinse\.dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 9.0.19 [C:\Users\ashwinse\.dotnet\shared\Microsoft.NETCore.App]
  Microsoft.WindowsDesktop.App 9.0.19 [C:\Users\ashwinse\.dotnet\shared\Microsoft.WindowsDesktop.App]

Other architectures found:
  None

Environment variables:
  DOTNET_ROOT       [C:\Users\ashwinse\.dotnet]

global.json file:
  C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\global.json

Learn more:
  https://aka.ms/dotnet/info

Download .NET:
  https://aka.ms/dotnet/download
== dotnet restore --locked-mode ==
  Determining projects to restore...
  All projects are up-to-date for restore.
== dotnet build -c Release --no-restore ==
  SterlingVale.Telemetry -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Telemetry\bin\Release\net9.0\SterlingVale.Telemetry.dll
  SterlingVale.Domain -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Domain\bin\Release\net9.0\SterlingVale.Domain.dll
  SterlingVale.Application -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Application\bin\Release\net9.0\SterlingVale.Application.dll
  SterlingVale.Domain.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Domain.Tests\bin\Release\net9.0\SterlingVale.Domain.Tests.dll
  SterlingVale.DataGenerator -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.DataGenerator\bin\Release\net9.0\SterlingVale.DataGenerator.dll
  SterlingVale.Tools -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Tools\bin\Release\net9.0\SterlingVale.Tools.dll
  SterlingVale.Data.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Data.Tests\bin\Release\net9.0\SterlingVale.Data.Tests.dll
  SterlingVale.Application.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Application.Tests\bin\Release\net9.0\SterlingVale.Application.Tests.dll
  SterlingVale.AgentShared -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.AgentShared\bin\Release\net9.0\SterlingVale.AgentShared.dll
  SterlingVale.Tools.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Tools.Tests\bin\Release\net9.0\SterlingVale.Tools.Tests.dll
  SterlingVale.Api.Shared -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Api.Shared\bin\Release\net9.0\SterlingVale.Api.Shared.dll
  SterlingVale.Infrastructure -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Infrastructure\bin\Release\net9.0\SterlingVale.Infrastructure.dll
  SterlingVale.CodeAct -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.CodeAct\bin\Release\net9.0\SterlingVale.CodeAct.dll
  SterlingVale.AgentShared.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.AgentShared.Tests\bin\Release\net9.0\SterlingVale.AgentShared.Tests.dll
  SterlingVale.Benchmark -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Benchmark\bin\Release\net9.0\SterlingVale.Benchmark.dll
  SterlingVale.Architecture.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Architecture.Tests\bin\Release\net9.0\SterlingVale.Architecture.Tests.dll
  SterlingVale.Classic.Api -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.Classic.Api\bin\Release\net9.0\SterlingVale.Classic.Api.dll
  SterlingVale.Contract.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Contract.Tests\bin\Release\net9.0\SterlingVale.Contract.Tests.dll
  SterlingVale.CodeAct.Api -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\src\SterlingVale.CodeAct.Api\bin\Release\net9.0\SterlingVale.CodeAct.Api.dll
  SterlingVale.Comparison.Tests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Comparison.Tests\bin\Release\net9.0\SterlingVale.Comparison.Tests.dll
  SterlingVale.Classic.IntegrationTests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Classic.IntegrationTests\bin\Release\net9.0\SterlingVale.Classic.IntegrationTests.dll
  SterlingVale.CodeAct.IntegrationTests -> C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.CodeAct.IntegrationTests\bin\Release\net9.0\SterlingVale.CodeAct.IntegrationTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.46
== dotnet format --verify-no-changes ==
== dotnet test -c Release --no-build ==
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Classic.IntegrationTests\bin\Release\net9.0\SterlingVale.Classic.IntegrationTests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Domain.Tests\bin\Release\net9.0\SterlingVale.Domain.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Data.Tests\bin\Release\net9.0\SterlingVale.Data.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Tools.Tests\bin\Release\net9.0\SterlingVale.Tools.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Application.Tests\bin\Release\net9.0\SterlingVale.Application.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.CodeAct.IntegrationTests\bin\Release\net9.0\SterlingVale.CodeAct.IntegrationTests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.AgentShared.Tests\bin\Release\net9.0\SterlingVale.AgentShared.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Contract.Tests\bin\Release\net9.0\SterlingVale.Contract.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Comparison.Tests\bin\Release\net9.0\SterlingVale.Comparison.Tests.dll (.NETCoreApp,Version=v9.0)
Test run for C:\Users\ashwinse\OneDrive - Microsoft\Desktop\CodeAct\codeact-poc\tests\SterlingVale.Architecture.Tests\bin\Release\net9.0\SterlingVale.Architecture.Tests.dll (.NETCoreApp,Version=v9.0)
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20, Duration: 188 ms - SterlingVale.Domain.Tests.dll (net9.0)

Passed!  - Failed:     0, Passed:    23, Skipped:     0, Total:    23, Duration: 713 ms - SterlingVale.Tools.Tests.dll (net9.0)

Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27, Duration: 450 ms - SterlingVale.Application.Tests.dll (net9.0)

Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27, Duration: 1 s - SterlingVale.AgentShared.Tests.dll (net9.0)

Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 1 s - SterlingVale.Data.Tests.dll (net9.0)

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 1 s - SterlingVale.Contract.Tests.dll (net9.0)
[xUnit.net 00:00:03.62]     SterlingVale.CodeAct.IntegrationTests.HyperlightRuntimeTests.Provider_constructs_with_provider_owned_tools_and_disposes [SKIP]

Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 1 s - SterlingVale.Architecture.Tests.dll (net9.0)
[xUnit.net 00:00:03.86]     SterlingVale.Classic.IntegrationTests.ClassicLiveTests.Live_classic_analysis_completes [SKIP]
[xUnit.net 00:00:03.97]     SterlingVale.CodeAct.IntegrationTests.HyperlightEndToEndTests.Analysis_runs_through_real_sandbox [SKIP]
[xUnit.net 00:00:03.97]     SterlingVale.CodeAct.IntegrationTests.CodeActLiveTests.Live_codeact_analysis_completes [SKIP]
[xUnit.net 00:00:03.97]     SterlingVale.CodeAct.IntegrationTests.HyperlightEndToEndTests.Sandbox_state_is_isolated_between_runs [SKIP]

Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 1 s - SterlingVale.Comparison.Tests.dll (net9.0)
  Skipped SterlingVale.CodeAct.IntegrationTests.HyperlightRuntimeTests.Provider_constructs_with_provider_owned_tools_and_disposes [1 ms]
  Skipped SterlingVale.CodeAct.IntegrationTests.HyperlightEndToEndTests.Analysis_runs_through_real_sandbox [1 ms]
  Skipped SterlingVale.CodeAct.IntegrationTests.CodeActLiveTests.Live_codeact_analysis_completes [1 ms]
  Skipped SterlingVale.CodeAct.IntegrationTests.HyperlightEndToEndTests.Sandbox_state_is_isolated_between_runs [1 ms]
  Skipped SterlingVale.Classic.IntegrationTests.ClassicLiveTests.Live_classic_analysis_completes [1 ms]

Passed!  - Failed:     0, Passed:     9, Skipped:     1, Total:    10, Duration: 1 s - SterlingVale.Classic.IntegrationTests.dll (net9.0)

Passed!  - Failed:     0, Passed:     7, Skipped:     4, Total:    11, Duration: 1 s - SterlingVale.CodeAct.IntegrationTests.dll (net9.0)
== dotnet list package --vulnerable --include-transitive ==

The following sources were used:
   https://packagefeedproxy.microsoft.io/nuget/v3/index.json

The given project `SterlingVale.Domain` has no vulnerable packages given the current sources.
The given project `SterlingVale.DataGenerator` has no vulnerable packages given the current sources.
The given project `SterlingVale.Domain.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Data.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Application` has no vulnerable packages given the current sources.
The given project `SterlingVale.Application.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Telemetry` has no vulnerable packages given the current sources.
The given project `SterlingVale.Tools` has no vulnerable packages given the current sources.
The given project `SterlingVale.Tools.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.AgentShared` has no vulnerable packages given the current sources.
The given project `SterlingVale.AgentShared.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Infrastructure` has no vulnerable packages given the current sources.
The given project `SterlingVale.Classic.Api` has no vulnerable packages given the current sources.
The given project `SterlingVale.Classic.IntegrationTests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Api.Shared` has no vulnerable packages given the current sources.
The given project `SterlingVale.CodeAct.Api` has no vulnerable packages given the current sources.
The given project `SterlingVale.CodeAct.IntegrationTests` has no vulnerable packages given the current sources.
The given project `SterlingVale.CodeAct` has no vulnerable packages given the current sources.
The given project `SterlingVale.Benchmark` has no vulnerable packages given the current sources.
The given project `SterlingVale.Comparison.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Architecture.Tests` has no vulnerable packages given the current sources.
The given project `SterlingVale.Contract.Tests` has no vulnerable packages given the current sources.
== dotnet list package --outdated ==

The following sources were used:
   https://packagefeedproxy.microsoft.io/nuget/v3/index.json

The given project `SterlingVale.Domain` has no updates given the current sources.
The given project `SterlingVale.DataGenerator` has no updates given the current sources.
Project `SterlingVale.Domain.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

Project `SterlingVale.Data.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

The given project `SterlingVale.Application` has no updates given the current sources.
Project `SterlingVale.Application.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

Project `SterlingVale.Telemetry` has the following updates to its packages
   [net9.0]: 
   Top-level Package                          Requested   Resolved   Latest 
   > System.Diagnostics.DiagnosticSource      9.0.0       9.0.0      10.0.11

The given project `SterlingVale.Tools` has no updates given the current sources.
Project `SterlingVale.Tools.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

Project `SterlingVale.AgentShared` has the following updates to its packages
   [net9.0]: 
   Top-level Package          Requested   Resolved   Latest
   > JsonSchema.Net           7.3.0       7.3.0      9.4.0 
   > Microsoft.Agents.AI      1.19.0      1.19.0     1.20.0

Project `SterlingVale.AgentShared.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.Agents.AI            1.19.0      1.19.0     1.20.0
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

Project `SterlingVale.Infrastructure` has the following updates to its packages
   [net9.0]: 
   Top-level Package                 Requested   Resolved   Latest
   > Azure.Identity                  1.13.1      1.13.1     1.21.0
   > Microsoft.Agents.AI.OpenAI      1.19.0      1.19.0     1.20.0

The given project `SterlingVale.Classic.Api` has no updates given the current sources.
Project `SterlingVale.Classic.IntegrationTests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                       Requested   Resolved   Latest 
   > coverlet.collector                    6.0.2       6.0.2      10.0.1 
   > Microsoft.AspNetCore.Mvc.Testing      9.0.0       9.0.0      10.0.11
   > Microsoft.NET.Test.Sdk                17.11.1     17.11.1    18.9.0 
   > xunit                                 2.9.2       2.9.2      2.9.3  
   > xunit.runner.visualstudio             2.8.2       2.8.2      4.0.0  
   > Xunit.SkippableFact                   1.5.23      1.5.23     1.5.85 

The given project `SterlingVale.Api.Shared` has no updates given the current sources.
The given project `SterlingVale.CodeAct.Api` has no updates given the current sources.
Project `SterlingVale.CodeAct.IntegrationTests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                       Requested                 Resolved                  Latest                  
   > coverlet.collector                    6.0.2                     6.0.2                     10.0.1                  
   > Microsoft.Agents.AI.Hyperlight        1.19.0-preview.260822.1   1.19.0-preview.260822.1   Not found at the sources
   > Microsoft.AspNetCore.Mvc.Testing      9.0.0                     9.0.0                     10.0.11                 
   > Microsoft.NET.Test.Sdk                17.11.1                   17.11.1                   18.9.0                  
   > xunit                                 2.9.2                     2.9.2                     2.9.3                   
   > xunit.runner.visualstudio             2.8.2                     2.8.2                     4.0.0                   
   > Xunit.SkippableFact                   1.5.23                    1.5.23                    1.5.85                  

Project `SterlingVale.CodeAct` has the following updates to its packages
   [net9.0]: 
   Top-level Package                     Requested                 Resolved                  Latest                  
   > Microsoft.Agents.AI                 1.19.0                    1.19.0                    1.20.0                  
   > Microsoft.Agents.AI.Hyperlight      1.19.0-preview.260822.1   1.19.0-preview.260822.1   Not found at the sources

The given project `SterlingVale.Benchmark` has no updates given the current sources.
Project `SterlingVale.Comparison.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

Project `SterlingVale.Architecture.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

Project `SterlingVale.Contract.Tests` has the following updates to its packages
   [net9.0]: 
   Top-level Package                Requested   Resolved   Latest
   > coverlet.collector             6.0.2       6.0.2      10.0.1
   > Microsoft.NET.Test.Sdk         17.11.1     17.11.1    18.9.0
   > xunit                          2.9.2       2.9.2      2.9.3 
   > xunit.runner.visualstudio      2.8.2       2.8.2      4.0.0 

```

## Summary of executed commands (verified)

| Command | Result |
| --- | --- |
| dotnet --info | .NET SDK 9.0.317, win-arm64, AspNetCore/NETCore 9.0.19 |
| dotnet restore --locked-mode | exit 0 (lock files consistent) |
| dotnet build -c Release --no-restore | Build succeeded — 0 Warning(s), 0 Error(s) |
| dotnet format --verify-no-changes | exit 0 (no changes) |
| dotnet test -c Release --no-build | 146 passed, 5 skipped, 0 failed (10 test projects) |
| dotnet list package --vulnerable --include-transitive | no vulnerable packages (all 22 projects) |
| dotnet list package --outdated | exit 0 |

Skipped tests (all gated by design): ClassicLiveTests.Live_classic_analysis_completes,
CodeActLiveTests.Live_codeact_analysis_completes, HyperlightRuntimeTests, HyperlightEndToEndTests (x2).

Not executed in this environment (recorded, not fabricated): live-model runs (no Azure creds /
ENABLE_LIVE_TESTS unset), Hyperlight runtime (no guest / virtualization), gitleaks + coverage
(CI-only actions).
