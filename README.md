# Playback Tools

Live event media playback software for AV technicians — dual-player video/audio/image
crossfading, built for conventions, galas, award shows, and LED wall shows.

Distributed by [AVTechKit](https://avtechkit.com).

## License

Playback Tools is licensed under the **GNU General Public License v3.0** (see `LICENSE`).
This is because it links against libmpv, which is GPLv3-licensed — see the app's own
"Licenses" page (Settings → Licenses) for full details on all third-party components.

## Building from source

This project targets **.NET 10 (Windows, WPF)**. Two third-party binaries are required
but are **not included in this repository** — download them separately and place them
in the locations below before building.

### 1. libmpv

Download: `mpv-dev-x86_64-20260610-git-304426c.7z`
From: https://github.com/shinchiro/mpv-winbuild-cmake/releases

Extract `libmpv-2.dll` from the archive and place it at: PlaybackTools/lib/libmpv-2.dll


### 2. FFmpeg

Version used: `9.0.1-full_build-gyan.dev`
From: https://www.gyan.dev/ffmpeg/builds/

Place `ffmpeg.exe` at: PlaybackTools/Installer/Redist/ffmpeg.exe

(only required for building the installer - the app itself looks for ffmpeg.exe
alongside its own executable at runtime, falling back to your system PATH if not found)

Once both are in place, open the solution in Visual Studio and build normally.