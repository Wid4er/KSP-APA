#!/bin/sh
set -eu
apa_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
apa_dotnet=${DOTNET:-dotnet}
: "${KSP_ROOT:?Set KSP_ROOT to the KSP installation directory}"
"$apa_dotnet" run --project "$apa_root/Tests/LogicTests.csproj" -c Release
"$apa_dotnet" run --project "$apa_root/Tests/ReloadTests.csproj" -c Release
"$apa_dotnet" run --project "$apa_root/Tests/AssemblyAudit.csproj" -c Release -p:KspRoot="$KSP_ROOT" -- "$KSP_ROOT" "$apa_root"
