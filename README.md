# WeaponSkin.Menu

`WeaponSkin.Menu` is a companion module for `WeaponSkin`.

It adds:

- in-game menu flow
- localized skin catalogs
- writes to the original `ws_*` tables
- inventory sync with the original `WeaponSkin`
- config-based live/deferred apply behavior

## Requirements

- [ModSharp](https://github.com/Kxnrl/modsharp-public)
- [WeaponSkin](https://github.com/Ariiisu/WeaponSkin/releases)
- `WeaponSkin.Shared`

## Build

```powershell
dotnet build .\WeaponSkin.Menu.csproj /p:WeaponSkinSharedHintPath="C:\path\to\WeaponSkin.Shared.dll"
```

## Deploy

Copy these into your server `game/sharp` directory:

- `modules/WeaponSkin.Menu`
- `locales/WeaponSkin.Menu.json`
- `configs/weaponskin.menu.jsonc`

## Notes

- This repo stays separate from the original `WeaponSkin` source.
- The module uses the original `ws_*` tables.
- Database settings come from `game/sharp/configs/weaponskin.jsonc`.
