#!/usr/bin/env bash
set -e

# Disable MSBuild server daemon to avoid preview SDK globbing caching bugs
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1

APP=Tasks
CONFIG=Release
RID=linux-x64

echo "==> Publishing $APP ($RID)"

dotnet publish "$APP.csproj" \
  -c $CONFIG \
  -r $RID \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=false \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true \
  -nodeReuse:false

echo "==> Linux binary ready"
