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
2. For the Quest app: start it, then enter the address it shows (for example `192.168.1.42:8723`) in the
   extension's **Options**. The PC and Quest must be on the same network. For the PC version, keep the default
   `127.0.0.1:8723`.

## Use
In an Onshape Part Studio, click the **Send to Quest** toolbar icon or press **Alt+Q**. If nothing changed since
the last send, the model isn't downloaded again.

### Hands
| Gesture | Action |
|---|---|
| Pinch the model | Grab and move it precisely |
| Close your hand around it | Whole-hand grab |
| Grab with both hands | Scale and rotate it (locks to 100%) |
| Push it with open fingers | It floats and drifts; let go mid-move to throw it |
| Double-pinch the model | Reset to 100% |
| Double-pinch in empty air | Bring the model in front of you |
| Pinch in empty air with both hands | Tape measure (mm) |

### Controllers
| Button | Action |
|---|---|
| Grip | Grab (both grips: scale and rotate) |
| A / X | Bring the model in front of you |
| B / Y | Cycle HUD mode |
| Thumbstick click | Hologram mode on/off |

The controller tip pushes things and presses buttons too.

### Keypad
A small panel starts low on your left; pinch it to put it anywhere. Press its buttons with a fingertip or controller:
- **HOLO**: hologram mode (translucent blue with scan lines). New models always materialise this way briefly.
- **HUD**: Visor (helmet frame with animations), Minimal (just the model info) or Off.
- **LIGHT**: Shaded or Flat (evenly lit, no shading).

The model's name, true size in mm and current scale are shown at the top of your view.
Your real hands stay visible in front of the model, and on the standalone Quest real objects in front of the model
hide it too (Meta Depth API; allow the spatial data permission when asked).

PC keyboard: **R** brings the model in front of you, **[** / **]** adjust the hand mask by 1 mm.

## License
[MIT](LICENSE). The Meta XR Core SDK and other Unity packages are downloaded by Unity under their own licenses.
