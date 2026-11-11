// Coordinates a send: the Onshape tab prepares/exports the model, this worker hands it to the viewer.
const DEFAULT_VIEWER = "127.0.0.1:8723";

async function viewerBase() {
  const { viewer } = await chrome.storage.sync.get({ viewer: DEFAULT_VIEWER });
  return "http://" + viewer.trim().replace(/^https?:\/\//, "").replace(/\/+$/, "");
}

async function badge(tabId, text, color, title) {
  await chrome.action.setBadgeText({ tabId, text });
  if (color) await chrome.action.setBadgeBackgroundColor({ tabId, color });
  if (title) await chrome.action.setTitle({ tabId, title });
}

async function toTab(tabId, msg) {
  const res = await chrome.tabs.sendMessage(tabId, msg);
  if (!res || !res.ok) throw new Error((res && res.error) || "No response from the Onshape tab.");
  return res;
}

async function ensureContentScript(tabId) {
  try {
    await chrome.tabs.sendMessage(tabId, { type: "ping" });
  } catch {
    await chrome.scripting.executeScript({ target: { tabId }, files: ["content.js"] });
  }
}

async function notify(base, state, message) {
  try {
    await fetch(base + "/notify", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ state, message }),
    });
  } catch { /* viewer gone; the main error is reported elsewhere */ }
}

function base64ToBytes(b64) {
  const bin = atob(b64);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes;
}

let busy = false;

async function send(tab) {
  if (!tab || !/^https:\/\/[^/]*onshape\.com\/documents\//.test(tab.url || "")) {
    await badge(tab && tab.id, "✗", "#c62828", "Open an Onshape document tab first.");
    return;
  }
  if (busy) return;
  busy = true;
  const tabId = tab.id;
  const base = await viewerBase();
  let viewerReached = false;

  try {
    await badge(tabId, "…", "#546e7a", "Sending to Quest…");
    await ensureContentScript(tabId);

    let status;
    try {
      const r = await fetch(base + "/status", { signal: AbortSignal.timeout(4000) });
      status = await r.json();
      viewerReached = true;
    } catch {
      chrome.runtime.openOptionsPage();
      throw new Error(`Viewer not reachable at ${base.replace("http://", "")}. Enter the address shown in the headset in the options page.`);
    }

    const info = await toTab(tabId, { type: "prepare" });
    if (status.key && status.key === info.key) {
      await notify(base, "unchanged", info.name);
      await badge(tabId, "✓", "#2e7d32", `${info.name}: already up to date`);
      return;
    }

    await notify(base, "downloading", info.name);
    const exp = await toTab(tabId, { type: "export", visible: info.visible, hidden: info.hidden });

    const res = await fetch(base + "/model", {
      method: "POST",
      headers: {
        "Content-Type": "model/gltf-binary",
        "X-Model-Name": encodeURIComponent(info.name),
        "X-Model-Key": encodeURIComponent(info.key),
        "X-Hidden-Parts": String(info.hidden),
      },
      body: base64ToBytes(exp.glb),
    });
    const data = await res.json().catch(() => ({ ok: false, message: `HTTP ${res.status}` }));
    if (!res.ok || !data.ok) throw new Error(data.message || `HTTP ${res.status}`);

    await badge(tabId, "✓", "#2e7d32", `Sent: ${data.message}`);
  } catch (e) {
    const message = e.message || String(e);
    if (viewerReached) await notify(base, "error", message);
    await badge(tabId, "✗", "#c62828", `Failed: ${message}`);
  } finally {
    busy = false;
    setTimeout(() => chrome.action.setBadgeText({ tabId, text: "" }).catch(() => {}), 8000);
  }
}

chrome.action.onClicked.addListener((tab) => send(tab));
