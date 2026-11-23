# WRECKED — briefing for Claude

You are continuing work on **WRECKED**, a multiplayer VR ape brawler for Meta Quest, with the game's creator. Read this before doing anything.

## How to work with the creator (most important)

- The creator gives **strategic, verbal-level direction** (often dictated). They do **not** want technical tasks. Do the work yourself: edit files, run Unity checks, commit and push. Never ask them to download, upload, install, copy files or paste logs if you can do it yourself.
- Their only regular jobs: press **Play** in Unity (or put on the headset) and say how it feels. Ask for that in one short, plain sentence.
- Explain results in plain language, short. No jargon. When something needs a decision, give a recommendation, not a menu.
- Keep them in the loop on what changed in-game, not on code details.

## The two versions

1. **Web version (live, playable, multiplayer):** `index.html` + `photon-realtime.js` at the repo root, served by GitHub Pages at https://oliverbell369.github.io/ape-rumble/ (cache-bust with `?v=N`). Three.js r160 (from jsDelivr) + WebXR. Online play uses Photon Realtime (JS SDK 4.4.0, vendored browser build), App ID `93cabdc9-913e-40c3-90df-37cf906b11b4`, region `eu`, app version `wrecked-0.3`. Every match is online; bots fill empty seats (6 seats). The first player in a room is host (runs bots + round clock); each player simulates their own ape and sends pose events 10x/s; hits/grabs/releases on someone else's ape are sent to the owner. This is the reference implementation for gameplay feel.
2. **Unity version (in progress, the future store version):** `WRECKED-Unity/` — Unity 6, URP, OpenXR (Meta Quest feature + Oculus Touch profile), no external assets: everything (map, textures, apes, sounds) is generated in code by one bootstrap object. Phase 1 = single-player port of the web game (written, never yet compiled in real Unity). Phase 2 = multiplayer with **Photon Fusion 2** (Shared mode; needs a new Fusion App ID from the creator's Photon dashboard and the Fusion SDK import).
   - Menu **WRECKED → Set Up Project** configures URP, Quest player settings, OpenXR and the main scene.
   - The creator's Unity is **6000.6.9f1** (Unity 6.6, Android module installed) at `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe`; keep whatever version Unity writes in `ProjectSettings/ProjectVersion.txt`.
   - Open the project from this repo (`ape-rumble\WRECKED-Unity`), not from a downloaded zip copy, so changes can be committed.
   - Desktop test mode (WASD/mouse, click slap, hold right-click grab) runs automatically in Play mode without a headset.

## Checking Unity yourself

- While the Editor is open, Unity 6.6 writes its log to the project: `WRECKED-Unity\Logs\Editor.log` (`%LOCALAPPDATA%\Unity\Editor\Editor.log` only has the first few startup lines). Read it for compile errors and Debug.Log output instead of asking the creator to copy errors.
- To open Unity straight into Play mode yourself: `Unity.exe -projectPath WRECKED-Unity -executeMethod Wrecked.EditorTools.WreckedSetup.PlayMain` (also on the menu as WRECKED → Play Main Scene). Without a headset, the `XR_ERROR_RUNTIME_UNAVAILABLE` lines in the log are expected; the game falls back to desktop mode.
- Headless setup also works: add `-batchmode -quit -executeMethod Wrecked.EditorTools.WreckedSetup.SetUp -logFile <file>` (Editor must not have this project open).
- After editing scripts, Unity recompiles when its window regains focus; if needed, ask the creator to click into Unity (one sentence), then read the log.
- With the Editor closed you can compile-check headless: `"<Unity.exe path>" -batchmode -nographics -quit -projectPath WRECKED-Unity -logFile -` (find Unity.exe under `C:\Program Files\Unity\Hub\Editor\<version>\Editor\`).
- Building to the Quest (needs developer mode + USB): `File → Build Profiles → Android → Build And Run`; can also be scripted with `-executeMethod` later.

## Game design (current)

- **Core:** Gorilla-Tag-style arm locomotion (push the world, fling with a 0.1 s velocity history, cap 7.5 m/s), hold grip to hang (grip resists light hits < 5.5 m/s), grab and ride the swinging log and ropes.
- **Fighting (Gang Beasts-style):** slap with hand speed (min 2.2 m/s), dizzy builds 3.2 per m/s of slap speed, dizzy at 100 → 3.5 s knocked out (shake arms to wake early). Grab with grip; throws are 0.9x hand speed (cap 7, awake targets 0.6x). Grabbed apes escape by slapping the holder: 3 slaps (hard ones count double) or knocking them dizzy. Bots slap back when grabbed.
- **Rounds:** free-for-all, 6 apes, last ape standing; 120 s; fall below y = -8 → out until next round (watch from the out ledge).
- **Map:** Ravine Sawmill — wooden deck over a ravine, swinging log, shed deck, log bridge to a crystal rock island, lookout. Pixel-texture look (low-res wood/rock/moss, glowing crystals) per the creator's reference images; original chimp-like apes, not Gorilla Tag's art.
- **No main menu:** the page loads straight into the Yard and joins Quick Play online. Browsers only start VR from a button press, so one Enter VR button sits over the game (on a computer: click to play). Name, room code and settings live on the Yard computer.
- **Yard computer** (lobby, west desk): green-screen terminal with a full keyboard you tap with your hands (PC: look at a key and click). Tabs: ROOM (join by code / Quick Play), NAME, COLOR (fur colour, R/G/B 0-9, synced to others with the LOOK event), CODE (promo codes unlock fur skins: WRECKED, BANANA, GHOST, GOLDAPE; add new ones in `CODES`), OPTIONS (blood, shadows).
- **Blood:** liquid look: glossy stretched droplets, wet shiny pools/splatters, drips running down walls, wrecked bodies bleed into a growing pool. Blood can be switched off.
- **Graphics:** real-time sun shadows (a depth map around the player; can be switched off on the computer if a headset struggles), warm sun / cool shade lighting. Fonts: Rubik Distressed (titles), Rubik Wet Paint (logo), Barlow Condensed (text), VT323 (computer).
- The creator can make 3D models; load them as .glb (three.js GLTFLoader) when they send some.
- Name: **WRECKED** (repo is still called `ape-rumble`).

## Roadmap (creator's priorities)

1. Get the Unity version compiling and running (Play mode, then on Quest). Fix errors from the Editor log.
2. Multiplayer in Unity (Photon Fusion 2).
3. More maps: Construction Site (cranes, wrecking ball, swinging platforms), Frozen Falls, floating islands, factory, sky scramble.
4. Progression: WRECK coins (earned) and gems (rarer), shop with outfits/skins (500 / 1,500 / 4,000 coins), hand cosmetics, emotes, trails. **No pay-to-win**: abilities (dash, shockwave, grapple, shield) should be earned or freely chosen, not bought.
5. Game modes later. Meta Store submission at the end (needs Meta developer org; real-money gems need Meta IAP and age rules).

## Repo conventions

- Push to `main` (GitHub Pages serves the root). Commit messages explain the player-facing change.
- Keep the web version working when changing shared files.
