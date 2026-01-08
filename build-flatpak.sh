#!/usr/bin/env bash
set -e

APP_ID=io.github.terra2o.Tasks
MANIFEST=$APP_ID.yml
BUILD_DIR=build-dir
REPO=repo
BUNDLE=$APP_ID.flatpak

echo "==> Building Flatpak"

rm -rf $BUILD_DIR $REPO
mkdir -p $REPO

flatpak-builder \
  --force-clean \
  --repo=$REPO \
  $BUILD_DIR \
  $MANIFEST

flatpak build-bundle \
  $REPO \
  $BUNDLE \
  $APP_ID

echo "==> Flatpak bundle created: $BUNDLE"
