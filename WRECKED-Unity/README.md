# WRECKED — Unity version (Meta Quest)

This is the Unity 6 version of WRECKED, the VR ape brawler. Right now it is **single player against 5 bots**
(the same game as the web version). Online multiplayer comes next.

Everything (the map, apes, textures, sounds) is built from code when the game starts, so the project is small.

---

## 1. Install Unity (one time)

1. Download and install **Unity Hub** from <https://unity.com/download>, then sign in (the free **Personal** licence is fine).
2. In Unity Hub, open **Installs → Install Editor** and pick the newest **Unity 6 (6000.0) LTS**.
3. On the modules screen tick **Android Build Support**, and under it tick **OpenJDK** and **Android SDK & NDK Tools**.
4. Click **Install** and wait (it's a big download).

## 2. Get the project

- **Easiest:** go to <https://github.com/oliverbell369/ape-rumble>, click the green **Code** button → **Download ZIP**, and unzip it.
- **Better for updates:** install **GitHub Desktop**, then **File → Clone repository → oliverbell369/ape-rumble**.
  When there's an update, click **Fetch origin** then **Pull origin**.

The Unity project is the **`WRECKED-Unity`** folder inside it.

## 3. Open it in Unity

1. In Unity Hub go to **Projects → Add → Add project from disk** and choose the **`WRECKED-Unity`** folder.
2. If Hub says the editor version isn't installed, choose your installed **Unity 6** version.
3. The first open takes a few minutes while Unity downloads the packages.
4. If Unity asks **"enable the new Input System backends?"**, click **Yes** (Unity restarts).

## 4. Set up the project (one click)

In the top menu click **WRECKED → Set Up Project**. It:

- makes the main scene and adds it to the build,
- sets up the Universal Render Pipeline tuned for Quest,
- sets the Android/Quest player settings,
- turns on **OpenXR** with **Meta Quest** support,
- switches the build target to Android.

Open the **Console** (**Window → General → Console**) and check for the line **`[WRECKED setup] Done`**.
Yellow warnings tell you if one step needs doing by hand.

## 5. Test on your computer

Press the **▶ Play** button at the top. Click inside the Game view to capture the mouse, then:

- **WASD** move · **Space** hop · **mouse** look
- **Left click** slap · **hold right click** to grab, let go to throw
- **Esc** frees the mouse

## 6. Play on your Quest

**Developer mode (one time):**
1. Create a free developer account / organisation at <https://developers.meta.com/horizon/> (you may need to verify the account).
2. In the **Meta Horizon** phone app: **Menu → Devices → your headset → Headset settings → Developer mode → On**.

**Build and run:**
1. Plug the Quest into the laptop with a USB‑C cable. Put the headset on and accept **Allow USB debugging** (tick *Always allow*).
2. In Unity: **File → Build Profiles → Android**. Make sure Android is the active profile.
3. Under **Run Device**, pick your Quest (press **Refresh** if it isn't listed).
4. Click **Build And Run**, choose a folder (for example a new `Builds` folder) and a name like `WRECKED.apk`.
5. The first build takes 5–15 minutes. The game then starts in the headset.
   Later you can find it in the headset under **Library → Unknown Sources**.

## In VR

- Push floors, walls and logs with your hands to move. Fling your arms to jump.
- Swing your hand fast into an ape to slap it. Enough slaps make it dizzy.
- Hold **grip** near an ape to grab it; swing and let go to throw.
- Hold **grip** on a ledge or beam to hang on. Grab the swinging log to ride it.
- Grabbed? Keep slapping the ape holding you. Dizzy? Shake your arms hard.
- **Right stick** turns.

## If something goes wrong

1. Open **Window → General → Console**.
2. Click the red error, press **Ctrl+C** (or right-click → **Copy**), and paste it to Claude.
3. If there are lots of errors, send the **first** one — later ones are often caused by it.

## What's inside

| Folder | What it is |
|---|---|
| `Assets/WRECKED/Scripts` | The game (C#): `Game*.cs` (rounds, fighting, player, bots, visuals), `World.cs` (map + collision), `TextureGen.cs`, `MeshGen.cs`, `Sfx.cs` |
| `Assets/WRECKED/Resources/Shaders` | URP shaders for the level, scenery, apes, sky and UI |
| `Assets/WRECKED/Editor/WreckedSetup.cs` | The **WRECKED → Set Up Project** menu |
| `Packages/manifest.json` | Unity packages: URP 17.0.3, OpenXR 1.14.3, XR Plugin Management 4.5.0, Input System 1.11.2 |
