# Gear Scavenger Art Candidates

This folder stages third-party art before it is approved for use in the playable
build. Keep source packages and license notes here. Only copy approved, prepared
sprites into `Assets/Resources/GearScavenger`.

## Selected free visual roles

| Game role | Selected source | Intended treatment |
| --- | --- | --- |
| Player scavenger | Tyst Top Down Robotic Enemies | Recolor one complete robot animation set with cyan highlights |
| Chaser, drone, and support enemies | Free Robot Warfare Pack | Use the five robot insects and recolor accents by gameplay role |
| Boss and heavy enemies | Mechs Assets free pack | Use large mechs for slow heavy units and projectile-focused bosses |
| Backup and extra variants | Robot's Last Stand / Vircon32 Robot Sprites | Fill gaps without changing the primary visual direction |

All selected sources have a zero-cost download option. On itch.io, `Name your
own price` pages can be downloaded for zero by selecting `Download Now`, then
`No thanks, just take me to the downloads`.

## Art direction

- Top-down pixel art with a readable silhouette at normal gameplay zoom.
- Worn steel, rust brown, soot black, and faded hazard yellow are the shared
  material palette.
- Player accent: cyan.
- Chaser accent: red.
- Drone accent: amber.
- Support accent: green.
- Boss accent: magenta or hot red.
- Preserve transparent backgrounds and use point filtering in Unity.

## Approval workflow

1. Place untouched downloads in each source's `SourcePackage` folder.
2. Keep the source license or a saved copy of the store page in `License`.
3. Extract working files into `Working`.
4. Recolor, resize, and prepare animation sheets in `Prepared`.
5. Review the prepared sprites in-game.
6. Copy only approved runtime assets into `Assets/Resources/GearScavenger`.

The current static preview is stored in `PreparedRuntime`. While running in the
Unity editor, the game loads matching PNG files from this folder before falling
back to the original runtime sprites.

Do not commit paid source packages unless their license explicitly permits
redistribution through a public repository. Prepared derivative sprites should
also remain out of a public repository when the source license forbids sharing
asset files.
