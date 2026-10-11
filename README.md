# QuasiRobotics

Workshop ID: 3817709455

**Requires [Mod Configuration Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3469678797).**

**Not required, but recommended [Direct Follower Orders](https://steamcommunity.com/sharedfiles/filedetails/?id=3784424405).**

Build, upgrade, and field a squad of AR robots alongside your operator. This was somewhat inspired by the [Squad: More Operatives](https://steamcommunity.com/sharedfiles/filedetails/?id=3776322157) and [AR Recovery](https://steamcommunity.com/sharedfiles/filedetails/?id=3787327571) mods, but with a different take on combating the ever-growing horde of enemies the game starts to throw at you. I love playing with summons in games if I can, and this was the mod that I created in order to scratch that itch. There is some warped humour I included in small parts, but typically I just made sure that each aspect of the mod met my own personal level of polish (and I hope that reflects).

## Robotics Ship Room

A new Room on the HIS Magnum, **Robotics (HIS Droid R&D)**, with its own technology tree:
- **Printyard**- Unlocks the Robot Roster and lets Robots join deployments alongside your Clone.
- **Salvage Drones**: Robots destroyed on a mission come back to the ship for repair instead of being lost (they keep any Armour or Weapons they had equipped).
- **Drop Pods**: From Mission Control (like the Capsule), call any Robots left aboard down onto the map in explosive pods. 2 charges, 48h recharge, and has its own rebindable hotkey.
- **Upgrades**: Health, Resistances, Accuracy, Action Points, Health Regen, faster repairs, fire suppression, and even more upgrades for your Robot army.

## Robot Roster?

- Bring home AR packages (Combat, Industrial and Recreational) and add them to the **Robots** tab in Manage Operators once you've unlocked the Printyard.
- Rename your Robotic friends, change their gear and augments, or dismantle them for parts.
- Robots keep their name, gear and upgrades between missions, even when revived with a Reanimatron.
- Add Robots on the departure screen with **Add Robot**. They land next to the shuttle and follow you through the mission like VIPs would.
- Damaged Robots repair aboard the Ship over time.

## Also included

- **No Allied Friendly Fire** (on by default): Your robots, turrets and other allies no longer hurt you or each other. Loosely based on [No Friendly Fire](https://steamcommunity.com/sharedfiles/filedetails/?id=3800002169).
- Mod Configuration Menu options: Start with Printyard, Salvage Drones or Drop Pods, robot slots per upgrade, the Drop Pods key, friendly fire, and an uninstall mode.

## Adding to and removing from a campaign

- Works in existing campaigns by making the Robotics room appear when you load a save if it was missing.
- To remove the mod turn on **Uninstall mode** in Mod Configuration Menu and save on the ship. Robots go back to cargo as default packages and Robotics devices are refunded. Then the mod can be unsubscribed.

## Translations

All text lives in the mod's **Localization** folder, one file per game language. I only speak English and didn't want to insult your mother-tongue by having AI translate, that being said I did try to structure the mod to make translation work as easy as possible, so if you're willing to translate it to your native language you can either ping me on the Quasimorph Discord or make a PR on the Git repo.

Currently supported languages:

- English

## Artwork

All of the art was hand-crafted by my silly broken hands or pulled straight from the game's own assets. That being said I'm definitely no Da Vinci so if you're a pixel artist and wish to contribute then I'll readily accept the work of my betters to improve the mod.

## Loving the mod?

You can say thanks [here](https://ko-fi.com/emousedhm01) if you appreciate the hard work and time that went into the creation of this mod. Any and all thanks makes creating stuff like this worth doing, so thank *you* in advance.

---

## For developers

### Layout
```
QuasiRobotics.sln
Directory.Build.props     version, game path, LocalUserPresets path
src/QuasiRobotics/        the mod (net48)
  Plugin.cs               entry point, applies Harmony patches
  Core/                   logger, config folder, settings
  Packages/               recovered AR packages and the package editor
  Roster/                 robot roster data and UI
  Deployment/             departure screen, mission spawn and recovery
  Robotics/               Robotics ship room and its devices (Printyard, Salvage Drones, Drop Pods)
  Patches/                Harmony patches
  Console/                dev console commands (qr_deployer)
  Legacy/                 loads saves made with AR Recovery
  Assets/                 embedded PNGs
Localization/             all mod text, one JSON file per game language (see Localization/README.md)
media/                    Workshop thumbnail source and screenshots
docs/                     Workshop page text
tests/QuasiRobotics.Tests/  headless checks against the real game DLL
workshop/                 exactly what is uploaded to Steam
tools/                    asset and prefab extraction scripts
game-reference/           decompiled game code, local only, never published
```

### Requirements
Mod Configuration Menu (`Crynano_ModConfigMenu`) must be installed: it is a manifest
dependency and the build references its MCM.dll. Elsewhere: add
`-p:McmDir="path\to\folder\with\MCM.dll"`.

### Build and test
Release builds copy the DLL into workshop/ and deploy workshop/ to
`%USERPROFILE%\AppData\LocalLow\Magnum Scriptum Ltd\Quasimorph\LocalUserPresets\QuasiRobotics`.
Restart the game to load a new build.

    .\build.ps1               tests, then build and deploy
    .\build.ps1 -SkipTests    build and deploy only
    .\build.ps1 -NoDeploy     tests and build, no deploy

Or by hand:

    dotnet build src/QuasiRobotics -c Release
    dotnet run --project tests/QuasiRobotics.Tests -c Release

Game installed elsewhere: add `-p:QuasimorphDir="D:\path\to\Quasimorph"`.
Skip deploying: add `-p:Deploy=false`.

