#!/usr/bin/env bash
set -euo pipefail

# Disable MSBuild server daemon to avoid preview SDK globbing caching bugs
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1

APP_NAME="Tasks"
CONFIG="Release"
RID="win-x64"

echo "==> Publishing Windows EXE..."

dotnet publish "$APP_NAME.csproj" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -nodeReuse:false

EXE_PATH="bin/$CONFIG/net10.0/$RID/publish/$APP_NAME.exe"

if [[ ! -f "$EXE_PATH" ]]; then
  echo "EXE not found at $EXE_PATH"
  exit 1
fi

echo "==> Done."
echo "Output:"
echo "  $EXE_PATH"
