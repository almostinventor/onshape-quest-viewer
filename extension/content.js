// Runs inside Onshape tabs. Talks to Onshape's REST API with your existing browser login (same origin, no API keys).
(() => {
  if (window.__sendToQuest) return;
  window.__sendToQuest = true;

  const API = location.origin + "/api/v17";

  function parseLocation() {
    const m = location.pathname.match(/\/documents\/([0-9a-f]{24})\/([wvm])\/([0-9a-f]{24})\/e\/([0-9a-f]{24})/i);
    if (!m) return null;
    return {
      did: m[1], wvm: m[2].toLowerCase(), wvmid: m[3], eid: m[4],
      configuration: new URLSearchParams(location.search).get("configuration"),
    };
  }

  async function api(path, accept = "application/json") {
    const res = await fetch(API + path, { credentials: "include", headers: { Accept: accept } });
    if (!res.ok) {
      const hint = res.status === 401 ? " (sign in to Onshape)"
        : res.status === 403 ? " (no access to this document)"
        : res.status === 404 ? " (not found)" : "";
      const err = new Error(`Onshape returned ${res.status}${hint}`);
      err.status = res.status;
      throw err;
    }
    return res;
  }

  // Name, a fingerprint of what would be exported (to skip unchanged models) and the visible part IDs.
  async function prepare() {
    const u = parseLocation();
    if (!u) throw new Error("Open a Part Studio tab in an Onshape document.");
    const doc = `d/${u.did}/${u.wvm}/${u.wvmid}`;
    const cfg = u.configuration ? `configuration=${encodeURIComponent(u.configuration)}` : "";

    const [elements, parts, mv] = await Promise.all([
      api(`/documents/${doc}/elements?elementId=${u.eid}`).then((r) => r.json()),
      api(`/parts/${doc}/e/${u.eid}${cfg ? "?" + cfg : ""}`).then((r) => r.json()),
      u.wvm === "m" ? Promise.resolve(u.wvmid)
        : api(`/documents/${doc}/currentmicroversion`).then((r) => r.json()).then((j) => j.microversion),
    ]);

    const element = elements[0] || {};
    if (element.elementType && element.elementType !== "PARTSTUDIO") {
      throw new Error("This tab isn't a Part Studio.");
    }
    const visible = parts.filter((p) => !p.isHidden).map((p) => p.partId).sort();
    if (parts.length > 0 && visible.length === 0) throw new Error("All parts are hidden.");

    return {
      name: element.name || "Onshape part",
      key: [u.did, u.eid, mv, u.configuration || "", visible.join(",")].join("|"),
      visible,
      hidden: parts.length - visible.length,
    };
  }

  // Binary glTF of the visible parts, base64-encoded for extension messaging.
  async function exportGlb(visible, hidden) {
    const u = parseLocation();
    const base = `/partstudios/d/${u.did}/${u.wvm}/${u.wvmid}/e/${u.eid}/gltf?outputFaceAppearances=true`;
    let query = u.configuration ? `&configuration=${encodeURIComponent(u.configuration)}` : "";
    if (hidden > 0) query += visible.map((id) => `&partId=${encodeURIComponent(id)}`).join("");

    let res;
    try {
      res = await api(base + "&precomputedLevelOfDetail=medium" + query, "model/gltf-binary");
    } catch (e) {
      if (e.status !== 400) throw e;
      res = await api(base + query, "model/gltf-binary");
    }
    const blob = await res.blob();
    const head = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
    if (String.fromCharCode(...head) !== "glTF") throw new Error("Onshape didn't return a binary glTF.");

    const dataUrl = await new Promise((resolve, reject) => {
      const r = new FileReader();
      r.onload = () => resolve(r.result);
      r.onerror = () => reject(r.error);
      r.readAsDataURL(blob);
    });
    return dataUrl.slice(dataUrl.indexOf(",") + 1);
  }

  chrome.runtime.onMessage.addListener((msg, _sender, reply) => {
    const run = async () => {
      if (msg.type === "ping") return { ok: true };
      if (msg.type === "prepare") return { ok: true, ...(await prepare()) };
      if (msg.type === "export") return { ok: true, glb: await exportGlb(msg.visible, msg.hidden) };
      return { ok: false, error: "unknown message" };
    };
    run().then(reply, (e) => reply({ ok: false, error: e.message || String(e) }));
    return true; // async reply
  });
})();
