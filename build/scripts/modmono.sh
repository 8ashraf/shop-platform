#!/usr/bin/env bash
#
# modmono.sh — scaffold and grow a .NET Modular Monolith.
#
# Commands:
#   new <SolutionName> [options]     Create a new modular-monolith repository
#   add-module <ModuleName> [opts]   Add a module to an existing repository
#   remove-module <ModuleName> [opts] Remove a module and everything add-module created
#   list-modules [--root <path>]     List modules in an existing repository
#
# Examples:
#   ./modmono.sh new Ecommerce                                  # empty platform, no modules
#   ./modmono.sh new RP.Shop --modules Catalog,Orders --org rital-tech
#   cd ecommerce && ./build/scripts/modmono.sh add-module Shipping
#   ./build/scripts/modmono.sh remove-module Shipping
#
# Layout produced:
#   <repo>/
#   ├── docs/{business,requirements,architecture,adr}
#   ├── src/
#   │   ├── Modules/<Module>/<Sln>.Modules.<Module>.{Domain,Application,Infrastructure,Presentation,IntegrationEvents}
#   │   ├── Shared/<Sln>.Shared.{Kernel,Application,Infrastructure}
#   │   └── Host/<Sln>.Host
#   ├── tests/
#   │   ├── Modules/<Module>/<Sln>.Modules.<Module>.UnitTests
#   │   ├── <Sln>.ArchitectureTests
#   │   └── <Sln>.IntegrationTests
#   ├── deploy/{docker,k8s}
#   └── .github/{workflows,CODEOWNERS}
#

set -euo pipefail

SCRIPT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
CONFIG_FILE=".modmono"

# ---------- Helpers ----------
log()  { printf "\033[1;34m==>\033[0m %s\n" "$*"; }
ok()   { printf "\033[1;32m✓\033[0m %s\n" "$*"; }
warn() { printf "\033[1;33m!\033[0m %s\n" "$*"; }
err()  { printf "\033[1;31m✗\033[0m %s\n" "$*" >&2; }
die()  { err "$*"; exit 1; }

lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }

# Template tokens (replaced by render):
#   __SOLUTION__  __SOLUTION_LOWER__  __MODULE__  __MODULE_LOWER__  __ORG__  __DOTNET_VERSION__  __FRAMEWORK__
SOLUTION=""; SOLUTION_LOWER=""; MODULE=""; MODULE_LOWER=""; ORG=""; FRAMEWORK=""; DOTNET_VERSION=""

render() {
    sed -e "s|__SOLUTION_LOWER__|${SOLUTION_LOWER}|g" \
        -e "s|__SOLUTION__|${SOLUTION}|g" \
        -e "s|__MODULE_LOWER__|${MODULE_LOWER}|g" \
        -e "s|__MODULE__|${MODULE}|g" \
        -e "s|__ORG__|${ORG}|g" \
        -e "s|__DOTNET_VERSION__|${DOTNET_VERSION}|g" \
        -e "s|__FRAMEWORK__|${FRAMEWORK}|g"
}

# emit <path>  — renders stdin into <path>, creating parent directories.
emit() {
    mkdir -p "$(dirname "$1")"
    render > "$1"
}

# insert_before_marker <file> <marker> <line>  — idempotent line insertion.
insert_before_marker() {
    python3 - "$1" "$2" "$3" <<'PY'
import sys, pathlib
path, marker, line = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3]
text = path.read_text()
if line.strip() in (l.strip() for l in text.splitlines()):
    sys.exit(0)
out, done = [], False
for l in text.splitlines(keepends=True):
    if not done and l.strip() == marker.strip():
        out.append(line + "\n")
        done = True
    out.append(l)
if not done:
    sys.exit(f"marker '{marker}' not found in {path}")
path.write_text("".join(out))
PY
}

# remove_lines_containing <file> <text>  — drops every line that contains <text>.
remove_lines_containing() {
    [[ -f "$1" ]] || return 0
    python3 - "$1" "$2" <<'PY2'
import sys, pathlib
path, needle = pathlib.Path(sys.argv[1]), sys.argv[2]
lines = path.read_text().splitlines(keepends=True)
path.write_text("".join(l for l in lines if needle not in l))
PY2
}

validate_solution_name() {
    [[ "$1" =~ ^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$ ]] \
        || die "Invalid solution name '$1'. Use PascalCase segments separated by dots, e.g. Ecommerce or RP.Shop"
}

validate_module_name() {
    [[ "$1" =~ ^[A-Z][A-Za-z0-9]*$ ]] \
        || die "Invalid module name '$1'. Use a single PascalCase word, e.g. Catalog, Shipping, Inventory"
    case "$1" in
        Shared|Host|Modules|Tests) die "'$1' is reserved and cannot be a module name." ;;
    esac
}

require_dotnet() {
    command -v dotnet >/dev/null 2>&1 || die "dotnet SDK not found in PATH."
}

# Package versions per target framework (edit Directory.Packages.props later to bump).
set_package_versions() {
    local major="${FRAMEWORK#net}"; major="${major%%.*}"
    case "$major" in
        8)  ASPNET_VER="8.0.11";  EF_VER="8.0.11";  SWASHBUCKLE_VER="6.9.0"; PKCS_VER="8.0.2" ;;
        9)  ASPNET_VER="9.0.0";   EF_VER="9.0.0";   SWASHBUCKLE_VER="7.2.0"; PKCS_VER="9.0.21" ;;
        10) ASPNET_VER="10.0.0";  EF_VER="10.0.13";  SWASHBUCKLE_VER="7.2.0"; PKCS_VER="10.0.13" ;;
        *)  die "Unsupported framework '$FRAMEWORK'. Use net8.0, net9.0 or net10.0." ;;
    esac
    DOTNET_VERSION="${major}.0"
}

# Locate the repository root by walking up from a directory to find $CONFIG_FILE.
find_root() {
    local dir
    dir="$(cd "$1" && pwd)"
    while [[ "$dir" != "/" ]]; do
        [[ -f "$dir/$CONFIG_FILE" ]] && { printf '%s' "$dir"; return 0; }
        dir="$(dirname "$dir")"
    done
    return 1
}

load_config() {
    local root="$1" key value
    while IFS='=' read -r key value; do
        case "$key" in
            SOLUTION)  SOLUTION="$value" ;;
            FRAMEWORK) FRAMEWORK="$value" ;;
            ORG)       ORG="$value" ;;
            SLN_FILE)  SLN_FILE="$value" ;;
        esac
    done < "$root/$CONFIG_FILE"
    [[ -n "$SOLUTION" && -n "$FRAMEWORK" && -n "$SLN_FILE" ]] || die "Corrupt $CONFIG_FILE in $root"
    SOLUTION_LOWER="$(lower "$SOLUTION")"
    set_package_versions
}

resolve_root() {
    local explicit="$1" root=""
    if [[ -n "$explicit" ]]; then
        root="$(find_root "$explicit")" || die "No $CONFIG_FILE found at or above $explicit"
    else
        root="$(find_root "$PWD" || find_root "$(dirname "$SCRIPT_PATH")")" \
            || die "Not inside a modular-monolith repo (no $CONFIG_FILE found). Use --root <path>."
    fi
    printf '%s' "$root"
}

usage() {
    cat <<EOF
Usage:
  $(basename "$0") new <SolutionName> [options]
  $(basename "$0") add-module <ModuleName> [--root <path>] [--no-build]
  $(basename "$0") remove-module <ModuleName> [--root <path>] [--yes] [--force] [--no-build]
  $(basename "$0") list-modules [--root <path>]

new options:
  --modules <A,B,C>    Modules to create right away (default: none — add later with add-module)
  --framework <tfm>    net8.0 | net9.0 | net10.0 (default: newest your SDK supports)
  --output <path>      Parent directory (default: .)
  --dir <name>         Repository folder name (default: solution name in lower case)
  --org <github-org>   GitHub org used in CODEOWNERS (default: your-org)
  --no-git             Skip git init and the initial commit
  --no-build           Skip restore/build at the end

add-module options:
  --root <path>        Repository root (default: auto-detected from the current directory)
  --no-build           Skip build after adding the module

remove-module options:
  --root <path>        Repository root (default: auto-detected)
  --yes, -y            Do not ask for confirmation
  --force              Remove even if other modules reference its IntegrationEvents
  --no-build           Skip build after removing the module

A copy of this script is placed at build/scripts/modmono.sh in the new repo,
so the team can run: ./build/scripts/modmono.sh add-module Shipping
EOF
}

# =====================================================================
#  Root-level files
# =====================================================================
write_root_files() {
    log "Writing root configuration"

    cat > global.json <<EOF
{
  "sdk": {
    "version": "$(dotnet --version)",
    "rollForward": "latestFeature"
  }
}
EOF

    cat > Directory.Build.props <<EOF
<Project>
  <PropertyGroup>
    <TargetFramework>$FRAMEWORK</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <!-- NuGet vulnerability audit stays a warning so a new CVE does not break every build -->
    <WarningsNotAsErrors>\$(WarningsNotAsErrors);NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest</AnalysisLevel>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>\$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <PropertyGroup Condition="\$(MSBuildProjectName.EndsWith('Tests'))">
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
EOF

    cat > Directory.Packages.props <<EOF
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <!-- Lets the "Security pins" below override vulnerable transitive dependencies -->
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!-- Host -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="$SWASHBUCKLE_VER" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="$EF_VER" />

    <!-- Persistence (one DbContext + schema per module) -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="$EF_VER" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />

    <!-- Tests -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="$ASPNET_VER" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>

  <ItemGroup>
    <!-- Security pins for transitive dependencies.
         Find new ones with: dotnet list package, using the vulnerable and include-transitive options (see README) -->
    <!-- GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7 (pulled in by EF Core / SqlClient) -->
    <PackageVersion Include="System.Security.Cryptography.Pkcs" Version="$PKCS_VER" />
  </ItemGroup>
</Project>
EOF

    cat > .editorconfig <<'EOF'
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{json,yml,yaml,xml,csproj,props,targets,slnx}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.cs]
csharp_new_line_before_open_brace = all
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
dotnet_sort_system_directives_first = true
EOF

    cat > .gitignore <<'EOF'
# Build
[Bb]in/
[Oo]bj/
[Oo]ut/
build/publish/
*.user
*.suo
.vs/

# IDE (keep the shared launch config)
.idea/
.vscode/*
!.vscode/launch.json
!.vscode/tasks.json

# Test results
[Tt]est[Rr]esult*/
coverage*/
*.trx
*.coverage

# NuGet
*.nupkg
*.snupkg
.nuget/
packages/

# OS
.DS_Store
Thumbs.db

# Secrets / env
.env
*.env
appsettings.*.local.json
secrets.json
EOF

    cat > .dockerignore <<'EOF'
**/bin
**/obj
**/out
.vs/
.idea/
.vscode/
.git/
.github/
docs/
tests/
build/publish/
**/*.user
**/*.suo
**/.DS_Store
EOF

    mkdir -p .vscode
    emit .vscode/launch.json <<'EOF'
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "__SOLUTION__.Host (swagger)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Host/__SOLUTION__.Host/bin/Debug/__FRAMEWORK__/__SOLUTION__.Host.dll",
      "cwd": "${workspaceFolder}/src/Host/__SOLUTION__.Host",
      "launchSettingsFilePath": "${workspaceFolder}/src/Host/__SOLUTION__.Host/Properties/launchSettings.json",
      "launchSettingsProfile": "swagger",
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
        "uriFormat": "%s/swagger"
      }
    },
    {
      "name": "__SOLUTION__.Host (https)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Host/__SOLUTION__.Host/bin/Debug/__FRAMEWORK__/__SOLUTION__.Host.dll",
      "cwd": "${workspaceFolder}/src/Host/__SOLUTION__.Host",
      "launchSettingsFilePath": "${workspaceFolder}/src/Host/__SOLUTION__.Host/Properties/launchSettings.json",
      "launchSettingsProfile": "https"
    },
    {
      "name": "__SOLUTION__.Host (http)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Host/__SOLUTION__.Host/bin/Debug/__FRAMEWORK__/__SOLUTION__.Host.dll",
      "cwd": "${workspaceFolder}/src/Host/__SOLUTION__.Host",
      "launchSettingsFilePath": "${workspaceFolder}/src/Host/__SOLUTION__.Host/Properties/launchSettings.json",
      "launchSettingsProfile": "http"
    }
  ]
}
EOF

    emit .vscode/tasks.json <<'EOF'
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/src/Host/__SOLUTION__.Host/__SOLUTION__.Host.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile",
      "group": { "kind": "build", "isDefault": true }
    },
    {
      "label": "test",
      "command": "dotnet",
      "type": "process",
      "args": ["test", "${workspaceFolder}"],
      "problemMatcher": "$msCompile",
      "group": "test"
    },
    {
      "label": "watch",
      "command": "dotnet",
      "type": "process",
      "args": ["watch", "run", "--project", "${workspaceFolder}/src/Host/__SOLUTION__.Host/__SOLUTION__.Host.csproj"],
      "problemMatcher": "$msCompile",
      "isBackground": true
    }
  ]
}
EOF

    emit README.md <<'EOF'
# __SOLUTION__

Modular monolith on .NET __DOTNET_VERSION__. One deployable (`__SOLUTION__.Host`), many
independently-owned modules with hard boundaries enforced by architecture tests.

## Layout

```
.
├── docs/
│   ├── business/          BRD
│   ├── requirements/      SRD, use cases
│   ├── architecture/      C4, DFDs, ERDs (Mermaid), one page per module
│   └── adr/               Architecture Decision Records
├── src/
│   ├── Modules/<Module>/  Domain · Application · Infrastructure · Presentation · IntegrationEvents
│   ├── Shared/            Kernel · Application · Infrastructure (building blocks)
│   └── Host/              API host / composition root
├── tests/
│   ├── Modules/<Module>/  per-module unit tests
│   ├── __SOLUTION__.ArchitectureTests
│   └── __SOLUTION__.IntegrationTests
├── deploy/                docker, compose, k8s
└── .github/               path-filtered CI per module, CODEOWNERS
```

## Module rules

Inside a module:

| Layer | May reference |
|---|---|
| Domain | Shared.Kernel |
| Application | Domain, own IntegrationEvents, Shared.Application |
| Presentation | Application |
| Infrastructure | Application, Presentation, IntegrationEvents, Shared.Infrastructure (registers the module via `IModule`) |
| IntegrationEvents | Shared.Kernel only — this is the module's **public contract** |

Between modules: a module may reference **only another module's `IntegrationEvents`
project**. Anything else fails `__SOLUTION__.ArchitectureTests`.

Each module owns its own database schema (`catalog`, `orders`, …) and its own `DbContext`
and migrations history table.

## Adding a module

```bash
./build/scripts/modmono.sh add-module Shipping
```

This creates the five module projects, a unit-test project, a docs page, a path-filtered
CI workflow and a CODEOWNERS entry; adds everything to the solution; and registers the
module in `src/Host/__SOLUTION__.Host/ModuleCatalog.cs`.

## Commands

```bash
dotnet build
dotnet test
dotnet run --project src/Host/__SOLUTION__.Host          # http://localhost:5080/swagger
docker compose -f deploy/docker-compose.yml up --build

# Vulnerable packages (pin fixes in the "Security pins" group of Directory.Packages.props)
dotnet list package --vulnerable --include-transitive

# Migrations for one module
dotnet ef migrations add Initial \
  --project src/Modules/Catalog/__SOLUTION__.Modules.Catalog.Infrastructure \
  --startup-project src/Host/__SOLUTION__.Host \
  --output-dir Database/Migrations
```
EOF
}

# =====================================================================
#  Shared building blocks
# =====================================================================
write_shared() {
    log "Writing shared building blocks"
    local kernel="src/Shared/$SOLUTION.Shared.Kernel"
    local app="src/Shared/$SOLUTION.Shared.Application"
    local infra="src/Shared/$SOLUTION.Shared.Infrastructure"

    # ---- Kernel ----
    emit "$kernel/$SOLUTION.Shared.Kernel.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
</Project>
EOF
    emit "$kernel/Entity.cs" <<'EOF'
namespace __SOLUTION__.Shared.Kernel;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
EOF
    emit "$kernel/DomainEvent.cs" <<'EOF'
namespace __SOLUTION__.Shared.Kernel;

public interface IDomainEvent
{
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
EOF
    emit "$kernel/IntegrationEvent.cs" <<'EOF'
namespace __SOLUTION__.Shared.Kernel;

/// <summary>
/// Base type for events that cross module boundaries.
/// Concrete events live in each module's IntegrationEvents project.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
EOF
    emit "$kernel/Result.cs" <<'EOF'
namespace __SOLUTION__.Shared.Kernel;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if ((isSuccess && error != Error.None) || (!isSuccess && error == Error.None))
        {
            throw new ArgumentException("Invalid error for result state.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");
}
EOF

    # ---- Application ----
    emit "$app/$SOLUTION.Shared.Application.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../__SOLUTION__.Shared.Kernel/__SOLUTION__.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$app/Messaging.cs" <<'EOF'
using __SOLUTION__.Shared.Kernel;

namespace __SOLUTION__.Shared.Application;

/// <summary>Publishes integration events to other modules.</summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}

/// <summary>Handles an integration event published by another (or the same) module.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
EOF
    emit "$app/IDateTimeProvider.cs" <<'EOF'
namespace __SOLUTION__.Shared.Application;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
EOF

    # ---- Infrastructure ----
    emit "$infra/$SOLUTION.Shared.Infrastructure.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../__SOLUTION__.Shared.Application/__SOLUTION__.Shared.Application.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$infra/IModule.cs" <<'EOF'
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace __SOLUTION__.Shared.Infrastructure;

/// <summary>
/// Entry point of a module. The Host only knows modules through this interface.
/// </summary>
public interface IModule
{
    string Name { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
EOF
    emit "$infra/ModuleExtensions.cs" <<'EOF'
using System.Reflection;
using __SOLUTION__.Shared.Application;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace __SOLUTION__.Shared.Infrastructure;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModules(
        this IEndpointRouteBuilder endpoints,
        IReadOnlyCollection<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    /// <summary>Registers every IIntegrationEventHandler&lt;T&gt; found in the assembly.</summary>
    public static IServiceCollection AddIntegrationEventHandlers(
        this IServiceCollection services,
        Assembly assembly)
    {
        var handlerType = typeof(IIntegrationEventHandler<>);

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var contract in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType))
            {
                services.AddScoped(contract, type);
            }
        }

        return services;
    }
}
EOF
    emit "$infra/InMemoryEventBus.cs" <<'EOF'
using __SOLUTION__.Shared.Application;
using __SOLUTION__.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace __SOLUTION__.Shared.Infrastructure;

/// <summary>
/// In-process event bus. Swap for an outbox + broker when a module is extracted.
/// </summary>
internal sealed class InMemoryEventBus(IServiceScopeFactory scopeFactory) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
EOF
    emit "$infra/DependencyInjection.cs" <<'EOF'
using __SOLUTION__.Shared.Application;
using Microsoft.Extensions.DependencyInjection;

namespace __SOLUTION__.Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        return services;
    }
}
EOF
}

# =====================================================================
#  Host (composition root)
# =====================================================================
write_host() {
    log "Writing host"
    local host="src/Host/$SOLUTION.Host"

    emit "$host/$SOLUTION.Host.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../Shared/__SOLUTION__.Shared.Infrastructure/__SOLUTION__.Shared.Infrastructure.csproj" />
    <!-- modules -->
  </ItemGroup>
</Project>
EOF

    emit "$host/ModuleCatalog.cs" <<'EOF'
// <module-usings>
// </module-usings>
using __SOLUTION__.Shared.Infrastructure;

namespace __SOLUTION__.Host;

/// <summary>
/// Every module loaded by the host. Maintained by build/scripts/modmono.sh add-module.
/// Comment a line out to switch a module off.
/// </summary>
public static class ModuleCatalog
{
    public static IModule[] All =>
    [
        // <modules>
        // </modules>
    ];
}
EOF

    emit "$host/Program.cs" <<'EOF'
using __SOLUTION__.Host;
using __SOLUTION__.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var modules = ModuleCatalog.All;

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSharedInfrastructure();
builder.Services.AddModules(builder.Configuration, modules);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.MapHealthChecks("/health");
app.MapModules(modules);

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program
{
}
EOF

    emit "$host/appsettings.json" <<'EOF'
{
  "ConnectionStrings": {
    "Database": "Server=localhost,1433;Database=__SOLUTION__;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
EOF
    emit "$host/appsettings.Development.json" <<'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
EOF
    emit "$host/Properties/launchSettings.json" <<'EOF'
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" },
      "dotnetRunMessages": true
    },
    "https": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7080;http://localhost:5080",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" },
      "dotnetRunMessages": true
    },
    "swagger": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:7080;http://localhost:5080",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" },
      "dotnetRunMessages": true
    }
  }
}
EOF
}

# =====================================================================
#  Solution-wide tests
# =====================================================================
write_global_tests() {
    log "Writing architecture & integration tests"
    local arch="tests/$SOLUTION.ArchitectureTests"
    local integ="tests/$SOLUTION.IntegrationTests"

    emit "$arch/$SOLUTION.ArchitectureTests.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="NetArchTest.Rules" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Shared/__SOLUTION__.Shared.Infrastructure/__SOLUTION__.Shared.Infrastructure.csproj" />
    <!-- modules -->
  </ItemGroup>
</Project>
EOF
    emit "$arch/ModuleBoundaryTests.cs" <<'EOF'
using System.Reflection;
using NetArchTest.Rules;

namespace __SOLUTION__.ArchitectureTests;

/// <summary>
/// Discovers every module from the assemblies copied into the test output, so
/// modules added with modmono.sh are covered automatically.
/// </summary>
public class ModuleBoundaryTests
{
    private const string ModulesRoot = "__SOLUTION__.Modules";
    private static readonly string[] InternalLayers = ["Domain", "Application", "Infrastructure", "Presentation"];

    /// <summary>Empty string = "no modules yet"; xUnit fails a theory that has no data.</summary>
    private const string NoModules = "";

    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();
        foreach (var module in DiscoverModules())
        {
            data.Add(module);
        }

        if (data.Count == 0)
        {
            data.Add(NoModules);
        }

        return data;
    }

    [Fact]
    public void Shared_building_blocks_do_not_depend_on_modules()
    {
        var shared = new[] { "Kernel", "Application", "Infrastructure" }
            .Select(name => Assembly.Load($"__SOLUTION__.Shared.{name}"))
            .ToArray();

        var result = Types.InAssemblies(shared)
            .ShouldNot()
            .HaveDependencyOn(ModulesRoot)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_does_not_depend_on_other_modules_internals(string module)
    {
        if (module == NoModules)
        {
            return;
        }

        var forbidden = DiscoverModules()
            .Where(other => other != module)
            .SelectMany(other => InternalLayers.Select(layer => $"{ModulesRoot}.{other}.{layer}"))
            .ToArray();

        if (forbidden.Length == 0)
        {
            return;
        }

        var result = Types.InAssemblies(LoadModule(module, [.. InternalLayers, "IntegrationEvents"]))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_does_not_depend_on_outer_layers(string module) =>
        AssertNoDependency(module, "Domain", "Application", "Infrastructure", "Presentation");

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_does_not_depend_on_infrastructure_or_presentation(string module) =>
        AssertNoDependency(module, "Application", "Infrastructure", "Presentation");

    [Theory]
    [MemberData(nameof(Modules))]
    public void Presentation_does_not_depend_on_infrastructure(string module) =>
        AssertNoDependency(module, "Presentation", "Infrastructure");

    [Theory]
    [MemberData(nameof(Modules))]
    public void IntegrationEvents_only_depend_on_shared_kernel(string module) =>
        AssertNoDependency(module, "IntegrationEvents", "Domain", "Application", "Infrastructure", "Presentation");

    private static void AssertNoDependency(string module, string layer, params string[] forbiddenLayers)
    {
        if (module == NoModules)
        {
            return;
        }

        var result = Types.InAssembly(Assembly.Load($"{ModulesRoot}.{module}.{layer}"))
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenLayers.Select(l => $"{ModulesRoot}.{module}.{l}").ToArray())
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static Assembly[] LoadModule(string module, IEnumerable<string> layers) =>
        layers.Select(layer => Assembly.Load($"{ModulesRoot}.{module}.{layer}")).ToArray();

    private static List<string> DiscoverModules() =>
        Directory.GetFiles(AppContext.BaseDirectory, $"{ModulesRoot}.*.Domain.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Select(name => name[(ModulesRoot.Length + 1)..^".Domain".Length])
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
EOF

    emit "$integ/$SOLUTION.IntegrationTests.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Host/__SOLUTION__.Host/__SOLUTION__.Host.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$integ/HostSmokeTests.cs" <<'EOF'
using System.Net;
using __SOLUTION__.Host;
using Microsoft.AspNetCore.Mvc.Testing;

namespace __SOLUTION__.IntegrationTests;

public class HostSmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var module in ModuleCatalog.All)
        {
            data.Add(module.Name);
        }

        if (data.Count == 0)
        {
            data.Add(string.Empty); // no modules yet; xUnit fails a theory that has no data
        }

        return data;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public async Task Every_module_answers_ping(string module)
    {
        if (module.Length == 0)
        {
            return;
        }

        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/{module.ToLowerInvariant()}/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
EOF
}

# =====================================================================
#  Docs
# =====================================================================
write_docs() {
    log "Writing docs"

    emit docs/README.md <<'EOF'
# __SOLUTION__ documentation

| Folder | Contents |
|---|---|
| [business/](business/) | Business Requirements Document (BRD) |
| [requirements/](requirements/) | Software Requirements (SRD) and use cases |
| [architecture/](architecture/) | C4 diagrams, DFDs, ERDs, one page per module |
| [adr/](adr/) | Architecture Decision Records |

Diagrams are written in Mermaid so they render on GitHub and diff in pull requests.
EOF

    emit docs/business/BRD.md <<'EOF'
# Business Requirements Document — __SOLUTION__

| Version | Date | Author | Status |
|---|---|---|---|
| 0.1 | | | Draft |

## 1. Purpose and background
## 2. Business objectives and success metrics
## 3. Scope
### 3.1 In scope
### 3.2 Out of scope
## 4. Stakeholders
| Role | Name | Interest |
|---|---|---|
## 5. Business requirements
| ID | Requirement | Priority | Owner module |
|---|---|---|---|
| BR-001 | | Must | |
## 6. Assumptions and constraints
## 7. Regulatory and compliance considerations
## 8. Glossary
EOF

    emit docs/requirements/SRD.md <<'EOF'
# Software Requirements Document — __SOLUTION__

## 1. Introduction
## 2. Overall description
## 3. Functional requirements
| ID | Requirement | Module | Traces to |
|---|---|---|---|
| FR-001 | | | BR-001 |
## 4. Non-functional requirements
| ID | Category | Requirement |
|---|---|---|
| NFR-001 | Performance | |
| NFR-002 | Security | |
| NFR-003 | Availability | |
## 5. External interfaces
## 6. Use cases
See [use-cases/](use-cases/).
EOF

    emit docs/requirements/use-cases/UC-000-template.md <<'EOF'
# UC-000: <Use case name>

| Field | Value |
|---|---|
| Module | |
| Primary actor | |
| Preconditions | |
| Postconditions | |
| Traces to | FR-xxx |

## Main flow
1.

## Alternative flows

## Exceptions
EOF

    emit docs/architecture/README.md <<'EOF'
# Architecture

- [C4 — System context](c4-context.md)
- [C4 — Containers](c4-container.md)
- [DFD — Level 0](dfd-level-0.md)
- [Modules](modules/README.md) — one page per module, each with its ERD
EOF

    emit docs/architecture/c4-context.md <<'EOF'
# C4 — System context

```mermaid
C4Context
  title System context — __SOLUTION__
  Person(customer, "Customer", "Browses and buys products")
  Person(merchant, "Merchant", "Manages catalog and orders")
  System(platform, "__SOLUTION__", "Modular monolith")
  System_Ext(psp, "Payment provider", "Processes payments")
  System_Ext(notify, "SMS / Email", "Notifications")

  Rel(customer, platform, "Uses", "HTTPS")
  Rel(merchant, platform, "Uses", "HTTPS")
  Rel(platform, psp, "Charges / refunds", "HTTPS")
  Rel(platform, notify, "Sends", "HTTPS")
```
EOF

    emit docs/architecture/c4-container.md <<'EOF'
# C4 — Containers

```mermaid
C4Container
  title Containers — __SOLUTION__
  Person(user, "User")
  System_Boundary(sys, "__SOLUTION__") {
    Container(host, "__SOLUTION__.Host", "ASP.NET Core", "Single deployable; loads every module via IModule")
    ContainerDb(db, "SQL Server", "One schema per module")
  }
  Rel(user, host, "HTTPS / JSON")
  Rel(host, db, "EF Core")
```

Modules inside the host are listed in [modules/README.md](modules/README.md).
EOF

    emit docs/architecture/dfd-level-0.md <<'EOF'
# DFD — Level 0

```mermaid
flowchart LR
  Customer([Customer]) -- order request --> P((__SOLUTION__))
  P -- order confirmation --> Customer
  Merchant([Merchant]) -- catalog updates --> P
  P -- payment request --> PSP([Payment provider])
  PSP -- payment result --> P
  P <--> DS[(Data stores)]
```
EOF

    emit docs/architecture/modules/README.md <<'EOF'
# Modules

| Module | Schema | Route prefix | Page |
|---|---|---|---|
EOF

    emit docs/adr/README.md <<'EOF'
# Architecture Decision Records

Copy [0000-template.md](0000-template.md) to the next number. Never edit an accepted ADR —
supersede it with a new one.

| # | Title | Status |
|---|---|---|
| 0001 | [Record architecture decisions](0001-record-architecture-decisions.md) | Accepted |
| 0002 | [Adopt a modular monolith](0002-adopt-modular-monolith.md) | Accepted |
| 0003 | [Schema per module, in-process integration events](0003-schema-per-module-and-integration-events.md) | Accepted |
EOF

    emit docs/adr/0000-template.md <<'EOF'
# NNNN. <Title>

- Status: Proposed | Accepted | Superseded by NNNN
- Date: YYYY-MM-DD
- Deciders:

## Context

## Decision

## Consequences
EOF

    emit docs/adr/0001-record-architecture-decisions.md <<'EOF'
# 0001. Record architecture decisions

- Status: Accepted

## Context
Decisions get lost in chat threads and meetings.

## Decision
Record significant decisions as ADRs in `docs/adr`, reviewed through pull requests.

## Consequences
New team members can read why the system looks the way it does.
EOF

    emit docs/adr/0002-adopt-modular-monolith.md <<'EOF'
# 0002. Adopt a modular monolith

- Status: Accepted

## Context
We need clear business boundaries and team ownership without the operational cost of
microservices at this stage.

## Decision
Ship one deployable (`__SOLUTION__.Host`) composed of modules under `src/Modules`.
Each module has Domain, Application, Infrastructure, Presentation and IntegrationEvents
projects. Modules talk to each other only through IntegrationEvents. Boundaries are
enforced by `__SOLUTION__.ArchitectureTests`.

## Consequences
- Single deployment and transaction boundary; simple local development.
- A module can be extracted into a service later by replacing the in-memory event bus.
- Discipline is required: the architecture tests must stay green.
EOF

    emit docs/adr/0003-schema-per-module-and-integration-events.md <<'EOF'
# 0003. Schema per module, in-process integration events

- Status: Accepted

## Context
Shared tables create hidden coupling between modules.

## Decision
Each module owns a SQL Server schema named after it, its own `DbContext` and its own
migrations history table. No module queries another module's schema. Cross-module
communication uses `IEventBus` with in-process delivery for now.

## Consequences
- Modules can evolve their data independently.
- Reporting across modules needs read models or a reporting store.
- Delivery is not durable yet; adopt an outbox before relying on events for money movement.
EOF
}

# =====================================================================
#  Deploy
# =====================================================================
write_deploy() {
    log "Writing deploy assets"

    emit deploy/docker/Dockerfile <<'EOF'
# syntax=docker/dockerfile:1
# Build from the repository root:
#   docker build -f deploy/docker/Dockerfile -t __SOLUTION_LOWER__:dev .
ARG DOTNET_VERSION=__DOTNET_VERSION__

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /source

# Props first for better layer caching (global.json intentionally not copied:
# the image SDK version may differ from the developer pin).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/

RUN dotnet restore "src/Host/__SOLUTION__.Host/__SOLUTION__.Host.csproj"
RUN dotnet publish "src/Host/__SOLUTION__.Host/__SOLUTION__.Host.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "__SOLUTION__.Host.dll"]
EOF

    emit deploy/docker-compose.yml <<'EOF'
name: __SOLUTION_LOWER__

services:
  host:
    build:
      context: ..
      dockerfile: deploy/docker/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Database: "Server=sqlserver,1433;Database=__SOLUTION__;User Id=sa;Password=${MSSQL_SA_PASSWORD:-Your_strong_Passw0rd};TrustServerCertificate=True"
    depends_on:
      - sqlserver

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:-Your_strong_Passw0rd}"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
EOF

    emit deploy/k8s/kustomization.yaml <<'EOF'
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: __SOLUTION_LOWER__
resources:
  - namespace.yaml
  - deployment.yaml
  - service.yaml
EOF
    emit deploy/k8s/namespace.yaml <<'EOF'
apiVersion: v1
kind: Namespace
metadata:
  name: __SOLUTION_LOWER__
EOF
    emit deploy/k8s/deployment.yaml <<'EOF'
apiVersion: apps/v1
kind: Deployment
metadata:
  name: __SOLUTION_LOWER__-host
  labels: { app: __SOLUTION_LOWER__-host }
spec:
  replicas: 2
  selector:
    matchLabels: { app: __SOLUTION_LOWER__-host }
  template:
    metadata:
      labels: { app: __SOLUTION_LOWER__-host }
    spec:
      containers:
        - name: host
          image: ghcr.io/__ORG__/__SOLUTION_LOWER__:latest
          ports:
            - containerPort: 8080
          env:
            - name: ConnectionStrings__Database
              valueFrom:
                secretKeyRef:
                  name: __SOLUTION_LOWER__-db   # kubectl create secret generic __SOLUTION_LOWER__-db --from-literal=connection='...'
                  key: connection
          readinessProbe:
            httpGet: { path: /health, port: 8080 }
          livenessProbe:
            httpGet: { path: /health, port: 8080 }
          resources:
            requests: { cpu: 100m, memory: 256Mi }
            limits: { cpu: "1", memory: 512Mi }
EOF
    emit deploy/k8s/service.yaml <<'EOF'
apiVersion: v1
kind: Service
metadata:
  name: __SOLUTION_LOWER__-host
spec:
  selector: { app: __SOLUTION_LOWER__-host }
  ports:
    - port: 80
      targetPort: 8080
EOF
}

# =====================================================================
#  GitHub: CI + CODEOWNERS
# =====================================================================
write_github() {
    log "Writing GitHub workflows and CODEOWNERS"

    emit .github/workflows/_module-ci.yml <<'EOF'
# Reusable workflow: build and test a single module.
name: module-ci

on:
  workflow_call:
    inputs:
      module:
        required: true
        type: string

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Build module
        run: dotnet build "src/Modules/${{ inputs.module }}/__SOLUTION__.Modules.${{ inputs.module }}.Infrastructure" -c Release
      - name: Unit tests
        run: dotnet test "tests/Modules/${{ inputs.module }}/__SOLUTION__.Modules.${{ inputs.module }}.UnitTests" -c Release --collect:"XPlat Code Coverage"
      - name: Architecture tests (module boundaries)
        run: dotnet test "tests/__SOLUTION__.ArchitectureTests" -c Release
EOF

    emit .github/workflows/ci.yml <<'EOF'
# Platform CI: shared code, host, root config — anything that can affect every module.
# Always runs on main so the whole solution is verified after each merge.
name: platform

on:
  push:
    branches: [main]
  pull_request:
    paths:
      - 'src/Shared/**'
      - 'src/Host/**'
      - 'tests/__SOLUTION__.ArchitectureTests/**'
      - 'tests/__SOLUTION__.IntegrationTests/**'
      - 'Directory.Build.props'
      - 'Directory.Packages.props'
      - 'global.json'
      - '*.sln'
      - '*.slnx'
      - 'deploy/**'
      - '.github/workflows/ci.yml'
      - '.github/workflows/_module-ci.yml'

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: dotnet build -c Release
      - run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage"

  docker:
    if: github.ref == 'refs/heads/main'
    needs: build-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker build -f deploy/docker/Dockerfile -t __SOLUTION_LOWER__:${{ github.sha }} .
EOF

    emit .github/CODEOWNERS <<'EOF'
# Last matching pattern wins. Replace @__ORG__/... with real teams or users.
*                                @__ORG__/architects
/docs/                           @__ORG__/architects
/src/Shared/                     @__ORG__/architects
/src/Host/                       @__ORG__/architects
/tests/__SOLUTION__.ArchitectureTests/  @__ORG__/architects
/deploy/                         @__ORG__/devops
/.github/                        @__ORG__/devops

# ---- Modules (appended by modmono.sh add-module) ----
EOF

    emit .github/pull_request_template.md <<'EOF'
## What & why

## Module(s) touched

## Checklist
- [ ] Architecture tests pass (no cross-module references except IntegrationEvents)
- [ ] Migrations added for schema changes
- [ ] Docs / ADR updated if behaviour or a decision changed
EOF
}

# =====================================================================
#  Module
# =====================================================================
add_module_files() {
    local mod_dir="src/Modules/$MODULE"
    local test_dir="tests/Modules/$MODULE"
    local P="$SOLUTION.Modules.$MODULE"
    local shared="../../../Shared"

    [[ -e "$mod_dir" ]] && die "Module '$MODULE' already exists at $mod_dir"

    log "Adding module $MODULE"

    # ---- IntegrationEvents (public contract) ----
    emit "$mod_dir/$P.IntegrationEvents/$P.IntegrationEvents.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Public contract of the $MODULE module. Other modules may reference ONLY this project. -->
  <ItemGroup>
    <ProjectReference Include="$shared/$SOLUTION.Shared.Kernel/$SOLUTION.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$mod_dir/$P.IntegrationEvents/AssemblyReference.cs" <<'EOF'
using System.Reflection;

namespace __SOLUTION__.Modules.__MODULE__.IntegrationEvents;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}

// Example:
// public sealed record __MODULE__ItemCreatedIntegrationEvent(Guid ItemId) : __SOLUTION__.Shared.Kernel.IntegrationEvent;
EOF

    # ---- Domain ----
    emit "$mod_dir/$P.Domain/$P.Domain.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="$shared/$SOLUTION.Shared.Kernel/$SOLUTION.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$mod_dir/$P.Domain/AssemblyReference.cs" <<'EOF'
using System.Reflection;

namespace __SOLUTION__.Modules.__MODULE__.Domain;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
EOF

    # ---- Application ----
    emit "$mod_dir/$P.Application/$P.Application.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../$P.Domain/$P.Domain.csproj" />
    <ProjectReference Include="../$P.IntegrationEvents/$P.IntegrationEvents.csproj" />
    <ProjectReference Include="$shared/$SOLUTION.Shared.Application/$SOLUTION.Shared.Application.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$mod_dir/$P.Application/AssemblyReference.cs" <<'EOF'
using System.Reflection;

namespace __SOLUTION__.Modules.__MODULE__.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
EOF
    emit "$mod_dir/$P.Application/DependencyInjection.cs" <<'EOF'
using Microsoft.Extensions.DependencyInjection;

namespace __SOLUTION__.Modules.__MODULE__.Application;

public static class DependencyInjection
{
    /// <summary>Use cases, validators and handlers of the __MODULE__ module.</summary>
    public static IServiceCollection Add__MODULE__Application(this IServiceCollection services)
    {
        return services;
    }
}
EOF

    # ---- Presentation ----
    emit "$mod_dir/$P.Presentation/$P.Presentation.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../$P.Application/$P.Application.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$mod_dir/$P.Presentation/${MODULE}Endpoints.cs" <<'EOF'
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace __SOLUTION__.Modules.__MODULE__.Presentation;

public static class __MODULE__Endpoints
{
    public const string RoutePrefix = "/api/__MODULE_LOWER__";

    public static IEndpointRouteBuilder Map__MODULE__Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("__MODULE__");

        group.MapGet("/ping", () => Results.Ok(new { module = "__MODULE__", status = "ok" }));

        return endpoints;
    }
}
EOF

    # ---- Infrastructure ----
    emit "$mod_dir/$P.Infrastructure/$P.Infrastructure.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../$P.Application/$P.Application.csproj" />
    <ProjectReference Include="../$P.Presentation/$P.Presentation.csproj" />
    <ProjectReference Include="../$P.IntegrationEvents/$P.IntegrationEvents.csproj" />
    <ProjectReference Include="$shared/$SOLUTION.Shared.Infrastructure/$SOLUTION.Shared.Infrastructure.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$mod_dir/$P.Infrastructure/Database/${MODULE}DbContext.cs" <<'EOF'
using Microsoft.EntityFrameworkCore;

namespace __SOLUTION__.Modules.__MODULE__.Infrastructure.Database;

public sealed class __MODULE__DbContext(DbContextOptions<__MODULE__DbContext> options) : DbContext(options)
{
    public const string Schema = "__MODULE_LOWER__";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(__MODULE__DbContext).Assembly);
    }
}
EOF
    emit "$mod_dir/$P.Infrastructure/${MODULE}Module.cs" <<'EOF'
using __SOLUTION__.Modules.__MODULE__.Application;
using __SOLUTION__.Modules.__MODULE__.Infrastructure.Database;
using __SOLUTION__.Modules.__MODULE__.Presentation;
using __SOLUTION__.Shared.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace __SOLUTION__.Modules.__MODULE__.Infrastructure;

public sealed class __MODULE__Module : IModule
{
    public string Name => "__MODULE__";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        services.AddDbContext<__MODULE__DbContext>(options =>
            options.UseNpgsql(
                connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", __MODULE__DbContext.Schema)));

        services.Add__MODULE__Application();
        services.AddIntegrationEventHandlers(Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.Map__MODULE__Endpoints();
}
EOF

    # ---- Unit tests ----
    emit "$test_dir/$P.UnitTests/$P.UnitTests.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../../../src/Modules/$MODULE/$P.Domain/$P.Domain.csproj" />
    <ProjectReference Include="../../../../src/Modules/$MODULE/$P.Application/$P.Application.csproj" />
  </ItemGroup>
</Project>
EOF
    emit "$test_dir/$P.UnitTests/${MODULE}SmokeTests.cs" <<'EOF'
namespace __SOLUTION__.Modules.__MODULE__.UnitTests;

public class __MODULE__SmokeTests
{
    [Fact]
    public void Domain_assembly_is_named_after_the_module() =>
        Assert.Equal(
            "__SOLUTION__.Modules.__MODULE__.Domain",
            global::__SOLUTION__.Modules.__MODULE__.Domain.AssemblyReference.Assembly.GetName().Name);
}
EOF

    # ---- Docs page ----
    emit "docs/architecture/modules/$MODULE_LOWER.md" <<'EOF'
# __MODULE__ module

| | |
|---|---|
| Code | `src/Modules/__MODULE__` |
| Schema | `__MODULE_LOWER__` |
| Route prefix | `/api/__MODULE_LOWER__` |
| Owners | `@__ORG__/__MODULE_LOWER__-team` |

## Responsibility
What this module owns — and what it explicitly does not.

## Public contract
Integration events published (`__SOLUTION__.Modules.__MODULE__.IntegrationEvents`):

| Event | When | Consumers |
|---|---|---|

Integration events consumed:

| Event | From module | Reaction |
|---|---|---|

## Data model

```mermaid
erDiagram
    EXAMPLE_ENTITY {
        uniqueidentifier Id PK
        nvarchar Name
        datetime2 CreatedOnUtc
    }
```
EOF
    printf '| %s | `%s` | `/api/%s` | [%s.md](%s.md) |\n' \
        "$MODULE" "$MODULE_LOWER" "$MODULE_LOWER" "$MODULE_LOWER" "$MODULE_LOWER" \
        >> docs/architecture/modules/README.md

    # ---- CI workflow (path-filtered) ----
    emit ".github/workflows/module-$MODULE_LOWER.yml" <<'EOF'
name: module-__MODULE_LOWER__

on:
  push:
    branches: [main]
    paths:
      - 'src/Modules/__MODULE__/**'
      - 'tests/Modules/__MODULE__/**'
      - '.github/workflows/module-__MODULE_LOWER__.yml'
  pull_request:
    paths:
      - 'src/Modules/__MODULE__/**'
      - 'tests/Modules/__MODULE__/**'
      - '.github/workflows/module-__MODULE_LOWER__.yml'

jobs:
  ci:
    uses: ./.github/workflows/_module-ci.yml
    with:
      module: __MODULE__
EOF

    # ---- CODEOWNERS ----
    {
        printf '/src/Modules/%s/    @%s/%s-team\n' "$MODULE" "$ORG" "$MODULE_LOWER"
        printf '/tests/Modules/%s/  @%s/%s-team\n' "$MODULE" "$ORG" "$MODULE_LOWER"
        printf '/docs/architecture/modules/%s.md  @%s/%s-team\n' "$MODULE_LOWER" "$ORG" "$MODULE_LOWER"
    } >> .github/CODEOWNERS

    # ---- Register in Host and ArchitectureTests ----
    local host_dir="src/Host/$SOLUTION.Host"
    local host_csproj="$host_dir/$SOLUTION.Host.csproj"
    insert_before_marker "$host_csproj" "<!-- modules -->" \
        "    <ProjectReference Include=\"../../Modules/$MODULE/$P.Infrastructure/$P.Infrastructure.csproj\" />"
    insert_before_marker "$host_dir/ModuleCatalog.cs" "// </module-usings>" \
        "using $P.Infrastructure;"
    insert_before_marker "$host_dir/ModuleCatalog.cs" "// </modules>" \
        "        new ${MODULE}Module(),"

    local arch_csproj="tests/$SOLUTION.ArchitectureTests/$SOLUTION.ArchitectureTests.csproj"
    local layer
    for layer in Domain Application Infrastructure Presentation IntegrationEvents; do
        insert_before_marker "$arch_csproj" "<!-- modules -->" \
            "    <ProjectReference Include=\"../../src/Modules/$MODULE/$P.$layer/$P.$layer.csproj\" />"
    done

    # ---- Solution ----
    dotnet sln "$SLN_FILE" add \
        "$mod_dir/$P.Domain/$P.Domain.csproj" \
        "$mod_dir/$P.Application/$P.Application.csproj" \
        "$mod_dir/$P.Infrastructure/$P.Infrastructure.csproj" \
        "$mod_dir/$P.Presentation/$P.Presentation.csproj" \
        "$mod_dir/$P.IntegrationEvents/$P.IntegrationEvents.csproj" \
        "$test_dir/$P.UnitTests/$P.UnitTests.csproj" >/dev/null

    ok "Module $MODULE added"
}

set_module() {
    MODULE="$1"
    MODULE_LOWER="$(lower "$1")"
}

build_solution() {
    log "Restoring & building"
    dotnet build "$SLN_FILE" -v quiet -nologo || die "Build failed — see output above."
}

# =====================================================================
#  Commands
# =====================================================================
cmd_new() {
    local output="." dir="" modules="" init_git=true do_build=true
    FRAMEWORK=""; ORG="your-org"

    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)   usage; exit 0 ;;
            --modules)   modules="${2:?--modules needs a value}"; shift 2 ;;
            --framework) FRAMEWORK="${2:?--framework needs a value}"; shift 2 ;;
            --output)    output="${2:?--output needs a value}"; shift 2 ;;
            --dir)       dir="${2:?--dir needs a value}"; shift 2 ;;
            --org)       ORG="${2:?--org needs a value}"; shift 2 ;;
            --no-git)    init_git=false; shift ;;
            --no-build)  do_build=false; shift ;;
            -*)          die "Unknown option: $1" ;;
            *)           [[ -z "$SOLUTION" ]] || die "Unexpected argument: $1"; SOLUTION="$1"; shift ;;
        esac
    done

    [[ -n "$SOLUTION" ]] || { usage; die "SolutionName is required."; }
    validate_solution_name "$SOLUTION"
    SOLUTION_LOWER="$(lower "$SOLUTION")"

    require_dotnet
    local sdk_version sdk_major
    sdk_version="$(dotnet --version)" || die "'dotnet --version' failed. Is the SDK installed correctly?"
    sdk_major="${sdk_version%%.*}"
    if [[ -z "$FRAMEWORK" ]]; then
        # Default to the newest framework the installed SDK can build (capped at net10.0).
        if   (( sdk_major >= 10 )); then FRAMEWORK="net10.0"
        elif (( sdk_major == 9 ));  then FRAMEWORK="net9.0"
        elif (( sdk_major == 8 ));  then FRAMEWORK="net8.0"
        else die ".NET SDK $sdk_version is too old. Install .NET 8, 9 or 10."
        fi
    fi
    set_package_versions
    if (( sdk_major < ${DOTNET_VERSION%%.*} )); then
        die "SDK $sdk_version cannot target $FRAMEWORK. Install the .NET ${DOTNET_VERSION} SDK or pass --framework net${sdk_major}.0"
    fi
    log "Using .NET SDK $sdk_version, target $FRAMEWORK"

    local module_list=() m
    modules="${modules// /}"
    if [[ -n "$modules" ]]; then
        IFS=',' read -r -a module_list <<< "$modules"
        for m in "${module_list[@]}"; do validate_module_name "$m"; done
        if [[ $(printf '%s\n' "${module_list[@]}" | sort | uniq -d | wc -l) -gt 0 ]]; then
            die "Duplicate module names in --modules"
        fi
    fi


    [[ -n "$dir" ]] || dir="$(printf '%s' "$SOLUTION_LOWER" | tr '.' '-')"
    local root="${output%/}/$dir"
    [[ -e "$root" ]] && die "Directory already exists: $root"

    log "Creating $root"
    mkdir -p "$root"
    cd "$root"
    mkdir -p src/Modules src/Shared src/Host tests/Modules build/scripts deploy
    touch src/Modules/.gitkeep tests/Modules/.gitkeep   # keep empty folders in git

    dotnet new sln -n "$SOLUTION" >/dev/null
    # SDK 10+ creates .slnx, older SDKs create .sln
    SLN_FILE=""
    local f
    for f in "$SOLUTION.slnx" "$SOLUTION.sln"; do
        [[ -f "$f" ]] && { SLN_FILE="$f"; break; }
    done
    [[ -n "$SLN_FILE" ]] || die "Could not find the generated solution file."

    cat > "$CONFIG_FILE" <<EOF
SOLUTION=$SOLUTION
FRAMEWORK=$FRAMEWORK
ORG=$ORG
SLN_FILE=$SLN_FILE
EOF

    write_root_files
    write_shared
    write_host
    write_global_tests
    write_docs
    write_deploy
    write_github

    log "Adding platform projects to $SLN_FILE"
    dotnet sln "$SLN_FILE" add \
        "src/Shared/$SOLUTION.Shared.Kernel/$SOLUTION.Shared.Kernel.csproj" \
        "src/Shared/$SOLUTION.Shared.Application/$SOLUTION.Shared.Application.csproj" \
        "src/Shared/$SOLUTION.Shared.Infrastructure/$SOLUTION.Shared.Infrastructure.csproj" \
        "src/Host/$SOLUTION.Host/$SOLUTION.Host.csproj" \
        "tests/$SOLUTION.ArchitectureTests/$SOLUTION.ArchitectureTests.csproj" \
        "tests/$SOLUTION.IntegrationTests/$SOLUTION.IntegrationTests.csproj" >/dev/null

    if [[ ${#module_list[@]} -gt 0 ]]; then
        for m in "${module_list[@]}"; do
            set_module "$m"
            add_module_files
        done
    fi

    # Ship the scaffolder with the repo so modules can be added later.
    cp "$SCRIPT_PATH" build/scripts/modmono.sh
    chmod +x build/scripts/modmono.sh

    $do_build && build_solution

    if $init_git; then
        if command -v git >/dev/null 2>&1; then
            log "Initializing git repository"
            git init -q -b main 2>/dev/null || git init -q
            git add .
            git -c user.email="scaffold@local" -c user.name="scaffold" \
                commit -q -m "chore: scaffold $SOLUTION modular monolith"
        else
            warn "git not found, skipping repository init"
        fi
    fi

    echo
    ok "Modular monolith $SOLUTION created at $root"
    if [[ ${#module_list[@]} -gt 0 ]]; then
        echo "   Modules: ${module_list[*]}"
    else
        echo "   Modules: none yet"
    fi
    echo
    echo "Next steps:"
    echo "  cd $root"
    echo "  dotnet run --project src/Host/$SOLUTION.Host"
    echo "  ./build/scripts/modmono.sh add-module <Name>"
}

cmd_add_module() {
    local root_arg="" do_build=true name=""
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)  usage; exit 0 ;;
            --root)     root_arg="${2:?--root needs a value}"; shift 2 ;;
            --no-build) do_build=false; shift ;;
            -*)         die "Unknown option: $1" ;;
            *)          [[ -z "$name" ]] || die "Unexpected argument: $1"; name="$1"; shift ;;
        esac
    done
    [[ -n "$name" ]] || { usage; die "ModuleName is required."; }
    validate_module_name "$name"
    require_dotnet

    local root
    root="$(resolve_root "$root_arg")"
    load_config "$root"
    cd "$root"

    set_module "$name"
    add_module_files

    $do_build && build_solution

    echo
    ok "Next: describe the module in docs/architecture/modules/$MODULE_LOWER.md and set real owners in .github/CODEOWNERS"
}

cmd_remove_module() {
    local root_arg="" do_build=true assume_yes=false force=false name=""
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)  usage; exit 0 ;;
            --root)     root_arg="${2:?--root needs a value}"; shift 2 ;;
            -y|--yes)   assume_yes=true; shift ;;
            --force)    force=true; shift ;;
            --no-build) do_build=false; shift ;;
            -*)         die "Unknown option: $1" ;;
            *)          [[ -z "$name" ]] || die "Unexpected argument: $1"; name="$1"; shift ;;
        esac
    done
    [[ -n "$name" ]] || { usage; die "ModuleName is required."; }
    validate_module_name "$name"
    require_dotnet

    local root
    root="$(resolve_root "$root_arg")"
    load_config "$root"
    cd "$root"
    set_module "$name"

    local mod_dir="src/Modules/$MODULE"
    local test_dir="tests/Modules/$MODULE"
    local P="$SOLUTION.Modules.$MODULE"
    [[ -d "$mod_dir" ]] || die "Module '$MODULE' not found in $mod_dir. Existing: $(cmd_list_modules | tr '\n' ' ')"

    # Refuse if another module still consumes this module's public contract.
    local dependents=() csproj
    while IFS= read -r csproj; do
        [[ "$csproj" == "$mod_dir/"* || "$csproj" == "$test_dir/"* ]] && continue
        [[ "$csproj" == "src/Host/"* || "$csproj" == "tests/$SOLUTION.ArchitectureTests/"* ]] && continue
        grep -q "$P\." "$csproj" && dependents+=("$csproj")
    done < <(find src tests -name '*.csproj' | sort)

    if [[ ${#dependents[@]} -gt 0 ]]; then
        warn "These projects still reference $MODULE:"
        printf '    %s\n' "${dependents[@]}"
        if ! $force; then
            die "Remove those references first (and the code using ${MODULE}'s integration events), or pass --force."
        fi
        warn "--force given: those references will be removed, code using them will no longer compile."
    fi

    echo "This will delete:"
    echo "    $mod_dir/"
    echo "    $test_dir/"
    echo "    docs/architecture/modules/$MODULE_LOWER.md"
    echo "    .github/workflows/module-$MODULE_LOWER.yml"
    echo "and unregister $MODULE from the solution, Host, ModuleCatalog, ArchitectureTests and CODEOWNERS."
    echo "Database objects in schema '$MODULE_LOWER' are NOT dropped."
    if ! $assume_yes; then
        local answer=""
        read -r -p "Remove module $MODULE? [y/N] " answer
        [[ "$answer" == "y" || "$answer" == "Y" ]] || die "Aborted."
    fi

    log "Removing module $MODULE"

    # Solution (must happen while the .csproj files still exist)
    local projects=()
    while IFS= read -r csproj; do projects+=("$csproj"); done \
        < <(find "$mod_dir" "$test_dir" -name '*.csproj' 2>/dev/null | sort)
    if [[ ${#projects[@]} -gt 0 ]]; then
        dotnet sln "$SLN_FILE" remove "${projects[@]}" >/dev/null \
            || warn "Could not remove some projects from $SLN_FILE — check it manually."
    fi

    # Host, ModuleCatalog, ArchitectureTests, and --force'd dependents
    local host_dir="src/Host/$SOLUTION.Host"
    remove_lines_containing "$host_dir/$SOLUTION.Host.csproj" "Modules/$MODULE/"
    remove_lines_containing "$host_dir/ModuleCatalog.cs" "using $P.Infrastructure;"
    remove_lines_containing "$host_dir/ModuleCatalog.cs" "new ${MODULE}Module()"
    remove_lines_containing "tests/$SOLUTION.ArchitectureTests/$SOLUTION.ArchitectureTests.csproj" "Modules/$MODULE/"
    if [[ ${#dependents[@]} -gt 0 ]]; then
        for csproj in "${dependents[@]}"; do
            remove_lines_containing "$csproj" "$P."
        done
    fi

    # GitHub + docs
    remove_lines_containing .github/CODEOWNERS "/Modules/$MODULE/"
    remove_lines_containing .github/CODEOWNERS "/modules/$MODULE_LOWER.md"
    remove_lines_containing docs/architecture/modules/README.md "[$MODULE_LOWER.md]($MODULE_LOWER.md)"
    rm -f ".github/workflows/module-$MODULE_LOWER.yml" "docs/architecture/modules/$MODULE_LOWER.md"

    # Code
    rm -rf "$mod_dir" "$test_dir"

    ok "Module $MODULE removed"

    $do_build && build_solution
    if command -v git >/dev/null 2>&1 && git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "Review with: git status   (undo with: git checkout -- .)"
    fi
}

cmd_list_modules() {
    local root_arg=""
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --root) root_arg="${2:?--root needs a value}"; shift 2 ;;
            *)      die "Unknown argument: $1" ;;
        esac
    done
    local root
    root="$(resolve_root "$root_arg")"
    local d
    for d in "$root"/src/Modules/*/; do
        [[ -d "$d" ]] && basename "$d"
    done
    return 0
}

main() {
    [[ $# -gt 0 ]] || { usage; exit 1; }
    local cmd="$1"; shift
    case "$cmd" in
        new)          cmd_new "$@" ;;
        add-module)   cmd_add_module "$@" ;;
        remove-module) cmd_remove_module "$@" ;;
        list-modules) cmd_list_modules "$@" ;;
        -h|--help|help) usage ;;
        *) usage; die "Unknown command: $cmd" ;;
    esac
}

main "$@"
