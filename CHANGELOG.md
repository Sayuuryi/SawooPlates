# Changelog

## [0.9.3.0]

### Lists
- Select several entries with Ctrl+click, or a range with Shift+click, and delete them together
- Deleting needs Ctrl+click, so nothing is removed by a stray click

### Presets
- Saving the same portrait and plate design twice reuses the existing preset

## [0.9.2.0]

### Recent
- Plates you open are kept in Recent again by default; you can turn this off in Settings

## [0.9.1.0]

### UI
- Buttons shown over game windows have a dark background, so they're easy to read on any plate
- They hide while another game window is in front of them

## [0.9.0.0]

### Fixes
- The plugin failed to load in 0.7.0.0 and 0.8.0.0

### Presets
- Presets can keep a plate design as well as the portrait
- "Apply plate design" fills in your own plate's design editor; parts you haven't unlocked are skipped and you confirm with the game's Save button

### Commands
- `/splates` opens the window, `/splates config` opens the settings

## [0.8.0.0]

### Presets
- Offers once to import presets you already have on this PC, with a button in Settings for later

### Fixes
- A failed start now cleans up after itself, so the next load or update isn't affected

## [0.7.0.0]

### UI
- Reworked main window: cleaner lists with icons, a clear action bar, and empty screens that say what to do
- Each portrait lists its background, frame, accent, pose and expression with a check or cross for whether you have it
- Hover an item to see a larger preview, details and how to unlock it
- Camera and lighting numbers moved into a collapsed section
- Settings grouped into sections
- About tab lists the commands

### Presets
- Edit a preset's name and tags from the Presets tab, and add new tags there
- Presets show which parts you haven't unlocked yet

## [0.6.1.0]

### Fixes
- A portrait image could fail to save when two saves of the same image overlapped

## [0.6.0.0]

### Portrait Editor
- Menu bar above the Portrait Editor: Reset, Copy and Paste preset codes, Advanced Import, Advanced Edit, Save as Preset, Preset Browser, Alignment Tool
- Advanced Edit: camera yaw, pitch, distance and position, zoom, rotation, eye and head direction, animation timestamp
- Alignment grid with line count and colour settings

### Presets
- Preset Browser with thumbnails and tags; copy the preset code or the portrait image
- Presets tab in the main window with tag filter and search

### Gear
- Notice when your gear no longer matches your portrait, with options to re-equip the gear set or update the portrait
