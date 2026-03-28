# WeaponSkin.Menu

`WeaponSkin.Menu` is a companion ModSharp module for the original `WeaponSkin` plugin.

Original `WeaponSkin` repository:

- [Ariiisu/WeaponSkin](https://github.com/Ariiisu/WeaponSkin)

It provides:

- in-game menu flow for `WeaponSkin`
- multi-language item catalogs
- direct writes to `ws_*` tables
- runtime sync with the original `WeaponSkin` inventory cache
- optional live/deferred apply behavior through config

## Requirements

This package only contains `WeaponSkin.Menu`.

Install these separately if your server does not already have them:

- original `WeaponSkin` release:
  [Ariiisu/WeaponSkin](https://github.com/Ariiisu/WeaponSkin/releases)
- `MenuManager` module source:
  [modsharp-public/Sharp.Modules/MenuManager](https://github.com/modsharp-project/modsharp-public/tree/master/Sharp.Modules/MenuManager)
- `LocalizerManager` module source:
  [modsharp-public/Sharp.Modules/LocalizerManager](https://github.com/modsharp-project/modsharp-public/tree/master/Sharp.Modules/LocalizerManager)

`WeaponSkin.Shared.dll` is already included in the original `WeaponSkin` release package under:

- `shared/WeaponSkin.Shared`

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
