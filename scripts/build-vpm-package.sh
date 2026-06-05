#!/usr/bin/env bash
set -euo pipefail

PACKAGE_ROOT="Packages/com.yumesora.merge-sponsor"
DIST_DIR="dist"

if [[ ! -f "$PACKAGE_ROOT/package.json" ]]; then
    echo "Package manifest not found: $PACKAGE_ROOT/package.json" >&2
    exit 1
fi

PACKAGE_NAME="$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$PACKAGE_ROOT/package.json" | head -1)"
PACKAGE_VERSION="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$PACKAGE_ROOT/package.json" | head -1)"

if [[ -z "$PACKAGE_NAME" || -z "$PACKAGE_VERSION" ]]; then
    echo "Could not read package name/version from $PACKAGE_ROOT/package.json" >&2
    exit 1
fi

mkdir -p "$DIST_DIR"
PACKAGE_PATH="$DIST_DIR/$PACKAGE_NAME-$PACKAGE_VERSION.zip"
rm -f "$PACKAGE_PATH"

(
    cd "$PACKAGE_ROOT"
    zip -qr "../../$PACKAGE_PATH" .
)

echo "Built $PACKAGE_PATH"
