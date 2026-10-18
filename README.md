# Harbor Racer

A kart-racing game built in Unity as a **learning project**. It is a single-track racer with eight drivers, drifting, boosts, items, hazards, anti-gravity sections and glider ramps. The goal was to learn how a complete arcade racer fits together: kart physics, AI, item logic, camera, HUD, audio and build tooling.

> **Study project, not a product.** It is non-commercial, has no store page and is not affiliated with or endorsed by any game company. See [Disclaimer](#disclaimer).

## What it covers

| Area | What was built |
|---|---|
| Kart model | Speed-driven movement with ground alignment, off-road slowdown, wall sliding, respawn after falling or getting stuck |
| Drift and boost | Hop, drift lock, three charge tiers, stacking boosts, rocket start at the countdown |
| Track systems | Checkpoints and laps, anti-gravity sections, glider ramps, jump panels, trick zones |
| Items | Item boxes with a roulette, shells (green, red, blue), bananas, bombs, mushrooms, star, bullet, coins, triples that orbit or trail |
| Hazards | Trolleys, crates, barrels, cones, spectators |
| AI | Path-following opponents with rubber banding, drift use, item use, obstacle avoidance and stuck recovery |
| Driver animation | Hands on the wheel, item poses, throws, reaction faces, hit, trick, win and lose reactions |
| Kart body animation | Hit flips, bump leans, boost tilt, glider flips, anti-gravity spin |
| Camera | Chase camera with boost pull, anti-gravity roll, glide levelling, intro fly-in, finish orbit, first-person and look-back |
| UI | Race HUD, item slot, minimap, lap and position, results screen, pause |
| Audio | Engine mix, drift and boost sounds, item and hazard effects, ambient harbour sound |
| Tooling | Editor builders that regenerate prefabs, markers, hazards and HUD, and edit-mode test scripts |

## Requirements

- Unity **6000.6.6f1**
- Universal Render Pipeline (included in the project)
- Unity Input System (included in the project)
- Windows 10 or 11 for the standalone build

## Getting started

1. Clone the repository.
2. Open the folder with Unity Hub using the editor version above. The first import takes a few minutes.
3. Open `Assets/_Project/Scenes/ToadHarbor.unity` and press **Play**.

### Build a standalone player

`File > Build Profiles`, select **Windows**, add the `ToadHarbor` scene if it is missing, and build. Or use the menu item `Tools > HarborRacer > Build Windows Player`.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Accelerate | W or Space | A or right trigger |
| Brake and reverse | S | X or left trigger |
| Steer | A and D, or arrow keys | Left stick |
| Drift and trick | V or Left Shift | Right bumper |
| Use item forward | E or Right Shift | Left bumper |
| Use or drop item backward | Q or Down arrow | D-pad down |
| Glider pitch | Up and Down arrows | Left stick up and down |
| Look back | B (hold) | B (hold) |
| Toggle first-person camera | C | Right stick click |
| Skip intro | Enter | Start |
| Pause | Esc | Start |

## Project layout

```
Assets/_Project
  Scripts/        runtime code, grouped by feature (Kart, Items, AI input, Race, Track, Hazards, Camera, UI, Audio)
  Scripts/Editor/ builders and importers that regenerate scene content
  Art, Audio, Resources, Prefabs, Scenes, ScriptableObjects, Data
Tools/Tests/      edit-mode test scripts (physics, drift, items, AI, race flow) with a README
ProjectSettings/  Unity project settings
```

## Design notes

- **Kart movement.** Each fixed step the velocity is set from a commanded speed along the kart's forward axis. Rotation is frozen on the rigid body and the kart is aligned to the ground by probes. Wall and ground queries use rays and short sphere casts rather than collision callbacks, which keeps the behaviour predictable.
- **Tuning data.** Speeds, drift tiers and boost values live in a `KartStats` ScriptableObject.
- **Play-mode settings.** Domain reload is turned off for faster iteration, so static state is cleared explicitly in `StaticReset`.
- **One component per file.** Unity needs each MonoBehaviour in a file of the same name. A class tucked into another file works in the editor but corrupts scene data in a player build.
- **Animation without controllers.** Driver and kart-body clips are played with the Playables API, so no Animator Controller assets are needed.

## Tests

The scripts in `Tools/Tests` run inside the open editor, drive karts with scripted input and print a pass or fail report. They change the open scene, so reload it without saving afterwards. See `Tools/Tests/README.md` for the list and how to run them.

## Known gaps

- Item models are static. Spin and spawn animations are not attached yet.
- No throw delay on items.
- Star power uses a colour tint and aura rather than a full material swap.
- The AI can still hesitate on one curb near the harbour wall and recovers by itself.
- Windows only. Other platforms have not been tried.

## Assets and credits

**The art, models, textures, animations and audio in this repository are not my own work.** I downloaded them from sources on the internet that offer them as free downloads, and I use them here only to study how a game like this is put together. I did not create, commission or purchase any of them, and I do not hold the rights to them.

- Free to download does not mean free to reuse. Whatever licence or terms each original source has still applies, and the rights stay with the original creators and owners.
- The character, track and item names are used only to label things in a study project. They belong to their owners.
- Nothing in this repository grants you any right to those assets. If you want to reuse them, go back to the original source and read its terms.
- If you are the creator or rights holder of anything here and want it credited or removed, open an issue and I will do it promptly.

**What I wrote myself:** the gameplay code in `Assets/_Project/Scripts`, the editor tooling, the test scripts in `Tools/Tests`, the scene assembly and this documentation.

## Disclaimer

This project exists only to learn game development. It is not for sale, is not monetised, and is not affiliated with or endorsed by any game company or asset creator. Please do not use it commercially and do not repackage the third-party assets from this repository.

## License

The source code I wrote (see "What I wrote myself" above) is shared for learning. No licence has been chosen for it yet, so all rights are reserved until one is added. This does not cover the third-party assets described in "Assets and credits".
