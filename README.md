# LootGoblin

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that keeps track of Need/Greed/Pass loot rolls, so you don't have to squint at the chat log to find out who rolled what — and who actually won.

## What it does

Every time a loot chest opens, LootGoblin quietly tracks it in the background and shows you a clean summary:

- **Need / Greed / Pass, per item** — see exactly who rolled what, and with which value. Passes are inferred automatically (the game doesn't send a chat message for them, so LootGoblin figures it out once the item is handed out).
- **Grouped by chest** — each loot window is grouped under its own "Chest N — Dungeon Name" entry, so you can scroll back through everything that dropped during a run instead of losing track after the first item.
- **Winner spotlight** — once an item is awarded, its icon and winner are shown front and center.
- **Class icons** — see at a glance which job rolled on an item, for every member of your party (not just yourself).
- **Auto-popup** — the window can open automatically the moment a new chest appears, so you never miss a roll. This can be turned off in the settings if you'd rather open it manually.
- **Clear button** — wipe the current session whenever you want a fresh start.

## Installation

1. In-game, open `/xlsettings` and go to the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL:
   ```
   https://raw.githubusercontent.com/Stinguim/Loot-Goblin-FF14/master/pluginmaster.json
   ```
3. Click the **+** button, then **Save and Close**.
4. Open `/xlplugins`, search for **LootGoblin**, and install it.

## Usage

- `/lootcheck` — opens or closes the Loot Results window.
- The window can also open automatically whenever a new loot chest appears (toggleable in the plugin's settings, accessible from the Plugin Installer).

## Feedback / Issues

Found a bug, or have a suggestion? Feel free to open an issue on this repository.
