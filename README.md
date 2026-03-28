# WeaponSkin.Menu

`WeaponSkin.Menu` is a companion ModSharp module for the original `WeaponSkin` plugin.

It provides:

- in-game menu flow for `WeaponSkin`
- multi-language item catalogs
- direct writes to `ws_*` tables
- runtime sync with the original `WeaponSkin` inventory cache
- optional live/deferred apply behavior through config

## Requirements

- original `WeaponSkin`
- original `WeaponSkin.Request.Sql`
- `WeaponSkin.Shared.dll`
- `MenuManager`
- `LocalizerManager`

## Build

Build `WeaponSkin.Shared` first, or pass the path manually:

```powershell
dotnet build .\WeaponSkin.Menu.csproj /p:WeaponSkinSharedHintPath="C:\path\to\WeaponSkin.Shared.dll"
```

## Deploy

Copy the build output under:

- `modules/WeaponSkin.Menu`
- `locales/WeaponSkin.Menu.json`
- `configs/weaponskin.menu.jsonc`

into your server's `game/sharp` directory.

## Notes

- This repo is intended to stay separate from the original `WeaponSkin` source.
- The module writes to the original `ws_*` tables and follows `game/sharp/configs/weaponskin.jsonc`.
