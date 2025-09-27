# Copilot Coding Agent Instructions for Costellobot

## Repository Overview

**Costellobot** is a .NET 10.0 ASP.NET Core web application that provides GitHub automation services. The application handles GitHub webhooks to automatically approve dependency updates, manage deployments, and perform repository maintenance tasks across multiple GitHub repositories.

**High-level Details:**

- **Size**: several hundred C# files, a handful of TypeScript files
- **Type**: ASP.NET Core web application with microservices architecture using .NET Aspire
- **Languages**: C# (.NET 10.0), TypeScript, PowerShell
- **Frameworks**: ASP.NET Core, .NET Aspire, xUnit, Playwright, webpack, Jest
- **Target Runtime**: .NET 10.0
- **Cloud Platform**: Azure (Service Bus, Blob Storage, Table Storage, Key Vault)
- **Container**: Publishes as container image to Azure Container Registry

## Build and Validation Process

### Prerequisites

- **PowerShell Core 7+** (required for build script)
- **Node.js 24** (for frontend asset compilation)
- **Latest .NET 10 SDK** (automatically installed by build script if missing)

### Core Build Commands

**ALWAYS use the `./build.ps1` script** - it handles .NET SDK installation automatically:

```powershell
# Full build and test (recommended)
./build.ps1

# Build without tests (faster for development)
./build.ps1 -SkipTests

# Run with filter for specific tests
./build.ps1 -TestFilter "ClassName*"
```

**ALWAYS use PowerShell Core (pwsh)** to run PowerShell scripts - it works on macOS, Linux and Windows.

**Build Time**: Full build takes 4-5 minutes including:

- .NET SDK installation (if needed): ~30 seconds
- NuGet package restore: ~10 seconds  
- TypeScript/npm compilation: ~10 seconds
- C# compilation: ~10 seconds
- Test execution: 3-4 minutes

### Frontend Asset Building

**ALWAYS run npm install first** in the `src/Costellobot` directory:

```bash
cd src/Costellobot
npm install
npm run build  # Compiles TypeScript, runs ESLint, Prettier, Jest tests
```

Frontend build includes:

- TypeScript compilation with webpack
- CSS processing and minification
- ESLint linting
- Prettier code formatting
- Jest unit tests
- Output to `src/Costellobot/wwwroot/static/`

### Manual Commands (for troubleshooting)

```bash
# Use local .NET installation (if build.ps1 fails)
PATH=./.dotnet:$PATH DOTNET_ROOT=./.dotnet ./.dotnet/dotnet build

# Run tests manually (after building)
PATH=./.dotnet:$PATH DOTNET_ROOT=./.dotnet ./.dotnet/dotnet test --configuration Release

# Frontend-only commands
cd src/Costellobot
npm run lint      # ESLint only
npm run format    # Prettier + StyleLint
npm test          # Jest tests only
npm run compile   # TypeScript compilation only
```

### Common Build Issues and Workarounds

1. **Test failures due to .NET runtime mismatch**:
   - **Issue**: Tests expect .NET 10.0.0 runtime but may find 9.0.x
   - **Workaround**: Use GitHub Actions environment or ensure proper `DOTNET_ROOT`/`PATH` setup
   - **Solution**: Always use `./build.ps1` which handles environment correctly

2. **Missing TypeScript assets**:
   - **Issue**: `main.js` not found during build
   - **Workaround**: Run `npm install && npm run build` in `src/Costellobot/` first

3. **PowerShell execution policy errors**:
   - **Issue**: Cannot run `build.ps1` on Windows
   - **Solution**: Run `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`

## Project Layout and Architecture

### Directory Structure

```
costellobot/
├── .github/                  # GitHub workflows, templates, configurations
│   ├── workflows/            # CI/CD pipelines (build.yml, lint.yml, etc.)
│   ├── dependabot.yml        # Dependency update configuration
│   └── CONTRIBUTING.md       # Contribution guidelines
├── src/
│   ├── Costellobot/          # Main web application
│   │   ├── Handlers/         # GitHub webhook event handlers
│   │   ├── DeploymentRules/  # Deployment approval logic
│   │   ├── Registries/       # Package registry integrations
│   │   ├── Authorization/    # Authentication/authorization
│   │   ├── Models/           # Data models and DTOs
│   │   ├── scripts/ts/       # TypeScript source files
│   │   ├── styles/           # CSS source files
│   │   ├── wwwroot/          # Static web assets
│   │   ├── Program.cs        # Application entry point
│   │   ├── appsettings.json  # Configuration
│   │   └── package.json      # npm dependencies
│   └── Costellobot.AppHost/  # .NET Aspire orchestration host
├── tests/
│   ├── Costellobot.Tests/         # Unit tests (xUnit and Playwright)
│   ├── Costellobot.EndToEndTests/ # E2E tests (Playwright)
│   └── Costellobot.Oats/          # OATs OpenTelemetry testing
├── perf/Costellobot.Benchmarks/   # BenchmarkDotNet performance tests
├── build.ps1                      # Main build script
├── global.json                    # .NET SDK version specification
├── Costellobot.slnx               # Solution file
├── Directory.Build.props          # MSBuild configuration
├── Directory.Packages.props       # Central package management
└── docker-compose.yml             # Local development services for OATs
```

### Key Configuration Files

- **`global.json`**: Specifies .NET SDK version
- **`Directory.Build.props`**: MSBuild settings, versioning, code analysis
- **`Directory.Packages.props`**: Central package version management
- **`Costellobot.ruleset`**: Code analysis rules
- **`.editorconfig`**: Code formatting and style rules
- **`stylecop.json`**: StyleCop analyzer configuration

### Main Application Architecture

The application follows a clean architecture pattern:

1. **Program.cs**: Application entry point using Minimal APIs
2. **CostellobotBuilder.cs**: Dependency injection and service configuration
3. **GitHubExtensions.cs**: GitHub service registration and client setup
4. **Handlers/**: Event-driven webhook processors for different GitHub events
   - `PullRequestHandler.cs`: PR automation and approval logic
   - `DeploymentStatusHandler.cs`: Deployment approval workflows
   - `CheckSuiteHandler.cs`: CI/CD status handling
   - `IssueCommentHandler.cs`: Issue comment processing
5. **DeploymentRules/**: Business logic for deployment approval decisions
6. **Registries/**: Package registry integrations (NuGet, npm, Docker, etc.)

### Testing Architecture

- **Unit Tests**: `tests/Costellobot.Tests/` using xUnit v3, NSubstitute, Shouldly
- **E2E Tests**: `tests/Costellobot.EndToEndTests/` using Playwright
- **Test Infrastructure**: `Builders/` folder contains test data builders
- **Mocking**: Uses `JustEat.HttpClientInterception` for HTTP mocking

## Continuous Integration and Validation

### GitHub Actions Workflows

1. **`build.yml`**: Main CI/CD pipeline
   - Runs on Windows, Linux, macOS
   - Includes build, test, container publishing, Azure deployment
   - **Timeout**: 20 minutes per job

2. **`lint.yml`**: Code quality checks
   - actionlint, zizmor (workflow security)
   - markdownlint, PSScriptAnalyzer
   - ESLint, Prettier, StyleLint (via npm)

3. **Additional workflows**:
   - `codeql.yml`: Security analysis
   - `dependency-review.yml`: Dependency security scanning
   - `lighthouse.yml`: Web performance testing
   - `container-scan.yml`: Container vulnerability scanning

If changing GitHub Actions workflows **ALWAYS** pin versions.

If adding a new tool that isn't a GitHub Actions, always provide a specific version via the relevant GitHub release and include a `*_VERSION` environment variable to specify the version with a renovate comment above it for automated updates.

### Pre-commit Validation Steps

To replicate CI locally:

```bash
# 1. Full build and test
./build.ps1

# 2. Frontend linting and formatting
cd src/Costellobot
npm run build

# 3. PowerShell script analysis (if modifying .ps1 files)
Invoke-ScriptAnalyzer -Path . -Recurse -Settings @{IncludeDefaultRules=$true}
```

### Dependencies and Package Management

- **NuGet**: Central package management via `Directory.Packages.props`
- **npm**: Frontend dependencies in `src/Costellobot/package.json`
- **Dependabot**: Yearly updates configured in `.github/dependabot.yml`
- **Renovate**: Configured in `.github/renovate.json`
- **Trusted Dependencies**: Extensive allow-list in `appsettings.json` under `TrustedEntities`

## Key Implementation Patterns

### Webhook Handler Pattern

All GitHub webhook handlers implement `IHandler` interface:

```csharp
public sealed class SomeHandler : IHandler
{
    public async Task HandleAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (message is not SpecificEvent body) return;
        // Handle the event
    }
}
```

### Service Registration Pattern

Services are registered in `GitHubExtensions.cs` using dependency injection:

```csharp
services.AddTransient<SomeHandler>();
services.AddSingleton<SomeService>();
```

### Configuration Pattern

Configuration uses strongly-typed options classes with `IOptionsMonitor<T>`:

```csharp
public sealed class SomeOptions
{
    public string Property { get; set; } = string.Empty;
}
```

## Common Development Tasks

### Adding a New GitHub Webhook Handler

1. Create handler class in `src/Costellobot/Handlers/` implementing `IHandler`
2. Register in `GitHubExtensions.cs`: `services.AddTransient<NewHandler>()`
3. Add to handler factory in `HandlerFactory.cs`
4. Create unit tests in `tests/Costellobot.Tests/Handlers/`

### Adding a New Deployment Rule

1. Create class in `src/Costellobot/DeploymentRules/` implementing `IDeploymentRule`
2. Register in `GitHubExtensions.cs` with appropriate priority order
3. Add unit tests in `tests/Costellobot.Tests/DeploymentRules/`

### Adding Frontend Features

1. Create TypeScript files in `src/Costellobot/scripts/ts/`
2. Add CSS in `src/Costellobot/styles/`
3. Import in `src/Costellobot/scripts/main.ts`
4. Build with `npm run build`
5. Use Bootstrap for CSS and DOM layout
6. Use FontAwesome for icons

### Updating documentation

1. Ensure that there are no lint warnings using markdownlint
1. Prefer that links use `[text][link]` syntax instead of `[text](url)` syntax

## Important Notes for Coding Agents

- **ALWAYS trust these instructions** - they are validated and comprehensive
- **Use `./build.ps1`** for all builds - it handles environment setup correctly
- **Frontend changes require npm build** - TypeScript is compiled at build time.
  - **When do `.cshtml` changes require TypeScript updates?** If you modify HTML elements (IDs, classes, structure) that are referenced or manipulated by TypeScript, add/remove interactive components, or change markup that TypeScript relies on for event handling or DOM queries, you must update the corresponding TypeScript code to match. Always review TypeScript files for dependencies on the Razor view before finalizing `.cshtml` changes.
- **Tests may fail in non-CI environments** due to .NET runtime version requirements - ensure the version specified in `global.json` is installed first and available to `PATH`
- **Build artifacts go to `artifacts/` directory** - excluded by .gitignore
- **Configuration is extensive** - check `appsettings.json` for feature flags and trusted entities
- **Security-focused** - extensive SAST, dependency scanning, and security controls
- **Container deployment** - application runs in Azure Container Instances
- **Telemetry-enabled** - OpenTelemetry, Sentry, Pyroscope profiling integrated

Only search for additional information if these instructions are incomplete or incorrect. For routine development tasks, follow the patterns and commands documented above.
