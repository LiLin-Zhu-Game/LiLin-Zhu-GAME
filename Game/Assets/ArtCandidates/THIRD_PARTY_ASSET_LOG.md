# Third-Party Asset Log

Review license terms again before release. Store receipts and downloaded license
files outside the public repository when required.

Important: permission to use an asset in a game does not automatically grant
permission to expose its raw files in a public source repository. Confirm the
repository visibility and each license's redistribution terms before committing
third-party runtime sprites.

| Asset | Creator | Source | Intended use | Current status |
| --- | --- | --- | --- | --- |
| Top Down Robotic Enemies | Tyst | https://tyst.itch.io/top-down-robotic-enemies | Player scavenger animation base | Static preview prepared, zero-cost download |
| Free Robot Warfare Pack | MattWalkden | https://mattwalkden.itch.io/free-robot-warfare-pack | Primary enemies, wasteland tiles, props, projectiles, and effects | Static enemies prepared, free, CC0 |
| Robot Sprites | Vircon32 (Carra) | https://opengameart.org/content/robot-sprites | Backup player and wheel cannons | Selected backup, free, CC-BY 4.0 |
| Modular 64x Robots | croomfolk | https://croomfolk.itch.io/modular-64x-robots | Backup enemy variants | Selected backup, free, CC-BY-SA 4.0 |
| Robot's Last Stand | Ninbax_303 | https://ninbax-303.itch.io/robots-last-stand-asset-pack | Backup enemies and projectiles | Selected backup, free, CC0 |
| Mechs Assets free pack | MarineSosa | https://marinesosa.itch.io/mech-assets | Boss and heavy enemies | Static boss prepared, free tier available |
| Top Down Robot Enemies | Dr. Monkeystein | https://drmonkeystein.itch.io/top-down-robot-enemies | Earlier enemy reference | Not selected, paid |
| Mechanical wasteland map and weapons | Gear Scavenger project | Project-created candidate art | Floor, walls, props, and modular weapons | Static preview prepared |

## Runtime filename contract

The current game loads static sprites from `Assets/Resources/GearScavenger`:

- `player.png`
- `enemy_chaser.png`
- `enemy_drone.png`
- `enemy_support.png`
- `enemy_boss.png`

Keep those files unchanged until a prepared candidate has been reviewed.
