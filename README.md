# Flash Deck

A small Windows app that wraps Google's **adb** and **fastboot** so you can unlock a bootloader, install a custom ROM (LineageOS and others), go back to stock, and use a few handy phone tools, without typing commands.

Written in C# (WinForms), built with the compiler that ships with Windows. No installer, no dependencies.

> **Warning:** unlocking the bootloader and flashing **erase all data** and can void the warranty. Back up first. Do **not** lock the bootloader while a custom ROM is installed, it can brick the phone. Use at your own risk.

## Features

- **Home**: asks what you want to do (install a ROM, hide apps, unlock, phone tools) and sends you to the right page. The sidebar is grouped as Set up and Use.
- **Install ROM**: numbered steps (recovery, format data, install ROM, GApps) with the current step highlighted from the phone's state. ROM choices: LineageOS, RisingOS, AlphaDroid, or any other zip.
- **Pre-flight check**: phone detected, battery, bootloader state, files in `Rom\`, valid zips, and ROM codename vs phone codename.
- **Unlock guide**: steps per brand (Xiaomi/Mi Unlock, Pixel/OnePlus/Nothing `flashing unlock`, Motorola/Sony unlock code), plus a status check.
- **Tools**: reboot modes, flash `.img` (one or several, in order), sideload `.zip`, MD5 check, stock Xiaomi fastboot ROM (keep data / wipe), phone tools (install APK, screenshot, copy files to/from the phone, battery info, backup photos and downloads), download pages.
- **Apps (beta)**: the phone's apps load by themselves, grouped by advice (safe to disable, optional, be careful, installed by you, unknown, background parts, core/protected). Tabs switch between your apps, system, all and disabled apps. Tick the ones you do not need and press *Disable* (reversible with *Enable*, no root). The *More* menu has *Remove* (system apps usually need root), *Restore by name*, *Search online* and a switch for background parts. The home screen app and core system parts are protected. The built-in advice only covers common Xiaomi, Google, Facebook and LineageOS packages, so on other brands most apps show as "unknown": use *Search online*.
- Auto-detects the phone brand and codename, remembers the last phone, logs every session to `logs\`.
- English (default) and Bahasa Melayu, light and dark themes.

## Quick start

1. Download **platform-tools** for Windows from <https://developer.android.com/tools/releases/platform-tools> and extract it so a `platform-tools` folder sits next to `Flash Deck.exe`.
2. Put `recovery.img`, the ROM zip and (optionally) a GApps zip in a `Rom` folder next to the app.
3. On the phone enable Developer options and **USB debugging**, connect USB, tap **Allow**.
4. Run the **Pre-flight check** tab, then follow the **ROM Guide**.

```
Flash Deck.exe
platform-tools\   (Google's adb + fastboot, not included)
Rom\              (recovery.img, ROM zip, GApps zip)
logs\             (created automatically)
```

## Build

```
build.bat      builds "Flash Deck.exe" (needs only Windows' built-in C# compiler)
package.bat    builds and zips to dist\FlashDeck-portable.zip
```

Developer switches: `Flash Deck.exe --page=N` opens a page (0 home, 1 install ROM, 2 tools, 3 check, 4 unlock, 5 apps, 6 log), `--wa=1366x728` pretends the screen is that size, `--selftest` writes `selftest.txt` with logic checks, and `--phonetest` (phone connected) disables then re-enables the Recorder app and writes `phonetest.txt`. For screenshots: `--demo-apps`, `--autoload`, `--filter=N` and `--guide-open`.

## Notes and limits

- Commands for brands other than Xiaomi are best-effort; check the manufacturer's guide.
- Samsung uses Odin / Download mode, not fastboot: only reboot and sideload work.
- If `platform-tools` is missing, a setup window explains how to get it and offers a "Check again" button.
- The window fits small screens (for example 1366x768), but the app is not DPI-aware: on 125-150% display scaling Windows enlarges it and the text can look slightly soft.
- The app is unsigned, so Windows SmartScreen or antivirus may warn about it. Build it yourself from the source if in doubt.
- Tested with a Xiaomi Mi 10T (M2007J3SY) running LineageOS. Other phones are untested.
- Apps (beta): on that phone Android refuses to remove system apps without root, so use *Disable*. *Remove* on a stock ROM is untested.
- Released under the MIT License (see `LICENSE`). Google's `platform-tools` is not included and has its own license.
