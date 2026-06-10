# Gear Scavenger Release and Playtest Checklist

Use this checklist before a presentation build or GitHub release. The short
end-to-end route uses Training Mode; Story and Challenge remain the full game.

## Build

- [ ] Open the project with Unity `2022.3.62f3c1`.
- [ ] Wait for Unity to finish importing and compiling.
- [ ] Run `Gear Scavenger > Run Release Readiness Check`.
- [ ] Confirm the Console contains no red errors.
- [ ] Run `Gear Scavenger > Build Windows Demo`.
- [ ] Confirm `Game/Builds/Windows/Gear Scavenger.exe` launches.

## Five-Minute End-to-End Route

- [ ] Launch the built game and select **Training Mode**.
- [ ] Open and close **Tutorial**, then press **Start Game**.
- [ ] Confirm WASD movement, mouse aiming/fire, Space dash, and Esc pause/resume.
- [ ] Approach a weapon drop; confirm the comparison panel displays damage,
      fire rate, heat per shot, and projectile behavior.
- [ ] Press E to equip a weapon without pausing combat.
- [ ] Enter each room; confirm every enemy in that room wakes and attacks.
- [ ] Confirm chaser, drone, and support anticipation animations are distinct.
- [ ] Confirm enemy hit flash and death animation are visible.
- [ ] Complete Training Waves 1-3 and retain the equipped weapon between maps.
- [ ] Defeat the three Wave 4 Bosses.
- [ ] Confirm **Mission Complete** appears and returns to the main menu.
- [ ] Target elapsed time: `4:00-6:00`.

## Visual and Stability Pass

- [ ] Player, enemies, weapons, props, and hazards are readable at gameplay zoom.
- [ ] HUD clearly shows health, armor, heat, scrap, weapon, wave, and objective.
- [ ] No weapon comparison panel remains after moving away from a pickup.
- [ ] No enemy deals duplicate damage during an anticipation or death animation.
- [ ] F11 toggles fullscreen correctly.
- [ ] No blocking Console errors occur from menu to Boss victory.

## Release Evidence

Record the build date, tester, elapsed time, result, and any issue number here:

| Date | Tester | Mode | Time | Result | Notes / Issue |
| --- | --- | --- | --- | --- | --- |
| YYYY-MM-DD | Name | Training | 00:00 | Pass / Fail | |

## Related Documents

- Controls and project overview: `README.md`
- Credits and licenses: `CREDITS_AND_LICENSES.md`
- Known issues: `KNOWN_ISSUES.md`
- Detailed game values: `GAME_DATA_REFERENCE.md`
