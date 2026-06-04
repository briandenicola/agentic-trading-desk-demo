/*
 * Fetch-wiring example — Scene 3 (Breaking news -> Exposure).
 *
 * index.html ships with hardcoded tables so it's a self-contained storyboard.
 * This file shows the pattern to make a scene DATA-DRIVEN off the agent API.
 * Copy it for the other scenes (Copilot can do this from the pattern here).
 *
 * To use: add  <script src="/app.js" defer></script>  before </body> in
 * index.html, then give the Scene 3 "Run exposure agent" button id="s3-run"
 * and add empty containers  #s3-impacted  #s3-neighbor  #s3-draft-body.
 */

const API = ""; // same origin when served by FastAPI

async function runAgent(scene, payload) {
  const res = await fetch(`${API}/api/agent/${scene}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ payload }),
  });
  if (!res.ok) throw new Error(`agent ${scene} failed: ${res.status}`);
  return res.json();
}

function rows(items, cols) {
  return items
    .map((it) => `<tr>${cols.map((c) => `<td>${it[c] ?? ""}</td>`).join("")}</tr>`)
    .join("");
}

async function loadExposure() {
  const data = await runAgent("exposure", { event: "IL_GO_downgrade" });
  // data.mode is "demo" or "live" — identical shape either way.

  const impacted = document.querySelector("#s3-impacted");
  if (impacted) impacted.innerHTML = rows(data.impacted, ["client", "position", "side"]);

  const neighbor = document.querySelector("#s3-neighbor");
  if (neighbor) neighbor.innerHTML = rows(data.nearest_neighbor, ["client", "exposure", "side"]);

  const draft = document.querySelector("#s3-draft-body");
  if (draft) draft.textContent = data.draft.body;
}

document.addEventListener("DOMContentLoaded", () => {
  const btn = document.querySelector("#s3-run");
  if (btn) btn.addEventListener("click", () => loadExposure().catch(console.error));
});
