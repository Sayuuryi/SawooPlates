# Changelog

## [0.9.22.0]

### Plate design
- Apply plate design goes all the way to Edit Plate Design by itself: it opens your plate, clicks Edit and then Edit Plate Design
- If a step can't be found, it asks you to open Edit Plate Design and still applies when you do

### Overlays
- The Portrait Editor menu bar and the alignment guides stay behind other plugin windows too

## [0.9.21.0]

### Gear
- Clearer wording for the glamour dresser and armoire reminder

## [0.9.20.0]

### Plate design
- Apply plate design opens your Adventurer Plate and puts the design in as soon as you open Edit Plate Design there
- Closing the plate before that cancels it without changing anything

### Overlays
- The Portrait Editor menu bar and the buttons over game windows stay behind other plugin windows

## [0.9.19.0]

### Plate design
- Ctrl+click on Apply plate design opens a window to pick the parts, with a preview of what you picked
- Picked decorations replace only yours of the same kind; the rest of your plate stays as it is
- The portrait position can be picked or left as it is

## [0.9.18.0]

### Recent and Presets
- Apply plate design opens your Adventurer Plate and its design editor when they're closed, then applies
- The Sawoo Plates window steps aside while an editor it opened is in use, and comes back when that editor closes

## [0.9.17.0]

### Recent and Presets
- Apply to my portrait opens the Portrait Editor for your current gearset when it's closed, then applies

### Overlays
- Buttons over the party and MVP portrait screens show again in duties and PvP; HUD elements no longer hide them

## [0.9.16.0]

### Gear
- Your glamour dresser and armoire are remembered per character, so owned gear is known even when they aren't open
- The lists update by themselves whenever you open the dresser or armoire
- A short reminder after logging in until both have been opened once

## [0.9.15.0]

### Recent and Presets
- Owned counts show for every group again
- More room and a divider between Portrait, Plate and Gear
- Presets saved from Recent keep their gear and dyes

## [0.9.14.0]

### Recent and Presets
- The plate has its own row at a fixed size, with camera and lighting next to it
- Portrait, Plate and Gear are listed below the plate, side by side when there's room

## [0.9.13.0]

### Recent and Presets
- The plate and its parts are always side by side
- Portrait, Plate and Gear sit next to each other when the window is wide, so there's less scrolling

## [0.9.12.0]

### Recent and Presets
- The plate keeps a fixed size; resizing the window only widens or narrows the parts list next to it
- The window opens wider, so the plate and its parts fit side by side

## [0.9.11.0]

### Recent and Presets
- The plate and its parts sit side by side, so everything is visible without scrolling
- The plate keeps a comfortable size instead of growing with the window
- Narrow windows stack them instead

## [0.9.10.0]

### Fixes
- Portraits no longer flicker or go blank while resizing the window

## [0.9.9.0]

### Recent and Presets
- Right-click a plate or preset for its actions: apply, save, copy, remove or delete
- One "Empty Recent" button with a confirmation instead of two trash icons
- What you're missing is shown under the buttons; hover it to see the list with icons
- Plate previews use the full width of the window
- Owned counts sit at the right of each section
- Presets with a plate design count its parts too
- The tag for presets saved from Recent is now "Saved"

### Settings
- Clearer gear warning texts; the Glamourer note is one line and only shows when Glamourer is on

## [0.9.8.0]

### Presets
- Portraits keep their real background (it showed a flat colour before); open a plate again to refresh an older one
- "Missing" counts only portrait and plate parts; gear you don't have is listed separately
- Camera and lighting labels are no longer cut off

### Settings
- Fewer, clearer settings: keep portraits in Recent, show buttons, Recent size, Portrait Editor menu bar and the gear warning
- Removed automatic gearset re-equip and automatic portrait updates

### Backups
- Settings, presets and Recent are backed up at every start and before importing presets (last 10 kept, in the Backups folder)
- Importing never changes or deletes anything on either side

## [0.9.7.0]

### Presets
- Plate previews show every part in full, nothing is cut at the edges
- Missing parts name the exact quest or duty that unlocks them
- Right-click a part or item to open it on Garland Tools
- Wider previews for borders and frames in the tooltips

## [0.9.6.0]

### Presets
- The apply buttons list the parts you don't have, with icons
- Applying with missing parts asks first
- Ctrl+click on "Apply plate design" lets you pick which parts to apply

### Fixes
- Unlock status no longer flickers in the list tooltips

## [0.9.5.0]

### Presets
- "Apply to my portrait" applies right away; hold Ctrl while clicking to choose which parts to apply
- "Apply plate design" works while Edit Plate Design is open

## [0.9.4.0]

### Presets
- Presets with a plate design show the whole Adventurer Plate: backing, base, borders, frames, accent and the portrait in its place

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
