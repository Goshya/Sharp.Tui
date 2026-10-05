#!/usr/bin/env bash
# Packs Sharp.Tui and proves the package works for a stranger, outside this repository:
#   1. the .nupkg has exactly the four library DLLs, the README, and ZERO dependencies;
#   2. a fresh app that copies the README's getting-started snippet restores from a feed holding
#      *only* this package (any hidden dependency would fail the restore), and builds;
#   3. the same app publishes with NativeAOT with no warnings.
# Run from anywhere:  eng/verify-package.sh      (needs bash, unzip, the .NET 10 SDK; NativeAOT needs a C++ toolchain)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
# KEEP_WORK=1 leaves the scratch directory (feed, app, native binary) behind and prints where.
if [ "${KEEP_WORK:-0}" = "1" ]; then echo "scratch dir: $WORK"; else trap 'rm -rf "$WORK"' EXIT; fi

# dotnet on Windows (Git Bash) wants Windows-style paths; elsewhere this is a no-op.
if command -v cygpath >/dev/null 2>&1; then W() { cygpath -m "$1"; }; else W() { echo "$1"; }; fi

# A unique version every run: NuGet's global cache is keyed by id+version, so a reused version
# could silently restore a stale package.
VERSION_SUFFIX="verify.$(date +%s)"
FEED="$WORK/feed"
APP="$WORK/app"
export NUGET_PACKAGES="$(W "$WORK/nuget-cache")" # isolated: don't touch the user's global cache
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

fail() { echo "FAIL: $*" >&2; exit 1; }

echo "== 1/4 pack"
dotnet pack "$(W "$ROOT/src/Sharp.Tui")" -c Release -o "$(W "$FEED")" -p:VersionSuffix="$VERSION_SUFFIX" -warnaserror >/dev/null
NUPKG="$(ls "$FEED"/Sharp.Tui.*.nupkg)"
VERSION="$(basename "$NUPKG" .nupkg)"; VERSION="${VERSION#Sharp.Tui.}"
echo "   packed Sharp.Tui $VERSION"

echo "== 2/4 inspect package"
LIST="$(unzip -Z1 "$NUPKG")"
NUSPEC="$(unzip -p "$NUPKG" Sharp.Tui.nuspec)"
echo "$NUSPEC" | grep -q "<dependencies" && fail "nuspec declares dependencies — the package must have none"
DLLS="$(echo "$LIST" | grep '^lib/' | sort | tr '\n' ' ')"
EXPECTED="lib/net10.0/Sharp.Tui.Core.dll lib/net10.0/Sharp.Tui.Layout.dll lib/net10.0/Sharp.Tui.Runtime.dll lib/net10.0/Sharp.Tui.Widgets.dll "
[ "$DLLS" = "$EXPECTED" ] || fail "unexpected lib/ contents: $DLLS"
echo "$LIST" | grep -qx "README.md" || fail "README.md missing from the package"
echo "$NUSPEC" | grep -q '<license type="expression">MIT</license>' || fail "license expression missing"
echo "$NUSPEC" | grep -q '<repository type="git"' || fail "repository metadata missing"
echo "   OK: 4 assemblies, README, MIT, repository info, no dependencies"

echo "== 3/4 consume from a feed containing only this package"
mkdir -p "$APP"
cat >"$APP/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$(W "$FEED")" />
  </packageSources>
</configuration>
EOF
cat >"$APP/App.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Sharp.Tui" Version="$VERSION" />
  </ItemGroup>
</Project>
EOF
# The app's code is the README's getting-started snippet, verbatim: first ```csharp block.
awk '/^```csharp/{f=1; next} /^```/{if(f) exit} f' "$ROOT/README.md" >"$APP/Program.cs"
[ -s "$APP/Program.cs" ] || fail "couldn't extract the README snippet"
dotnet build "$(W "$APP")" -c Release -warnaserror 2>&1 | tail -3
echo "   OK: restored from a local-only feed and built"

echo "== 4/4 NativeAOT publish of that app"
# AOT needs the ILCompiler package, which lives on nuget.org; Sharp.Tui itself must still come from the local feed.
cat >"$APP/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$(W "$FEED")" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Sharp.Tui" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF
RID="$(dotnet --info | awk '/RID:/{print $2; exit}')"
dotnet publish "$(W "$APP")" -c Release -r "$RID" -p:PublishAot=true -warnaserror -o "$(W "$WORK/out")" 2>&1 | tail -3
BIN="$(ls "$WORK"/out/App "$WORK"/out/App.exe 2>/dev/null | head -1)"
[ -n "$BIN" ] || fail "no native binary produced"
echo "   OK: native binary $(basename "$BIN"), $(( $(wc -c <"$BIN") / 1024 )) KB ($RID)"

echo "ALL CHECKS PASSED"
