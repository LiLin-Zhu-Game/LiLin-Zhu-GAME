# Gear Scavenger Project Board

This plan continues after the first 11 completed issues.

## Board Fields

- Status: Backlog, Ready, In Progress, Review, Done
- Priority: Must-have, Should-have, Could-have, Cut-first
- Area: Combat, Enemies, Weapons, Art, Level, UI, Audio, Release
- Sprint: Vertical Slice 1, Polish, Release

## Recommended Views

- Current Sprint: grouped by Status, filtered to Vertical Slice 1
- Must-have: filtered to Must-have
- Art Pipeline: filtered to Art
- Release Readiness: filtered to Release and grouped by Status

## Completed Baseline

Keep the first 11 completed issues in `Done` and attach their relevant commit or
screenshot as completion evidence. Together they established player movement,
rooms, enemy spawning, the core combat loop, heat and purge, robot abilities,
weapon drops, feedback, and the main menu.

## Next Issues

### 12. Integrate candidate robot art

- Priority: Should-have
- Area: Art
- Sprint: Vertical Slice 1
- Acceptance:
  - Player, chaser, drone, support, and boss use the approved free-art lineup.
  - Each enemy role remains readable during combat.
  - Original sprites remain available as fallback.
  - Third-party licenses and attribution notes are recorded.

### 13. Add directional player animation

- Priority: Should-have
- Area: Art
- Sprint: Vertical Slice 1
- Acceptance:
  - Player movement displays directional animation.
  - Shooting and movement can play without visual snapping.
  - Animation does not affect collision or movement speed.

### 14. Add enemy attack and damage animation

- Priority: Should-have
- Area: Enemies
- Sprint: Vertical Slice 1
- Acceptance:
  - Chaser, drone, and support attacks have distinct anticipation.
  - Hit and death feedback is visible.
  - Animation events cannot cause duplicate damage.

### 15. Build modular weapon selection UI

- Priority: Must-have
- Area: Weapons
- Sprint: Vertical Slice 1
- Acceptance:
  - Player can compare and equip weapon drops during a run.
  - Heat, damage, fire rate, and projectile behavior are displayed.
  - Selection does not break the combat loop.

### 16. Polish boss encounter

- Priority: Must-have
- Area: Enemies
- Sprint: Vertical Slice 1
- Acceptance:
  - Boss has at least three readable attack patterns.
  - Weapon adaptation and purge are useful during the fight.
  - Defeating the boss completes the demo flow.

### 17. Add final HUD and tutorial messaging

- Priority: Should-have
- Area: UI
- Sprint: Polish
- Acceptance:
  - Health, armor, heat, scrap, weapon, and objective are readable.
  - Controls and purge behavior are explained during the first run.
  - Debug-only HUD text is removed from the release view.

### 18. Add combat audio and ambience

- Priority: Could-have
- Area: Audio
- Sprint: Polish
- Acceptance:
  - Weapons, hits, purge, pickups, enemies, and boss have distinct audio.
  - Mechanical wasteland ambience supports the single biome.
  - Audio levels remain balanced during dense combat.

### 19. Create release build and playtest checklist

- Priority: Must-have
- Area: Release
- Sprint: Release
- Acceptance:
  - A clean five-minute run can be completed from menu to boss victory.
  - No blocking console errors occur.
  - Build, controls, credits, licenses, and known issues are documented.
