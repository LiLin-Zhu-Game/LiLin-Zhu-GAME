# Gear Scavenger Enemy Roster

Each enemy must change the player's movement or weapon decision. New enemies
should not be added when their only difference is health or color.

| Enemy | HP | Speed | Primary attack | Combat purpose | First appearance |
| --- | ---: | ---: | --- | --- | --- |
| Ripper Chaser | 64 | 3.35 | Fast 13-damage melee contact | Forces movement and purge timing | Difficulty 1 |
| Hornet Drone | 46 | 2.25 | Accurate 9-damage single shot | Punishes standing still | Difficulty 1 |
| Scarab Support | 82 | 1.75 | Repairs nearby machines for 14 HP every 3.2 seconds | Creates a priority target and extends fights | Difficulty 2 |
| Centipede Bulwark | 180 | 1.35 | Periodic high-speed 22-damage ram | Blocks space and absorbs frontal pressure | Difficulty 3 |
| Wasp Artillery | 58 | 1.55 | Five-projectile fan barrage | Denies wide areas while remaining fragile | Difficulty 2 |
| Breaker Boss | 480 | 1.75 | Alternating aimed triple shot and radial barrage | Tests movement, purge, and target prioritization | Boss wave |

## Defensive Rules

- Bulwark receives 48% of incoming damage and only 20% of normal knockback.
- Boss receives 45% of normal knockback.
- Support machines buff nearby enemy movement and periodically repair allies.

## Attack Rules

- Chaser is the only regular enemy designed around repeated contact damage.
- Drone fires one fast, accurate projectile.
- Artillery fires a slow five-projectile fan and tries to stay at long range.
- Bulwark periodically charges instead of constantly moving at high speed.
- Support does not deal meaningful ranged damage; its value comes from keeping
  other threats alive.
- Boss alternates patterns. Below half health, movement, fire rate, radial
  projectile count, and projectile speed increase.

## Encounter Progression

- Difficulty 1 teaches movement against chasers and accurate drone shots.
- Difficulty 2 adds target prioritization through support repair and artillery.
- Difficulty 3 adds the armored bulwark to constrain movement.
- Later waves add artillery first, then bulwarks.
- Boss wave combines all pressure types but uses only one support machine.
