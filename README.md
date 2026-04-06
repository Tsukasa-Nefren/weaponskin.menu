# WeaponSkin.Menu

`WeaponSkin.Menu` is a menu-based frontend for the original [`WeaponSkin`](https://github.com/Ariiisu/WeaponSkin) module on [`ModSharp`](https://github.com/Kxnrl/ModSharp-public).

It lets players manage their cosmetics in-game without editing database rows manually.

## What It Supports

- Weapon skins
- Knife selection and knife finish selection
- Glove selection
- Agent selection
- Music kit selection
- Pin selection
- StatTrak toggle for the active weapon
- Wear preset selection for weapons, knives, and gloves

## What It Does Not Support

- Stickers
- Keychains
- Nametags
- Web UI

## Requirements

- [`ModSharp`](https://github.com/Kxnrl/ModSharp-public)
- [`WeaponSkin`](https://github.com/Ariiisu/WeaponSkin) module
- `MenuManager`
- `LocalizerManager`

`WeaponSkin.Menu` does not replace [`WeaponSkin`](https://github.com/Ariiisu/WeaponSkin). It uses the same underlying storage and refresh flow.

## Installation

1. Download the release zip.
2. Extract the `sharp/` folder into your server.
3. Make sure the original `WeaponSkin` module is already installed and working.
4. Restart the server or reload the module.

## Commands

Player commands:

- `!ws` : open the main menu
- `!knife` : open the knife menu
- `!gloves` : open the glove menu
- `!agents` : open the agent menu
- `!music` : open the music kit menu
- `!pins` : open the pin menu
- `!stattrak` : toggle StatTrak for the active weapon
- `!st` : short alias for StatTrak toggle

If your setup uses the standard ModSharp client command flow, the console equivalents use the `ms_` prefix, for example `ms_ws`.

## Apply Behavior

Default config:

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

Meaning:

- `Weapons = Deferred`
  Weapon skins are saved immediately, but usually apply on next spawn, next weapon receive, or next time the weapon is recreated.
- `TeamCosmetics = Live`
  Gloves, agents, music kits, and pins try to apply immediately.
- `UseClientCommandFallback = true`
  If direct `WeaponSkin` refresh fails, the menu can fall back to issuing `ws_refresh`.
- `WriteBothTeamsWhenSpectator = true`
  If a player is not currently on CT or T, team cosmetics are written to both teams.

## Storage

This module uses the same cosmetic data model as `WeaponSkin`.

Supported database backends:

- SQLite
- MySQL
- PostgreSQL

## Notes

- Weapon and knife paints are resolved from bundled catalog data.
- Agent definitions are resolved from the runtime econ data exposed by the game.
- Doppler and Gamma Doppler variants are separated by paint id, not just by the display name.
- Some cosmetics are applied immediately, while others depend on the original `WeaponSkin` refresh path.

## Version

Current public release target: `1.1.0`
