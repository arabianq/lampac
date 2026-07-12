#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
architecture="$(uname -m)"
case "$architecture" in
  x86_64) runtime_id="linux-x64" ;;
  aarch64|arm64) runtime_id="linux-arm64" ;;
  *) echo "Unsupported architecture: $architecture" >&2; exit 1 ;;
esac

build_dir="${BUILD_DIR:-$script_dir/build-linux-$architecture}"
output_dir="$script_dir/runtimes/$runtime_id/native/gstreamer-1.0"

if [[ -f "$build_dir/build.ninja" ]]; then
  meson setup --reconfigure "$build_dir" "$script_dir" -Dstatic-ffmpeg=false --buildtype=release
else
  meson setup "$build_dir" "$script_dir" -Dstatic-ffmpeg=false --buildtype=release
fi
meson compile -C "$build_dir"
mkdir -p "$output_dir"
cp "$build_dir/libgsthdrtonemap.so" "$output_dir/"
GST_PLUGIN_PATH="$output_dir${GST_PLUGIN_PATH:+:$GST_PLUGIN_PATH}" gst-inspect-1.0 hdrtonemap
echo "Plugin: $output_dir/libgsthdrtonemap.so"
