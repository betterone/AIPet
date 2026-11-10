#!/bin/zsh
set -eu
cd "${0:A:h}/.."
DOTNET_CLI_HOME="$PWD/.tools/dotnet-home" DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project tests/Momo.Core.Tests.csproj -c Release
