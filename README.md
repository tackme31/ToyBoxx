# ToyBoxx - a tiny media player
Limited, buggy, and uncustomizable, but perfect for me.

![](./img/screenshot.png)

## Features

- Seek bar thumbnail preview on hover
- Loop playback within a specified range
- Zoom in and out (Ctrl + Mouse Wheel)
- Adjust playback speed
- Move forward one frame

## Setup

- Download FFmpeg library from [here](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2024-08-31-12-50).
    - ffmpeg-n7.0.2-6-g7e69129d2f-win64-gpl-shared-7.0.zip
- Extract the zip file.
- Download SoundTouch from [here](https://www.surina.net/soundtouch/download.html).
    - SoundTouch DLL library 2.1.1 for Windows
- Copy all files in SoundTouch zip into `/bin` in extracted ffmpeg folder.
- Delete `SoundTouch.dll` and Rename `SoundTouch.dl_x64.dll` into `SoundTouch.dll`.
- Edit appsettings.json
    - FFMpegRootPath: `/bin` folder in the ffmepg folder.
    - You can also create a appsettings.user.json.
- Now ready to debug.

SoundTouch is optional. It allows keeping the pitch constant when changing the playback speed.

## Todos

- [ ] Take screenshots
