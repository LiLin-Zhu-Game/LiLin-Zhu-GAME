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
| Breaker Boss | 620 | 1.75 | Alternating aimed triple shot and radial barrage | Tests movement, purge, and target prioritization | Boss wave |
| Siege Titan Boss | 780 | 1.30 | Heavy fan/radial barrages and repeated high-speed charges | Forces lane changes and punishes staying near walls | Boss wave |
| Reactor Warden Boss | 540 | 2.10 | Continuous rotating spiral with periodic aimed volleys | Creates sustained bullet pressure and limits safe positions | Boss wave |

## Defensive Rules

- Bulwark receives 48% of incoming damage and only 20% of normal knockback.
- Breaker and Reactor Warden receive 35% of normal knockback.
- Siege Titan receives only 12% of normal knockback.
- Support machines buff nearby enemy movement and periodically repair allies.

## Room Alert Rules

- Entering a combat room immediately awakens every enemy assigned to that room.
- Awakened enemies remain active until defeated, even if the player moves far
  away inside the same room.
- Enemies stay leashed to their home room so adjacent rooms do not attack
  through connecting corridors.

## Attack Rules

- Chaser is the only regular enemy designed around repeated contact damage.
- Drone fires one fast, accurate projectile.
- Artillery fires a slow five-projectile fan and tries to stay at long range.
- Bulwark periodically charges instead of constantly moving at high speed.
- Support does not deal meaningful ranged damage; its value comes from keeping
  other threats alive.
- Breaker alternates aimed and radial patterns.
- Siege Titan alternates heavy fan and radial barrages while periodically
  charging across the arena.
- Reactor Warden strafes at range and continuously creates rotating spiral
  bullets with periodic aimed volleys.
- Every Boss becomes faster or more aggressive below half health.

## Encounter Progression

- Difficulty 1 teaches movement against chasers and accurate drone shots.
- Difficulty 2 adds target prioritization through support repair and artillery.
- Difficulty 3 adds the armored bulwark to constrain movement.
- Later waves add artillery first, then bulwarks.
- Boss wave combines three distinct Boss machines, one support machine, and
  two artillery enemies in a larger dedicated arena.
