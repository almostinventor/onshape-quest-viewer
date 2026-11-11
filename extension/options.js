const DEFAULT_VIEWER = "127.0.0.1:8723";
const input = document.getElementById("viewer");
const status = document.getElementById("status");

function normalize(v) {
  return (v || "").trim().replace(/^https?:\/\//, "").replace(/\/+$/, "") || DEFAULT_VIEWER;
}

chrome.storage.sync.get({ viewer: DEFAULT_VIEWER }).then(({ viewer }) => (input.value = viewer));

document.getElementById("save").addEventListener("click", async () => {
  const viewer = normalize(input.value);
  input.value = viewer;
  const origin = `http://${viewer.split(":")[0]}/*`;
  // Addresses other than 127.0.0.1 (e.g. the Quest on your Wi-Fi) need permission once.
  if (!viewer.startsWith("127.0.0.1")) {
    const granted = await chrome.permissions.request({ origins: [origin] });
    if (!granted) { status.textContent = "Not saved: Chrome needs permission to reach this address. Click Save again and choose Allow."; return; }
  }
  await chrome.storage.sync.set({ viewer });
  status.textContent = `Saved: ${viewer}. Click "Test connection" to check.`;
});

document.getElementById("test").addEventListener("click", async () => {
  const viewer = normalize(input.value);
  status.textContent = "Testing…";
  try {
    const r = await fetch(`http://${viewer}/status`, { signal: AbortSignal.timeout(4000) });
    const s = await r.json();
    status.textContent = s.ok ? `Connected${s.model ? ", showing " + s.model : ""}.` : "Viewer answered with an error.";
  } catch {
    status.textContent = "Not reachable. Is the viewer running, and did you Save this address?";
  }
});
