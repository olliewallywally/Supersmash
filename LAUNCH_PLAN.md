# Supersmash — Launch Plan & Audit

Last updated: 2026-06-09 · Status: **pre-alpha, feature-complete core, unverified in-engine**

---

## 1. The single biggest risk (read this first)

**This codebase has never been compiled or run.** The development environment has
no .NET SDK and no Godot binary, so every line of C# and every `.tscn` has been
verified by review and cross-referencing only. The review has been systematic
(state-target/animation/hitbox-node cross-checks all pass, `load_steps` audited),
but reviews are not builds.

**P0 — before anything else:** open the project in Godot 4.3 (Mono), let it
import, hit Build, and fix whatever falls out. Expected failure classes, in
order of likelihood:

1. `.tscn` exported-property spelling mismatches (C# `[Export]` name vs scene file key)
2. Minor C# API drift (method/enum names that moved between Godot 4.x minors)
3. Resource path typos

Everything else in this plan assumes that smoke pass is done.

---

## 2. Audit findings — already fixed (this commit)

| # | Severity | Issue | Fix |
|---|----------|-------|-----|
| 1 | High | `load_steps` wrong in 6 scene/resource files — import warnings/failures | Recounted programmatically; all 19 files verified |
| 2 | High | No platform drop-through — down on a platform just crouched (core mechanic missing) | Platforms moved to collision layer 5; `DropThroughFrames` window on hard-down in CrouchState |
| 3 | Medium | Game-over "2.5 s" menu return actually took ~8 s (SceneTreeTimer scaled by the 0.3× slow-mo) | `CreateTimer(..., ignoreTimeScale: true)` |
| 4 | Medium | Grab's first active frame could never connect (Area2D overlap list refreshes one physics frame after `Monitoring = true`) | Monitoring enabled one frame early |
| 5 | Medium | Dash-grab auto-threw instantly (stick still held from approach selected a throw on the catch frame) | 6-frame gate before throw input is read |
| 6 | Medium | Both players visually identical (same placeholder sheet) | Per-player warm/cool `Modulate` tint applied at spawn |
| 7 | Low | Pixel art rendered blurry (default linear filter) | Project-wide nearest filtering + 1280×720 `canvas_items` stretch |
| 8 | Low | Air dodge landed with only 2-frame lag — panic-dodge was free | 10-frame air-dodge landing lag |
| 9 | Low | Landing/footstep SFX existed but were never played | Wired into LandingState / RunState |

Fixed in earlier commits, recorded for completeness: aerial attacks froze midair
(no gravity in AttackState); hitboxes never mirrored with facing (HitboxRoot
flip); missing `p1_*`/c-stick input actions error-spammed every tick; CrouchState
mutated a collision shape shared across all fighter instances; percent didn't
reset on death.

## 3. Known issues — open, accepted for now

| # | Issue | Why deferred | Trigger to revisit |
|---|-------|--------------|--------------------|
| A | Grounded attacks slide off ledges into a midair "ground" attack until recovery ends | Rare, short windows; needs edge-stop logic in AttackState | First playtest complaint |
| B | Training dummy waits the full 5 s respawn float (samples zero input, never cancels) | Cosmetic in training; RespawnState could auto-drop for `IsAI` | When dummy testing feels slow |
| C | `AudioManager` routes to an "SFX" bus that doesn't exist (falls back to Master) | Harmless until a mixer exists | When adding volume options |
| D | Shield press isn't consumed on state entry — 1-frame taps can re-enter ShieldState for a few frames | Invisible in practice (hold-based input) | Never, unless input feel complaints |
| E | Throws use fixed code-side data, not per-character `.tres` | Correct architecture comes with a Throws section in AttackLibrary | When tuning Falcon/Marth throws |

## 4. Missing before a public launch — prioritized

### P1 — correctness & playability (blockers)
1. **In-engine smoke pass** (see §1) — compile, boot menu, play a stock match,
   verify every state transition reachable. *0.5–1 day with engine access.*
2. **Tuning pass on knockback/hitstun feel.** All values are meleelight-derived
   but the KB→velocity scale (`KbScale = 18`) and hitstun coefficient (0.4×KB)
   have never been felt. Expect kill percents to be wrong by ±40% on first run.
   *1–2 days of playtesting.*
3. **Pause menu** (resume / restart / quit to menu). Esc currently hard-exits the
   match — losing a match to a misclick is launch-blocking. *Half day.*
4. **Hitstun → tumble/knockdown flow.** Currently hitstun ends straight into
   Idle even at 150%. Knockdown + getup (anims already in the SpriteFrames:
   `knock_down`, `get_up`) + tech window is the biggest gameplay-depth gap.
   *1–2 days.*

### P2 — completeness (launch-quality, not launch-blocking)
5. **Missing move slots**: DashAttack, DownTilt, UpSmash, DownSmash, UpAir,
   DownAir. The data pipeline (AttackLibrary + named hitboxes) makes each ~30 min
   of data + a hitbox node. Melee values already researched for Fox/Falco/Falcon/Marth.
6. **Up-specials / recovery moves** (Firefox, Falcon Dive…). HelplessState and
   LedgeZone already support the flow; needs a `UpSpecial` AttackData variant
   with a velocity component. *1 day.*
7. **Per-character sprites.** All five archetypes share the tinted Adventurer.
   Either commission/source per-character CC0 sheets or recolor-swap the
   Adventurer per archetype. *Asset work, parallelizable.*
8. **Results screen** (winner, stocks taken, damage dealt) instead of the 2.5 s
   banner. *Half day.*
9. **Music** (menu + 1 stage track; Kevin MacLeod CC-BY or Kenney music packs).

### P3 — juice & competitive depth (post-launch fine)
10. Screen shake on heavy hits (camera already centralized — trivial hook).
11. KO blast VFX/SFX at blast-zone crossing.
12. L-cancel (hook exists in AttackState landing-cancel), SDI during hitlag,
    shield tilting, ledge trump, crouch-cancel.
13. CPU opponent (TrainingDummyComponent → simple chase/attack brain).
14. Rollback netcode — architecture is already fixed-tick + integer frames
    (deliberately), but this is a quarter-scale project. Do not start before
    local play is polished.

### P4 — release engineering
15. **CI build** — GitHub Action with `godot-ci` (headless import + `dotnet build`)
    so no future commit can break compilation invisibly. *Highest leverage item
    on this list after the smoke pass.* Needs `workflow` permission on push.
16. Export presets (Windows/Linux/Web) + itch.io page.
17. License/credits screen surfacing `Resources/Sprites/CREDITS.txt` (CC-BY
    attribution for rvros Adventurer is **legally required** in shipped builds).

---

## 5. Suggested execution order

```
Week 1: §1 smoke pass → P1.2 feel tuning → P1.3 pause → P4.15 CI
Week 2: P1.4 knockdown/tech → P2.5 move slots → P2.6 up-specials
Week 3: P2.7 sprites → P2.8 results → P2.9 music → P4.16 exports → ship alpha
```

The only items that genuinely gate "someone outside the team can play it":
smoke pass, feel tuning, pause menu, and the CC-BY credits screen.
