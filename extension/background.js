// Coordinates a send: the Onshape tab prepares/exports the model, this worker hands it to the viewer.
const DEFAULT_VIEWER = "127.0.0.1:8723";

async function viewerSettings() {
  const { viewer, pairCode } = await chrome.storage.sync.get({ viewer: DEFAULT_VIEWER, pairCode: "" });
  return {
    base: "http://" + viewer.trim().replace(/^https?:\/\//, "").replace(/\/+$/, ""),
    pairCode: (pairCode || "").trim().toUpperCase(),
  };
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
  const { base, pairCode } = await viewerSettings();
  const auth = { "X-Pair-Code": pairCode };
  let viewerReached = false;

  const notify = async (state, message) => {
    try {
      await fetch(base + "/notify", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...auth },
        body: JSON.stringify({ state, message }),
        signal: AbortSignal.timeout(4000),
      });
    } catch { /* viewer gone; the main error is reported elsewhere */ }
  };

  try {
    await badge(tabId, "…", "#546e7a", "Sending to Quest…");
    await ensureContentScript(tabId);

    let r;
    try {
      r = await fetch(base + "/status", { headers: auth, signal: AbortSignal.timeout(4000) });
    } catch {
      chrome.runtime.openOptionsPage();
      throw new Error(`Viewer not reachable at ${base.replace("http://", "")}. Enter the address shown on the keypad in the options page.`);
    }
    if (r.status === 401) {
      chrome.runtime.openOptionsPage();
      throw new Error("Wrong pairing code. Enter the code shown on the viewer's keypad in the options page.");
    }
    const status = await r.json();
    viewerReached = true;

    const info = await toTab(tabId, { type: "prepare" });
    if (status.key && status.key === info.key) {
      await notify("unchanged", info.name);
      await badge(tabId, "✓", "#2e7d32", `${info.name}: already up to date`);
      return;
    }

    await notify("downloading", info.name);
    const exp = await toTab(tabId, { type: "export", visible: info.visible, hidden: info.hidden });

    const res = await fetch(base + "/model", {
      method: "POST",
      headers: {
        "Content-Type": "model/gltf-binary",
        "X-Model-Name": encodeURIComponent(info.name),
        "X-Model-Key": encodeURIComponent(info.key),
        "X-Hidden-Parts": String(info.hidden),
        "X-Part-Info": exp.parts || "",
        ...auth,
      },
      body: base64ToBytes(exp.glb),
      signal: AbortSignal.timeout(120000),
    });
    const data = await res.json().catch(() => ({ ok: false, message: `HTTP ${res.status}` }));
    if (!res.ok || !data.ok) throw new Error(data.message || `HTTP ${res.status}`);

    await badge(tabId, "✓", "#2e7d32", `Sent: ${data.message}`);
  } catch (e) {
    const message = e.message || String(e);
    if (viewerReached) await notify("error", message);
    await badge(tabId, "✗", "#c62828", `Failed: ${message}`);
  } finally {
    busy = false;
    setTimeout(() => chrome.action.setBadgeText({ tabId, text: "" }).catch(() => {}), 8000);
  }
}

chrome.action.onClicked.addListener((tab) => send(tab));

// Voice commands: while an Onshape tab is open, it pings this worker (keeping it alive) and the worker waits
// on the viewer's GET /commands. "send" sends the Onshape document tab you used last.
const isDocument = (url) => /^https:\/\/[^/]*onshape\.com\/documents\//.test(url || "");
let polling = false;

async function voiceTarget() {
  const { lastTab } = await chrome.storage.session.get("lastTab");
  if (lastTab != null) {
    try {
      const t = await chrome.tabs.get(lastTab);
      if (isDocument(t.url)) return t;
    } catch { /* closed */ }
  }
  const tabs = await chrome.tabs.query({ url: "https://*.onshape.com/documents/*" });
  return tabs.find((t) => t.active) || tabs[0];
}

async function pollCommands() {
  if (polling) return;
  polling = true;
  try {
    while (true) {
      const { base, pairCode } = await viewerSettings();
      if (!pairCode) return;
      const started = Date.now();
      let r;
      try {
        r = await fetch(base + "/commands", { headers: { "X-Pair-Code": pairCode }, signal: AbortSignal.timeout(30000) });
      } catch {
        return; // viewer not running; the next ping from an Onshape tab tries again
      }
      if (!r.ok) return;
      const { commands } = await r.json().catch(() => ({}));
      if (Array.isArray(commands) && commands.includes("send")) {
        const tab = await voiceTarget();
        if (tab) await send(tab);
      }
      // The viewer holds each request up to 20 s; never poll faster than once a second, whatever answers.
      const wait = 1000 - (Date.now() - started);
      if (wait > 0) await new Promise((resolve) => setTimeout(resolve, wait));
    }
  } finally {
    polling = false;
  }
}

chrome.runtime.onMessage.addListener((msg, sender) => {
  if (!sender.tab || !msg || (msg.type !== "alive" && msg.type !== "focused")) return;
  if (msg.type === "focused") chrome.storage.session.set({ lastTab: sender.tab.id });
  pollCommands();
});
