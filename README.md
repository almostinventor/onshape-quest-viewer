# Onshape Quest Viewer

View an Onshape Part Studio at 1:1 scale in mixed reality on a Meta Quest 3 and handle it with your hands.
A Chrome extension sends the open Part Studio to the viewer app; the part appears in front of you over passthrough.
Edit the part in Onshape, send it again, and the new version replaces the old one.

> **AI disclaimer.** All code in this repository (the Unity viewer, its shaders, the Chrome extension) was written
> by an AI coding assistant (Claude, by Anthropic, used through Claude Code). almostinventor
> directed the project and tried it on a Meta Quest 3. Expect bugs, and use it at your own risk; see the
> [license](LICENSE).

## Status
Early and experimental. Everything has been developed and tried on **one Meta Quest 3** with **Google Chrome** on
Windows. Nothing has been tried on other headsets, browsers or operating systems.

**Confirmed working on the Quest 3** (standalone app):
- Sending a Part Studio from Chrome without API keys, using the browser's Onshape login
- Passthrough, with your real hands visible in front of the model
- Pinch to grab, two-hand scaling with the lock at 100%
- Pushing the model with your fingers
- The keypad, exploded view and rotate mode (earlier versions of these)

## What you need
- Meta Quest 3 with hand tracking turned on, and [developer mode](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/)
  enabled to install the app.
- Google Chrome and an Onshape account.
- To build the viewer: Unity **6000.6.5f1** with **Android Build Support** (used for development; other versions are
  untested).
- For the PC version: the Meta Quest Link app with Meta set as the active OpenXR runtime, and passthrough over Link
  enabled in the Link app's beta settings. Without that, the viewer logs
  `Failed to initialize Insight Passthrough` and the background is black.

## Setup

### 1. Viewer
1. Open the `Viewer` folder in Unity Hub.
2. Run **QuestViewer → Setup Project and Scene**.
3. Build:
   - **Quest:** **QuestViewer → Build Quest (APK)**, then install `Viewer/Builds/Android/OnshapeQuestViewer.apk`, for
     example with `adb install`. On the Quest it appears under *Unknown Sources* in the library.
   - **PC:** press Play in the editor, or **QuestViewer → Build PC (Link)** (untested).

### 2. Chrome extension
1. Open `chrome://extensions`, enable **Developer mode**, click **Load unpacked** and pick the `extension` folder.
2. Start the viewer. Its keypad display shows an **address** and a 6-character **pairing code**. Enter both in the
   extension's **Options** (right-click the icon → Options), click **Save**, then **Test connection**.
   For the Quest app, the PC and the Quest must be on the same network. For the PC version the address is
   `127.0.0.1:8723`.

## Use
In an Onshape Part Studio, click the **Send to Quest** toolbar icon or press **Alt+Q**. The extension skips the
download when the document's current microversion, the configuration and the set of visible parts are the same as
in the last send. Any edit in the document changes its microversion, so it then downloads again.

### Hands
| Gesture | Action |
|---|---|
| Pinch the model | Grab and move it |
| Close your hand around it | Whole-hand grab |
| Grab with both hands | Scale and rotate it; the scale locks to 100% within ±5% |
| Pinch in empty air with both hands, spread / close | Scale it in place; in MEASURE mode a tape measure instead |
| Push it with open fingers | Nudge it; it floats without gravity and slows down quickly |
| Clap | Exploded view on/off |
| Pinch a part in exploded view | Pull that part out; it moves back when you let go |
| Point your index finger up and draw a circle | Rotate mode on, in the direction you circled; again to turn it off |
| Double-pinch the model | Reset to 100% |
| Double-pinch in empty air | Bring the model in front of you |

Letting go of the model up to 12 cm above a horizontal surface known to the Quest (from its room setup) lowers it
onto that surface.

### Controllers
| Button | Action |
|---|---|
| Grip | Grab (both grips: scale and rotate) |
| A / X | Bring the model in front of you |
| B / Y | Exploded view on/off |
| Thumbstick click | Hologram mode on/off |

The tip of each controller can also push the model and press keypad buttons.

### Keypad
A panel with a small display starts low on your left; pinch it to move it. Press its buttons with a fingertip or a
controller tip. The display shows the current scale (green at 100%), short status messages, and the address and
pairing code.

| | | |
|---|---|---|
| **HOLO** hologram look | **STYLE** solid / clay / X-ray | **LIGHT** shaded / flat |
| **EXPLODE** exploded view | **ROTATE** turntable | **CUT** section cut (pinch the frame to move it) |
| **MEASURE** two-hand tape measure | **INSPECT** touch a part to see its name and size, plus mass if a material is assigned in Onshape (otherwise volume); touch it twice to isolate it | **RESET** back in front of you at 100% |

The exploded view is calculated by the viewer (each part moves outward from the model's centre); Onshape's own
exploded views exist only for Assemblies, which this project doesn't support.

When a new version arrives it first appears as a hologram, and the previous version fades out over a few seconds.
On the standalone Quest, real objects in front of the model are meant to hide it (Meta Depth API); the app asks for
the spatial data permission for this and for finding surfaces.

PC keyboard: **R** brings the model in front of you, **[** / **]** make the hand mask 1 mm thinner or wider.

## Known limitations
- Only Part Studios; Assemblies are not supported.
- Over Quest Link, a thin dark outline was visible around the model.
- Models with more than 96 parts get a single box-shaped collider instead of one per part.
- The model is sent from the PC to the Quest over your local network without encryption.

## Security
- The viewer rejects every request that doesn't carry its pairing code (HTTP 401).
- It sends no CORS headers.
- It limits requests to 200 MB, times out stalled connections after 20 seconds, and accepts at most 8 connections
  at a time.
- The PC version listens only on `127.0.0.1`; the Quest version listens on your local network.
- The extension reads from Onshape only through read (GET) requests made with your existing browser session, and
  sends the model only to the viewer address you entered. It stores the viewer address and pairing code in
  `chrome.storage.sync`, which Chrome syncs across your devices if Chrome sync is on. It does not use or store
  Onshape API keys.

## License
[MIT](LICENSE). The Meta XR Core SDK, glTFast and other Unity packages are downloaded by Unity's package manager and
come with their own licenses.
