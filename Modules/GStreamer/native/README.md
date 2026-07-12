# HDR tone-mapping GStreamer plugin

`hdrtonemap` is a `GstBaseTransform` element. It accepts `P010_10LE` or `I420_10LE` PQ/HLG video and produces SDR `I420` with BT.709 colorimetry. With `use-opencl=true` it lazily probes an OpenCL GPU once, uses `tonemap_opencl` when available, and automatically falls back to the CPU graph if initialization or frame processing fails. `use-opencl=false` selects the CPU graph without probing OpenCL.

The OpenCL graph is:

```text
P010
hwupload
tonemap_opencl (Hable, BT.709 NV12)
hwdownload
scale (NV12 to I420 layout conversion)
```

The CPU fallback graph is:

```text
zscale (PQ/HLG to linear BT.2020)
format (gbrpf32le)
tonemap (Hable)
zscale (BT.2020 to BT.709 limited range)
format (yuv420p)
```

## Linux

Install Meson, Ninja, a C compiler, GStreamer development packages, and development packages for `libavfilter`, `libavutil`, `libswscale`, and `zimg`. The CPU fallback requires the `zscale`, `tonemap`, `format`, `buffer`, and `buffersink` filters. OpenCL acceleration is enabled when the system FFmpeg also exposes `hwupload`, `hwdownload`, and `tonemap_opencl` and an OpenCL GPU is available.

```bash
./build-linux.sh
```

The script builds a dynamically linked plugin and copies it to `runtimes/linux-<arch>/native/gstreamer-1.0`.

## Windows

Use an MSYS2 MinGW64 shell matching the MinGW GStreamer distribution. Install the compiler toolchain, Meson, Ninja, pkg-config, make, autoconf, automake, libtool, nasm, OpenCL headers, and the OpenCL ICD loader:

```bash
pacman -S --needed mingw-w64-x86_64-opencl-headers mingw-w64-x86_64-opencl-icd
```

Provide unpacked zimg and FFmpeg source directories:

```bash
export ZIMG_SOURCE=/path/to/zimg
export FFMPEG_SOURCE=/path/to/ffmpeg
export DEPS_PREFIX=/c/gst-native-build/deps
export BUILD_DIR=/c/gst-native-build/plugin
export TMPDIR=/c/gst-native-build/tmp
export GSTREAMER_ROOT='/c/Program Files/gstreamer/1.0/mingw_x86_64'
./build-windows-msys2.sh
```

Keep the zimg, FFmpeg, dependency, build, and temporary paths free of spaces. The GStreamer path may contain spaces.

The script builds zimg and the required CPU/OpenCL FFmpeg filters as static libraries, links them into `libgsthdrtonemap.dll`, checks that no FFmpeg/zimg DLL remains, and copies the plugin plus the portable `OpenCL.dll` loader to `runtimes/win-x64/native/gstreamer-1.0`. The vendor OpenCL implementation still comes from the installed GPU driver; systems without one use the CPU fallback.

Do not replace the `libavfilter` DLL shipped with GStreamer. Review and satisfy the GStreamer, FFmpeg, and zimg license requirements before distributing native binaries.
