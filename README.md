# WeaponSkin.Menu

A ModSharp plugin for Counter-Strike 2 servers. Adds an in-game menu for `WeaponSkin` and saves player selections into the existing `ws_*` tables.

## Features

- **In-Game Menu:** Opens a `ws` menu for weapon skins, knives, gloves, agents, music kits, pins, and StatTrak.
- **WeaponSkin Integration:** Uses the existing `WeaponSkin` data model and sync flow instead of introducing a separate schema.
- **Localized Catalogs:** Loads multi-language item catalogs for supported client languages.
- **Configurable Apply Behavior:** Supports deferred weapon apply and configurable live apply for team cosmetics.

## Requirements & Dependencies

**Core**
- .NET 10.0
- [ModSharp 2.x](https://github.com/Kxnrl/modsharp-public)
- [WeaponSkin](https://github.com/Ariiisu/WeaponSkin/releases)

**Plugin Dependencies**
- MenuManager
- LocalizerManager

## Installation

1. Download the latest release from the [Releases](https://github.com/Tsukasa-Nefren/weaponskin-menu-private/releases) page.
2. Extract the `.zip` file into your server's root directory so the `sharp` folder merges automatically.
3. Make sure [WeaponSkin](https://github.com/Ariiisu/WeaponSkin/releases) is already installed on the server.
4. Start or reload the server.

**Directory Structure:**
```text
sharp/
├─ modules/
│  └─ WeaponSkin.Menu/
│     └─ WeaponSkin.Menu.dll
├─ locales/
│  └─ WeaponSkin.Menu.json
└─ configs/
   └─ weaponskin.menu.jsonc
```

## Configuration

**Path:** `sharp/configs/weaponskin.menu.jsonc`

```jsonc
{
  "Apply": {
    "Weapons": "Deferred",
    "TeamCosmetics": "Live"
  },
  "Sync": {
    "UseClientCommandFallback": true
  },
  "Selection": {
    "WriteBothTeamsWhenSpectator": true
  }
}
```

| Option | Description | Default |
|--------|-------------|---------|
| `Apply.Weapons` | `Deferred` only syncs `WeaponSkin` cache. `Live` also tries immediate in-round apply. | `"Deferred"` |
| `Apply.TeamCosmetics` | Apply behavior for gloves, agents, music kits, and pins. | `"Live"` |
| `Sync.UseClientCommandFallback` | Falls back to issuing `ws_refresh` if direct sync invocation fails. | `true` |
| `Selection.WriteBothTeamsWhenSpectator` | Saves team cosmetics to both teams when the player is not currently on CT/TE. | `true` |

## Commands

| Command | Description |
|---------|-------------|
| `ws` | Opens the main WeaponSkin menu |
| `ws_menu` | Opens the main WeaponSkin menu |
| `ws_knife` | Opens the knife menu |
| `ws_gloves` | Opens the glove menu |
| `ws_agents` | Opens the agent menu |
| `ws_music` | Opens the music kit menu |
| `ws_pins` | Opens the pin menu |
| `ws_stattrak` | Toggles StatTrak |
| `ws_st` | Toggles StatTrak |

## Notes

- This repository stays separate from the original `WeaponSkin` source.
- Database settings are still read from `sharp/configs/weaponskin.jsonc`.
- The module writes to the existing `ws_*` tables.
