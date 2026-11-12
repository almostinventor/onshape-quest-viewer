# Onshape Quest Viewer

See your Onshape Part Studio at true 1:1 scale in mixed reality on a Meta Quest 3, and hold it in your hands.
Click **Send to Quest** in Onshape; the part appears in front of you over passthrough. Edit, send again, and the new
version swaps in.

- Runs **standalone on the Quest**, or on a PC streaming to the Quest over Meta Quest Link.
- No Onshape API keys: the Chrome extension uses your existing Onshape login.
- Only parts that are visible in the Part Studio are sent.

## What you need
- Meta Quest 3 or 3S with hand tracking on, and [developer mode](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/) enabled to install the app.
- Chrome (or another Chromium browser) with Onshape.
- Unity **6000.6** with **Android Build Support** (for the Quest) to build the viewer.
- For the PC version: Meta Quest Link with Meta set as the OpenXR runtime, and *Settings → Beta → Developer Runtime
  Features* and *Passthrough over Meta Quest Link* turned on.

## Setup

### 1. Viewer
1. Open the `Viewer` folder in Unity Hub.
2. Run **QuestViewer → Setup Project and Scene**.
3. Build:
   - **Quest:** **QuestViewer → Build Quest (APK)**, then install `Viewer/Builds/Android/OnshapeQuestViewer.apk`
     with Meta Quest Developer Hub or `adb install`. It appears under *Unknown Sources* in the Quest library.
   - **PC:** press Play in the editor, or **QuestViewer → Build PC (Link)**.

### 2. Chrome extension
1. Open `chrome://extensions`, enable **Developer mode**, click **Load unpacked** and pick the `extension` folder.
2. Start the viewer. Its keypad display shows an **address** and a 6-character **pairing code**. Enter both in the
   extension's **Options** (right-click the icon → Options) and click **Save**, then **Test connection**.
   For the Quest app the PC and Quest must be on the same network; for the PC version the address is
   `127.0.0.1:8723`.

### Security notes
- The viewer only accepts requests carrying its pairing code, and sends no CORS headers, so websites can't use it.
- The extension reads Onshape with your existing browser login, read-only, and only sends the model to the
  viewer address you set. No API keys are stored anywhere.
- Models travel from the PC to the Quest unencrypted over your local network; use it on a network you trust.

## Use
In an Onshape Part Studio, click the **Send to Quest** toolbar icon or press **Alt+Q**. If nothing changed since
the last send, the model isn't downloaded again.

### Hands
| Gesture | Action |
|---|---|
| Pinch the model | Grab and move it precisely |
| Close your hand around it | Whole-hand grab |
| Grab with both hands | Scale and rotate it (locks to 100%) |
| Pinch in empty air with both hands, spread / close | Scale it in place (locks to 100%); tape measure in MEASURE mode |
| Push it with open fingers | Nudge it; it floats and stops quickly |
| Clap | Exploded view on/off |
| Pinch a part in exploded view | Pull it out; it glides back when you let go |
| Point your index finger up and draw a circle | Rotate mode on (spins the way you swirled); again to stop |
| Double-pinch the model | Reset to 100% |
| Double-pinch in empty air | Bring the model in front of you |

Let go of the model just above a real table and it settles onto it (standalone Quest with room setup done).

### Controllers
| Button | Action |
|---|---|
| Grip | Grab (both grips: scale and rotate) |
| A / X | Bring the model in front of you |
| B / Y | Exploded view on/off |
| Thumbstick click | Hologram mode on/off |

The controller tip pushes things and presses buttons too.

### Keypad
A panel with a small display starts low on your left; pinch it to put it anywhere. Press its buttons with a fingertip
or controller. The display shows the scale (green at true size) and short status messages.

| | | |
|---|---|---|
| **HOLO** hologram look | **STYLE** solid / clay / X-ray | **LIGHT** shaded / flat |
| **EXPLODE** exploded view | **ROTATE** turntable | **CUT** section cut (pinch the frame to move it) |
| **MEASURE** two-hand tape measure | **INSPECT** touch a part for its name, size and mass; touch twice to isolate it | **RESET** back in front of you at 100% |

When a new version arrives it materialises as a hologram, and the previous version fades out as a ghost.
Your real hands stay visible in front of the model, and on the standalone Quest real objects in front of the model
hide it too (Meta Depth API; allow the spatial data permission when asked).

PC keyboard: **R** brings the model in front of you, **[** / **]** adjust the hand mask by 1 mm.

## License
[MIT](LICENSE). The Meta XR Core SDK and other Unity packages are downloaded by Unity under their own licenses.
