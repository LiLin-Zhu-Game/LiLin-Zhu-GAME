# Gear Scavenger Full Game Data Table
This document records the functional operational values active in the current code. Theoretical DPS assumes all projectiles land and does not account for overheating, dashing, range, spread, or enemy armor.

## 1. Base Player Stats
| Stat | Value |
| --- | ---: |
| Starting/Max Core Health | 100 |
| Starting/Max Armor | 100 |
| Base Movement Speed | 6.2 |
| Dash Speed | 12 |
| Dash Duration | 0.12 seconds |
| Dash Cooldown | 0.9 seconds |
| Starting Scrap Pickup Radius | 1.45 |
| Scrap Magnetisation Start Distance | 2.2 times pickup radius, starting at 3.19 |
| Resource Value per Scrap Piece | 1 Scrap Unit |
| Armor Restored per Scrap Piece | 4 |
| Max Heat Threshold | 100 |
| Passive Heat Dissipation Rate | 18 Heat per second |
| Overheat Recovery Threshold | Heat reduced to 32 |

Enemy damage depletes armor first; core health only takes damage once armor is fully exhausted. The run ends in failure when core health hits 0.

## 2. Player Active Abilities
| Ability | Hotkey | Cost / Requirement | Effect |
| --- | --- | --- | --- |
| Dash | Space | 0.9 second cooldown | Move at 12 speed for 0.12 seconds |
| Purge | R | Minimum 24.75 Heat | Reduce heat by 45; 3.1 radius, deal 36 damage and apply 9 knockback |
| Scrap Nova | Q | 10 Scrap Units | 4.2 radius; 58 central damage, ~26 edge damage; 13 knockback; generate 22 Heat |
| Magnetic Guard | F | 6 Scrap Units | Lasts 4.5 seconds; restore 16 armor; reduce heat by 26; incoming damage cut by 55% |

## 3. Weapon Statistics
Parameter order matches code order: Fire Interval, Heat Per Shot, Damage Per Projectile, Projectile Speed, Spread Angle, Projectile Count, Projectile Lifespan. Theoretical range is calculated as `Projectile Speed × Projectile Lifespan`.

| Weapon | Damage Per Projectile | Projectile Count | Total Damage Per Shot | Fire Interval | Theoretical DPS | Heat Per Shot | Heat Per Second | Projectile Speed | Spread Angle | Lifespan | Theoretical Range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Rust Rifle | 18 | 1 | 18 | 0.14 | 128.6 | 9.5 | 67.9 | 13 | 3.5° | 1.6 | 20.8 |
| Scatter Core | 12 | 5 | 60 | 0.28 | 214.3 | 17 | 60.7 | 11 | 12° | 1.15 | 12.65 |
| Beam Needle | 9 | 1 | 9 | 0.08 | 112.5 | 6.5 | 81.3 | 17 | 1.2° | 1.25 | 21.25 |
| Scrap Cannon | 42 | 1 | 42 | 0.48 | 87.5 | 24 | 50 | 9.5 | 4° | 1.9 | 18.05 |
| Coil Ripper | 7 | 1 | 7 | 0.055 | 127.3 | 4.8 | 87.3 | 18 | 5.5° | 1.05 | 18.9 |
| Arc Splitter | 14 | 3 | 42 | 0.24 | 175 | 15 | 62.5 | 12 | 18° | 1.35 | 16.2 |
| Heat Lance | 70 | 1 | 70 | 0.68 | 102.9 | 34 | 50 | 20 | 0.6° | 1.1 | 22 |
| Nanite Swarm | 8 | 6 | 48 | 0.18 | 266.7 | 11 | 61.1 | 10 | 28° | 1.65 | 16.5 |
| Rail Spike | 52 | 1 | 52 | 0.38 | 136.8 | 20 | 52.6 | 24 | 0.35° | 1.35 | 32.4 |
| Pulse Sprayer | 11 | 2 | 22 | 0.1 | 220 | 7.2 | 72 | 14 | 15° | 1.4 | 19.6 |

Players start equipped with the Rust Rifle. Three pickupable weapons spawn permanently at the starting zone: Scatter Core, Beam Needle, and Scrap Cannon.

## 4. Skill Cores
| Skill Core | Type | Functional Effect |
| --- | --- | --- |
| Kinetic Overdrive | Temporary | Lasts 24 seconds; 25% movement speed boost to 7.75; 28% reduced fire interval |
| Coolant Matrix | Permanent | Weapon heat generation multiplied by 0.78; instantly clear all accumulated heat |
| Nanite Shell | Permanent | Max armor increased by 25; restore 45 armor and 20 core health |
| Salvage Magnet | Permanent | Pickup radius increased by 0.9 from 1.45 to 2.35; grant 8 Scrap Units, restoring a maximum of 32 armor |

## 5. Enemy Statistics
| Enemy | HP | Movement Speed | Contact Damage | Contact Hit Interval | Primary Attacks & Mechanics | Total Scrap Drop On Death |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| Ripper Chaser | 64 | 3.35 | 13 | 0.55 seconds | Fast melee pursuer | 4 |
| Hornet Drone | 46 | 2.25 | 8 | 0.8 seconds | Fires 1 precision projectile every 1.35 seconds; 9 damage, 8.8 projectile speed, 2.1 second lifespan | 4 |
| Scarab Support | 82 | 1.75 | 8 | 0.8 seconds | Heal injured allies within 4.5 radius for 14 HP every 3.2 seconds; buff speed of other enemies in 4 radius by 35% | 7 |
| Centipede Bulwark | 180 | 1.35 | 22 | 1.15 seconds | Charge forward for 0.65 seconds every 3.8 seconds; charge speed roughly 3.8x base movement speed | 13 |
| Wasp Artillery | 58 | 1.55 | 8 | 0.8 seconds | Fires a spread of 5 projectiles every 2.45 seconds; 6 damage per projectile, 5.4 speed, 2.7 second lifespan | 6 |
| Breaker Boss | 620 | 1.75 | 24 | 0.8 seconds | Alternates targeted triple shot and circular barrage; enters Phase 2 when HP drops below half | 22 |
| Siege Titan Boss | 780 | 1.30 | 30 | 0.8 seconds | Heavy spread/circular barrages; periodic high-speed charges; charges and fire rate become more frequent below half HP | 22 |
| Reactor Warden Boss | 540 | 2.10 | 18 | 0.8 seconds | Constant rotating spiral barrage; periodic targeted volleys; additional spiral arms and faster fire rate below half HP | 22 |

### Enemy Defense, Distance & Special Rules
| Rule | Value |
| --- | --- |
| Damage Received – Centipede Bulwark | 48% of incoming raw damage (~52% damage resistance) |
| Knockback Received – Centipede Bulwark | 20% standard knockback force |
| Knockback Received – Breaker / Reactor Warden | 35% standard knockback force |
| Knockback Received – Siege Titan | 12% standard knockback force |
| Boss Phase 2 Activation Threshold | HP ≤ 50% of maximum health |
| Room Alert State Rule | Upon player entry into a combat room, all enemies in the room permanently activate alert status; no proximity trigger required for individual enemies |
| Hornet Drone Retreat Distance | Drone pulls back when player distance < 4.2 |
| Scarab Support Halt Movement Range | Stops moving if player distance is between 3.5 and 5 |
| Wasp Artillery Halt Movement Range | Stops moving if player distance is between 5.8 and 7.8 |

### Three Boss Attack Breakdown
| Phase | Attack Pattern | Stats |
| --- | --- | --- |
| Phase 1 | Targeted Triple Shot | Central projectile: 13 damage, 9 speed; side projectiles: 9 damage each, 8 speed |
| Phase 1 | Circular Barrage | 8 projectiles; 7 damage each, 6.2 speed |
| Phase 1 | Attack Cycle Interval | 1.05 seconds |
| Phase 2 | Targeted Triple Shot | Central projectile speed raised to 10.5; damage unchanged |
| Phase 2 | Rotating Circular Barrage | 12 projectiles; 7 damage each, 7.2 speed |
| Phase 2 | Attack Cycle Interval | 0.72 seconds |

- **Siege Titan**: Alternates volleys of 5 heavy spread shots and 12 circular shots; below half HP, heavy shots fire faster and circular barrage expands to 16 projectiles; executes periodic high-speed charges.
- **Reactor Warden**: Continuously fires rotating spiral barrages mixed with periodic targeted triple shots; below half HP, spiral barrage arms increase from 2 to 4, attack interval shortened from 0.38 seconds to 0.26 seconds.

## 6. Enemy Loot & Weapon Rewards
Total scrap dropped on enemy death equals base scrap drop plus type bonus scrap; values listed in the enemy stats table reflect this combined total.

| Enemy | Base Scrap Drop | Type Bonus Scrap | Total Scrap Yield |
| --- | ---: | ---: | ---: |
| Ripper Chaser | 2 | 2 | 4 |
| Hornet Drone | 2 | 2 | 4 |
| Scarab Support | 3 | 4 | 7 |
| Centipede Bulwark | 7 | 6 | 13 |
| Wasp Artillery | 2 | 4 | 6 |
| Boss Enemies | 12 | 10 | 22 |

- Standard enemies have an 18% chance to drop a random weapon upon death.
- Bosses guarantee a random weapon drop on defeat.
- Every sixth machine eliminated spawns an additional fixed weapon reward scaled to wave progression.
- Collecting each scrap piece simultaneously adds 1 scrap resource and restores 4 armor.

## 7. Destructible Objects & Room Interactables
| Object | HP | Scrap Drop | Weapon Drop Chance | Unique Effect |
| --- | ---: | ---: | ---: | --- |
| Breakable Crate | 36 | 3 | 8% | Blocks player movement and projectiles |
| Scrap Barrel | 34 | 2 | 4% | Blocks player movement and projectiles |
| Volatile Fuel Barrel | 28 | 6 | 14% | Explosion radius 2.4; deals 30 damage and 5.5 knockback to enemies; player within 2.04 radius takes 8 damage |
| Terminal | 58 | 5 | 16% | Blocks player movement and projectiles |
| Reinforced Barricade | 92 | 4 | 6% | High-durability cover, blocks movement and projectiles |
| Scrap Machinery | Indestructible | 0 | 0% | Permanent impassable terrain |

| Interactive Station | Functional Effect |
| --- | --- |
| Repair Station | Restores 10 armor every 1.25 seconds while player remains within bounds |
| Cooling Station | Reduces heat by 24 every 1.25 seconds while player remains within bounds |
| Coolant Recovery Zone | Dissipates ~32 heat per second for players; enemy movement speed reduced to 52% baseline |
| Unstable Shock Field | Initial pulse delay randomized between 0.5–1.2 seconds; subsequent pulses every 1.65 seconds; deals 16 damage to alerted enemies, 7 damage to player |
| Recovered Defense Turret | Requires player within 5.6 radius; targets alerted enemies within 6.2 radius; fires 13-damage bullets every 0.72 seconds, 13 projectile speed, 1.3 second lifespan |

### Object Visual Scaling Sizes
| Object | Current Visual Scale Multiplier |
| --- | ---: |
| Breakable Crate | 1.05 |
| Standard / Volatile Fuel Barrel | 1.08 |
| Terminal | 1.28 |
| Scrap Machinery | Original layout scale × 1.05 |
| Reinforced Barricade | 1.9 × 0.9 |
| Repair / Cooling Station | 1.9 |
| Defense Turret | 1.6 |
| Skill Core | 1.15 scale with ±12% breathing animation |
| Coolant Recovery Zone | ~3.1 unit diameter |
| Shock Field | Diameter equals twice configured radius with pulsing animation |

## 8. Rooms & Map Layout Structure
| Room Name | Central Coordinates | Dimensions | Difficulty Tier | Layout & Facilities |
| --- | --- | --- | ---: | --- |
| Start Workshop | (0, 0) | 10 × 8 | 0 | Non-combat zone; Repair Station; starter weapons spawn |
| Scrap Yard | (-13, 0) | 9 × 8 | 1 | Shock Field, Reinforced Barricades, Kinetic Overdrive Core |
| Assembly Maze | (13, 0) | 9 × 8 | 2 | Shock Field, two Reinforced Barricades, Coolant Matrix Core |
| Cache Room | (0, 10) | 8 × 7 | 2 | Coolant Recovery Zone, Allied Defense Turret, Cooling Station, Nanite Shell Core |
| Reactor Yard | (0, -10) | 10 × 7 | 3 | Two Shock Fields, two Reinforced Barricades, Salvage Magnet Core |
| Isolated Boss Arena | (0, 0) | 18 × 12 | Boss Tier | Spawns exclusively in Wave 4; contains all three unique Bosses, Scarab Supports, Wasp Artillery, Barricades, Shock Fields and a Repair Station |

Four connecting corridor dimensions: `4 × 3`, `4 × 3`, `3 × 4`, `3 × 4`.
Each floor tile has a 12% chance to spawn cosmetic ground detail props.

## 9. Per-Wave Enemy Compositions
Every standard wave regenerates fixed enemy groups across all four combat rooms:

| Room | Fixed Enemy Group Spawns |
| --- | --- |
| Scrap Yard | 2 Ripper Chasers + 1 Hornet Drone |
| Assembly Maze | 2 Ripper Chasers + 1 Hornet Drone + 1 Scarab Support + 1 Wasp Artillery |
| Cache Room | 2 Ripper Chasers + 1 Hornet Drone + 1 Scarab Support + 1 Wasp Artillery |
| Reactor Yard | 2 Ripper Chasers + 1 Hornet Drone + 1 Scarab Support + 2 Wasp Artillery + 1 Centipede Bulwark |

Total fixed standard wave enemy count: 20 units, including 8 Ripper Chasers, 4 Hornet Drones, 3 Scarab Supports, 4 Wasp Artillery, and 1 Centipede Bulwark.

| Wave | Full Enemy Composition | Total Enemies |
| --- | --- | ---: |
| Wave 1 | Base standard fixed group | 20 |
| Wave 2 | Base fixed group + 2 Ripper Chasers + 1 Hornet Drone | 23 |
| Wave 3 | Base fixed group + 3 Ripper Chasers + 2 Hornet Drones + 1 Wasp Artillery | 26 |
| Wave 4 Boss Arena | Breaker Boss + Siege Titan Boss + Reactor Warden Boss + 1 Scarab Support + 2 Wasp Artillery | 6 |

- After clearing all enemies across the four combat rooms in Waves 1–3, the active map is destroyed and a new four-room map generates immediately.
- Player-held weapons, scrap resources, core health, armor, and skill core upgrades persist through map transitions.
- Upon completing Wave 3, the isolated dedicated Boss Arena spawns; Wave 4 combat occurs solely within this arena.

## 10. Wave Completion Rewards & Game Modes
| Clear Reward | Stat Bonus Value |
| --- | --- |
| Wave 1 Complete | Restore 22 armor and 15 core health; unlock first Salvage Core; 1 random weapon spawns on new map |
| Wave 2 Complete | Restore 26 armor and 18 core health; unlock second Salvage Core; 1 random weapon spawns on new map |
| Wave 3 Complete | Restore 30 armor and 21 core health; unlock third Salvage Core; unlock isolated Wave 4 Boss Arena |

| Game Mode | Core Distinction |
| --- | --- |
| Story Mode | Balanced baseline stats and standard wave spawn rules |
| Challenge Mode | Additional Wave 3 pressure group spawns at run start: 3 Ripper Chasers, 2 Hornet Drones, 1 Wasp Artillery |
| Training Mode | 2 extra random weapons spawn at starting zone; total of 5 pickupable weapons available at spawn |

## 11. Controls & Interaction Ranges
| Input Action | Controls & Parameters |
| --- | --- |
| Player Movement | WASD keys |
| Aiming / Firing Weapons | Mouse to aim, Left Mouse Button to shoot |
| Dash Ability | Spacebar |
| Weapon Pickup & Equip | Press E when within 2.25 unit distance of weapon spawn |
| Purge Ability | R key |
| Scrap Nova Ability | Q key |
| Magnetic Guard Ability | F key |
| Pause / Resume Gameplay | Esc key |
| Return to Main Menu Post-Victory / Defeat | RETURN TO MAIN MENU button on results screen |