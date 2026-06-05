#!/usr/bin/env bash
set -euo pipefail

PACKAGE_ROOT="Assets/YUMESORA"
PACKAGE_NAME="YUMESORA_MergeSponsor.unitypackage"
DIST_DIR="dist"
BUILD_DIR="$(mktemp -d)"

cleanup() {
    rm -rf "$BUILD_DIR"
}
trap cleanup EXIT

if [[ ! -d "$PACKAGE_ROOT" ]]; then
    echo "Package root not found: $PACKAGE_ROOT" >&2
    exit 1
fi

mkdir -p "$DIST_DIR"

find "$PACKAGE_ROOT" -name "*.meta" -print0 | sort -z | while IFS= read -r -d '' meta_path; do
    asset_path="${meta_path%.meta}"
    guid="$(awk '/^guid:/ { print $2; exit }' "$meta_path")"

    if [[ -z "$guid" ]]; then
        echo "Missing guid in meta file: $meta_path" >&2
        exit 1
    fi

    package_entry="$BUILD_DIR/$guid"
    mkdir -p "$package_entry"
    cp "$meta_path" "$package_entry/asset.meta"
    printf "%s" "$asset_path" > "$package_entry/pathname"

    if [[ -f "$asset_path" ]]; then
        cp "$asset_path" "$package_entry/asset"
    fi
done

tar -czf "$DIST_DIR/$PACKAGE_NAME" -C "$BUILD_DIR" .
gzip -t "$DIST_DIR/$PACKAGE_NAME"

echo "Built $DIST_DIR/$PACKAGE_NAME"
