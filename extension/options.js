const DEFAULT_VIEWER = "127.0.0.1:8723";
const viewerInput = document.getElementById("viewer");
const codeInput = document.getElementById("pairCode");
const status = document.getElementById("status");

function normalizeViewer(v) {
  return (v || "").trim().replace(/^https?:\/\//, "").replace(/\/+$/, "") || DEFAULT_VIEWER;
}

function normalizeCode(v) {
  return (v || "").trim().toUpperCase().replace(/[^A-Z0-9]/g, "");
}

chrome.storage.sync.get({ viewer: DEFAULT_VIEWER, pairCode: "" }).then(({ viewer, pairCode }) => {
  viewerInput.value = viewer;
  codeInput.value = pairCode;
});

document.getElementById("save").addEventListener("click", async () => {
  const viewer = normalizeViewer(viewerInput.value);
  const pairCode = normalizeCode(codeInput.value);
  viewerInput.value = viewer;
  codeInput.value = pairCode;
  // Addresses other than 127.0.0.1 (e.g. the Quest on your Wi-Fi) need permission once.
  if (!viewer.startsWith("127.0.0.1")) {
    const granted = await chrome.permissions.request({ origins: [`http://${viewer.split(":")[0]}/*`] });
    if (!granted) { status.textContent = "Not saved: Chrome needs permission to reach this address. Click Save again and choose Allow."; return; }
  }
  await chrome.storage.sync.set({ viewer, pairCode });
  status.textContent = `Saved. Click "Test connection" to check.`;
});

document.getElementById("test").addEventListener("click", async () => {
  const viewer = normalizeViewer(viewerInput.value);
  const pairCode = normalizeCode(codeInput.value);
  status.textContent = "Testing…";
  try {
    const r = await fetch(`http://${viewer}/status`, { headers: { "X-Pair-Code": pairCode }, signal: AbortSignal.timeout(4000) });
    if (r.status === 401) { status.textContent = "Reached the viewer, but the pairing code is wrong."; return; }
    const s = await r.json();
    status.textContent = s.ok ? `Connected${s.model ? ", showing " + s.model : ""}.` : "The viewer answered with an error.";
  } catch {
    status.textContent = "Not reachable. Is the viewer running, and did you Save this address?";
  }
});
