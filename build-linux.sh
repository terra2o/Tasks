#!/usr/bin/env bash
set -e

APP=Tasks
RID=linux-x64
CONFIG=Release

echo "==> Publishing $APP ($RID)"

dotnet publish \
  -c $CONFIG \
  -r $RID \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true

echo "==> Linux binary ready"
