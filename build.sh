#!/bin/sh
set -eu
apa_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
apa_dotnet=${DOTNET:-dotnet}
: "${KSP_ROOT:?Set KSP_ROOT to the KSP installation directory}"
"$apa_dotnet" build "$apa_root/AirParkStability.csproj" -c Release -p:KspRoot="$KSP_ROOT" --nologo
mkdir -p "$apa_root/APA/Plugins"
cp "$apa_root/bin/Release/net472/AirParkStability.dll" "$apa_root/APA/Plugins/AirParkStability.dll"
printf '%s\n' 'Built APA/Plugins/AirParkStability.dll (APA is the live GameData addon folder).'
