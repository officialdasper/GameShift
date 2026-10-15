# Changelog

## [1.0.0] - 2026-10-15

First public release.

- Game mode with one click from the window or the tray, or automatically when a game from your list starts.
- Switches to the Maximum performance power plan and restores the previous plan afterwards.
- Hides Windows notifications while you play.
- Closes background programs by a per-program rule: close and restore, close for good, or leave alone.
- Before a game, a "What to close" window lists the running programs with checkboxes, a select-all box and a "Remember my choice" option. After the game, a "What to reopen" window does the same for the programs that were closed.
- Programs with unsaved work are never force-closed: GameShift asks them to close, waits, and lets you wait longer, close without saving or leave them open.
- The lists in both windows update on their own: a program you close or reopen yourself appears or disappears within a second.
- The stop-list also covers the Epic, Ubisoft, GOG and other launchers and their helper processes, so they are never offered for closing.
- Optional standby memory cache clearing through a scheduled task, with one administrator prompt on setup.
- NVIDIA driver profile settings applied only to the games in your list, with a button to restore the original values.
- A snapshot of the system is written to disk before the first change, so a crash in the middle of a session can be rolled back on the next start.
- Game scan for Steam, Epic Games Store, Riot Games, Battle.net, EA app and Ubisoft Connect, plus manual add by `.exe` or from running processes.
- Game covers from the local Steam cache.
- Dark and light themes, accent colors, Mica and Acrylic window backgrounds on Windows 11.
- A custom accent color: pick it on a color square with a hue slider, and the whole window recolors as you drag.
- The logo and the window icon take the accent color.
- Soft shadows at the edges of scrolling lists and pages show that there is more above or below.
- An info button on each mode card shows what the mode does with your current settings, and the cards carry a short caption.
- The Work card uses a blurred copy of your desktop wallpaper as its background.
- Onest for text and Unbounded for page and mode titles, bundled with the app (SIL Open Font License, see THIRD-PARTY-NOTICES.md).
- Ten interface languages: Russian, English, Ukrainian, German, Spanish, Brazilian Portuguese, French, Polish, Turkish, Simplified Chinese.
- Tray icon, autostart with Windows and the `--tray` flag.
