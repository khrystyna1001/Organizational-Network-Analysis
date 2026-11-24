import { ApiError, call, roleOf, session } from "./api.js";
import { BOTTLENECK, departmentColors, fitGraph, limitNodes, renderGraph, selectInGraph, suggestedLayout, zoomGraph } from "./graph.js";

const $ = (id) => document.getElementById(id);

/** Tiny DOM builder. Text is always set via text nodes, so API data can never inject markup. */
function h(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === "class") node.className = value;
    else if (key === "style") node.style.cssText = value;
    else if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else if (key === "value") node.value = value;
    else node.setAttribute(key, value === true ? "" : value);
  }
  for (const child of children.flat()) {
    if (child === undefined || child === null || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}
const initials = (name) => (name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]).join("") || "?").toUpperCase();
/** Betweenness is normalised by (n-1)(n-2), so on big graphs it is tiny: switch to scientific notation. */
const fmtB = (b) => (b === 0 ? "0" : b < 0.001 ? b.toExponential(1) : b.toFixed(4));
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function toast(message, isError = false) {
  const t = $("toast");
  t.textContent = message;
  t.className = "show" + (isError ? " error" : "");
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => (t.className = ""), 4200);
}

/** Runs an API action, reports the outcome, returns true on success. */
async function attempt(fn, success) {
  try {
    await fn();
    if (success) toast(success);
    return true;
  } catch (error) {
    toast(error instanceof ApiError ? error.message : "Something went wrong.", true);
    return false;
  }
}

// ------------------------------------------------------------------ state
const state = {
  boards: [], boardId: null, board: null,
  nodes: [], connections: [], analysis: null, recs: [],
  byId: new Map(), metrics: new Map(), reportsOf: new Map(), bossOf: new Map(), ranked: [],
  selected: null, layout: "hierarchy", labels: "smart",
  search: "", sort: { key: "betweenness", dir: -1 }, watch: 0,
};

// ------------------------------------------------------------------ auth screens
function showAuth() {
  $("shell").hidden = true;
  $("auth").hidden = false;
}

async function showApp() {
  const s = session.read();
  $("auth").hidden = true;
  $("shell").hidden = false;
  $("user-avatar").textContent = initials(s.user.display_name);
  $("user-name").textContent = s.user.display_name;
  $("user-role").textContent = s.role;
  document.querySelector("#profile-form [name=display_name]").value = s.user.display_name;
  $("admin-box").hidden = s.role !== "Admin";
  if (s.role === "Admin") loadUsers();
  await loadBoards();
}

function authError(message) {
  const box = $("auth-error");
  box.textContent = message || "";
  box.hidden = !message;
}

async function signIn(email, password) {
  const result = await call("POST", "/api/v1/users/login", { json: { email, password } });
  session.write({ token: result.access_token, user: result.user, role: roleOf(result.access_token) });
  authError("");
  await showApp();
}

for (const tab of document.querySelectorAll("#auth-tabs .tab")) {
  tab.addEventListener("click", () => {
    for (const t of document.querySelectorAll("#auth-tabs .tab")) t.classList.toggle("active", t === tab);
    for (const t of document.querySelectorAll("#auth-tabs .tab")) t.setAttribute("aria-selected", String(t === tab));
    for (const pane of document.querySelectorAll(".pane")) pane.hidden = pane.dataset.pane !== tab.dataset.tab;
    authError("");
  });
}

const formData = (form) => Object.fromEntries(new FormData(form));

$("login-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const f = formData(e.target);
  try { await signIn(f.email, f.password); e.target.reset(); } catch (err) { authError(err.message); }
});
$("register-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const f = formData(e.target);
  try {
    await call("POST", "/api/v1/users", { json: f });
    await signIn(f.email, f.password);
    e.target.reset();
  } catch (err) { authError(err.message); }
});
$("reset-request-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  try {
    await call("POST", "/api/v1/users/password-reset/request", { json: formData(e.target) });
    authError("");
    toast("If that account exists, an email is on its way.");
  } catch (err) { authError(err.message); }
});
$("reset-confirm-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  try {
    await call("POST", "/api/v1/users/password-reset/confirm", { json: formData(e.target) });
    authError("");
    e.target.reset();
    toast("Password changed. You can sign in now.");
    document.querySelector('#auth-tabs [data-tab="login"]').click();
  } catch (err) { authError(err.message); }
});

$("signout").addEventListener("click", () => { session.write(null); showAuth(); });
window.addEventListener("ona:signout", showAuth);

// ------------------------------------------------------------------ sidebar forms
// ---- CSV import: choose a file -> preview -> confirm the column mapping -> import
const MAP_ROLES = [
  ["employee_id", "Person ID", "optional, unique"],
  ["full_name", "Full name", "one column"],
  ["first_name", "First name", ""],
  ["last_name", "Last name", ""],
  ["title", "Title", "optional"],
  ["department", "Department", "optional"],
  ["supervisor", "Boss", "optional, an ID or a full name"],
];
let pendingImport = null;   // { file, preview }

$("import-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const file = e.target.file.files[0];
  if (!file) return;
  const body = new FormData();
  body.append("file", file);
  try {
    const preview = await call("POST", "/api/v1/data/preview", { form: body });
    if (!preview.columns.length) { toast("That file is empty.", true); return; }
    openMapping(file, preview, e.target.board_name.value || file.name.replace(/\.csv$/i, ""));
  } catch (err) { toast(err.message, true); }
});

function openMapping(file, preview, boardName) {
  pendingImport = { file, preview };
  $("mapping-file").textContent = `${file.name} · ${preview.row_count} rows · ${preview.columns.length} columns`;
  document.querySelector("#mapping-form [name=board_name]").value = boardName;
  $("mapping-error").hidden = true;

  const selects = {};
  $("map-grid").replaceChildren(...MAP_ROLES.map(([role, label, hint]) => {
    const select = h("select", { name: role, "aria-label": label },
      h("option", { value: "" }, "— not mapped —"),
      preview.columns.map((c) => h("option", { value: c }, c)));
    select.value = preview.suggested_mapping[role] ?? "";
    selects[role] = select;
    return h("label", {}, label, hint ? h("small", {}, ` (${hint})`) : null, select);
  }));
  // Full name and first/last name exclude each other.
  const sync = () => {
    const full = selects.full_name.value !== "";
    for (const r of ["first_name", "last_name"]) { selects[r].disabled = full; if (full) selects[r].value = ""; }
  };
  selects.full_name.addEventListener("change", sync);
  for (const r of ["first_name", "last_name"]) {
    selects[r].addEventListener("change", () => { if (selects[r].value) { selects.full_name.value = ""; sync(); } });
  }
  sync();

  $("preview-table").replaceChildren(
    h("thead", {}, h("tr", {}, preview.columns.map((c) => h("th", {}, c)))),
    h("tbody", {}, preview.sample_rows.map((row) => h("tr", {}, row.map((cell) => h("td", { title: cell }, cell))))));
  $("mapping-dialog").showModal();
}

$("mapping-cancel").addEventListener("click", () => $("mapping-dialog").close());

$("mapping-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const { file } = pendingImport;
  const values = Object.fromEntries(MAP_ROLES.map(([role]) => [role, e.target.elements[role].value || null]));
  const fail = (message) => { $("mapping-error").textContent = message; $("mapping-error").hidden = false; };
  if (!values.full_name && !values.first_name && !values.last_name) return fail("Pick the column that holds the person's name.");

  const body = new FormData();
  body.append("file", file);
  body.append("board_name", e.target.elements.board_name.value);
  body.append("mapping", JSON.stringify(values));
  $("mapping-submit").disabled = true;
  try {
    const result = await call("POST", "/api/v1/data", { form: body });
    $("mapping-dialog").close();
    $("import-form").reset();
    $("import-box").open = false;
    toast(`Imported ${result.nodes_created} people, ${result.connections_created} reporting lines.`);
    await loadBoards(result.board_id);
  } catch (err) {
    fail(err.message);                       // the whole file was rejected: fix the mapping and try again
  } finally {
    $("mapping-submit").disabled = false;
  }
});

$("profile-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  await attempt(async () => {
    const user = await call("PUT", "/api/v1/users/me", { json: formData(e.target) });
    session.write({ ...session.read(), user });
    $("user-name").textContent = user.display_name;
    $("user-avatar").textContent = initials(user.display_name);
  }, "Profile saved.");
});

$("password-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const form = e.target;
  if (await attempt(() => call("POST", "/api/v1/users/me/password", { json: formData(form) }), "Password changed.")) form.reset();
});

async function loadUsers() {
  try {
    const users = await call("GET", "/api/v1/users");
    $("users-list").replaceChildren(...users.map((u) => h("div", {}, h("span", {}, u.email), h("span", { class: "muted" }, u.role || "User"))));
    $("role-user").replaceChildren(...users.map((u) => h("option", { value: u.id }, u.email)));
  } catch (err) { toast(err.message, true); }
}

$("role-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const f = formData(e.target);
  if (await attempt(() => call("PUT", `/api/v1/users/${f.user}/role`, { json: { role: f.role } }), "Role changed.")) loadUsers();
});

$("board-select").addEventListener("change", (e) => { state.boardId = e.target.value; state.selected = null; loadBoard(); });

// ------------------------------------------------------------------ data loading
async function loadBoards(selectId) {
  try {
    state.boards = await call("GET", "/api/v1/boards");
  } catch (err) { toast(err.message, true); return; }
  const select = $("board-select");
  select.replaceChildren(...state.boards.map((b) => h("option", { value: b.id }, `${b.name} · ${b.lifecycle}`)));
  const ids = state.boards.map((b) => b.id);
  state.boardId = [selectId, state.boardId].find((id) => id && ids.includes(id)) ?? ids[0] ?? null;
  $("empty").hidden = state.boards.length > 0;
  $("board-view").hidden = state.boards.length === 0;
  $("import-box").open = state.boards.length === 0;
  if (state.boardId) { select.value = state.boardId; state.selected = null; await loadBoard(); }
}

async function loadBoard({ quiet = false } = {}) {
  const board = state.boards.find((b) => b.id === state.boardId);
  if (!board) return;
  try {
    const published = board.current_graph_version_id !== null;
    const [nodes, connections, runs] = await Promise.all([
      call("GET", `/api/v1/nodes?board_id=${board.id}`),
      call("GET", `/api/v1/connections?board_id=${board.id}`),
      published ? call("GET", `/api/v1/analysis-runs?board_id=${board.id}`) : [],
    ]);
    const analysis = runs.reduce((best, r) => (!best || r.started_at > best.started_at ? r : best), null);
    const recs = analysis ? await call("GET", `/api/v1/recommendations?analysis_run_id=${analysis.id}`) : [];
    if (board.id !== state.boardId) return;           // user switched boards meanwhile
    if (state.layoutFor !== board.id) {          // first time this board is shown: pick a sensible layout
      state.layoutFor = board.id;
      state.layout = suggestedLayout(nodes, connections);
      for (const b of $("layout-seg").children) b.classList.toggle("on", b.dataset.v === state.layout);
    }
    Object.assign(state, { board, nodes, connections, analysis, recs });
  } catch (err) {
    if (!quiet) toast(err.message, true);
    return;
  }
  derive();
  renderAll();
}

/** Refresh the board list entry (state may have changed) then reload its data. */
async function refresh(options) {
  const id = state.boardId;
  try { state.boards = await call("GET", "/api/v1/boards"); } catch { return; }
  const select = $("board-select");
  select.replaceChildren(...state.boards.map((b) => h("option", { value: b.id }, `${b.name} · ${b.lifecycle}`)));
  if (state.boards.some((b) => b.id === id)) { select.value = id; await loadBoard(options); }
  else await loadBoards();
}

function derive() {
  state.byId = new Map(state.nodes.map((n) => [n.id, n]));
  state.metrics = new Map((state.analysis?.node_metrics ?? []).map((m) => [m.node_id, m]));
  state.reportsOf = new Map();
  state.bossOf = new Map();
  for (const c of state.connections) {
    state.reportsOf.set(c.source_node_id, [...(state.reportsOf.get(c.source_node_id) ?? []), c.target_node_id]);
    state.bossOf.set(c.target_node_id, c.source_node_id);
  }
  const b = (n) => state.metrics.get(n.id)?.betweenness ?? 0;
  state.ranked = [...state.nodes].sort((x, y) => b(y) - b(x) || x.name.localeCompare(y.name));
  if (!state.byId.has(state.selected)) state.selected = state.ranked[0]?.id ?? null;
}

/** After publishing, Kafka triggers analysis and insights asynchronously: poll briefly. */
async function watchForResults() {
  const ticket = ++state.watch;
  for (let i = 0; i < 20; i++) {
    await sleep(2000);
    if (ticket !== state.watch) return;
    await refresh({ quiet: true });
    if (state.analysis && (state.recs.length > 0 || state.analysis.bottlenecks.length === 0)) return;
  }
}

// ------------------------------------------------------------------ rendering
function renderAll() {
  renderHeader();
  renderKpis();
  renderGraphPanel();
  renderInspector();
  renderRanking();
  renderRecs();
  renderRoster();
}

function renderHeader() {
  const { board, nodes, connections, analysis } = state;
  const published = board.current_graph_version_id !== null;
  $("board-name").textContent = board.name;
  $("board-state").textContent = board.lifecycle;
  $("board-state").className = `kicker state-${board.lifecycle.toLowerCase()}`;
  const steps = ["Draft", "Built", "Published", "Archived"], at = steps.indexOf(board.lifecycle);
  $("stepper").replaceChildren(...steps.map((s, i) => h("li", { class: i < at ? "done" : i === at ? "current" : "", "aria-current": i === at ? "step" : false }, s)));
  $("board-meta").textContent = `${nodes.length} people · ${connections.length} reporting lines` +
    (published ? ` · version ${board.current_graph_version_id.slice(0, 8)}` : "");
  $("board-note").textContent = !published
    ? "Build and publish the board, then the analysis highlights communication bottlenecks."
    : !analysis ? "Published. The analysis starts automatically – it will appear in a moment, or run it yourself." : "";

  const act = (label, fn, ok, cls = "") =>
    h("button", { class: `btn ${cls}`, type: "button", onclick: async () => { if (await attempt(fn, ok)) await refresh(); } }, label);
  const path = `/api/v1/boards/${board.id}`;
  const buttons = [];
  if (board.lifecycle === "Draft") buttons.push(act("Build graph", () => call("POST", `${path}/build`), "Graph built.", "primary"));
  if (board.lifecycle === "Built") {
    buttons.push(h("button", { class: "btn primary", type: "button", onclick: async () => {
      if (await attempt(() => call("POST", `${path}/publish`), "Published – analysis starts automatically.")) { await refresh(); watchForResults(); }
    } }, "Publish"));
  }
  if (board.lifecycle === "Published") buttons.push(act("Archive", () => call("POST", `${path}/archive`), "Archived."));
  buttons.push(h("button", { class: `btn ${published && !analysis ? "primary" : ""}`, type: "button", disabled: !published, onclick: async () => {
    if (await attempt(() => call("POST", "/api/v1/analysis-runs", { json: { board_id: board.id } }), "Analysis complete.")) await refresh();
  } }, "Run analysis"));
  buttons.push(h("button", { class: "btn", type: "button", disabled: !analysis || analysis.status !== "Completed", onclick: async () => {
    if (await attempt(() => call("POST", "/api/v1/recommendations", { json: { analysis_run_id: analysis.id } }), "Insights generated.")) await refresh();
  } }, "Insights"));
  buttons.push(h("button", { class: "btn", type: "button", onclick: () => refresh() }, "↻ Refresh"));
  if (board.lifecycle !== "Published") {
    buttons.push(h("button", { class: "btn danger", type: "button", onclick: async () => {
      if (!confirm(`Delete board "${board.name}"?`)) return;
      if (await attempt(() => call("DELETE", path), "Board deleted.")) await loadBoards();
    } }, "Delete"));
  }
  $("actions").replaceChildren(...buttons);
}

function kpi(label, value, small, sub, dotColor, valueColor) {
  return h("div", { class: "kpi" },
    h("div", { class: "label" }, label),
    h("div", { class: "val", style: valueColor ? `color:${valueColor}` : "" }, value, h("small", {}, small)),
    h("div", { class: "sub" }, h("span", { class: "dot", style: `background:${dotColor}` }), sub));
}

function renderKpis() {
  const { nodes, connections, analysis, reportsOf, byId } = state;
  const departments = new Set(nodes.map((n) => n.department.trim())).size;
  const spans = [...reportsOf.values()].map((v) => v.length);
  const span = spans.length ? spans.reduce((a, b) => a + b, 0) / spans.length : 0;
  const possible = nodes.length * (nodes.length - 1);
  const density = possible ? connections.length / possible : 0;
  const hot = (analysis?.bottlenecks ?? []).map((id) => byId.get(id)).filter(Boolean);
  const names = hot.slice(0, 3).map((n) => n.name).join(", ") || (analysis ? "none detected" : "run the analysis");
  $("kpis").replaceChildren(
    kpi("People", nodes.length, `in ${departments} departments`, `${connections.length} reporting lines`, "#00D2FF"),
    kpi("Network density", fmtB(density), "of possible links", "lower = more hierarchical", "#6366F1", "#C0C1FF"),
    kpi("Span of control", span.toFixed(1), "directs / manager", `${spans.length} managers`, "#10B981", "#69F6B9"),
    kpi("Critical bottlenecks", analysis ? hot.length : "–", "friction hubs", names, "#F43F5E", "#FFB4AB"));
}

function renderGraphPanel() {
  const { nodes, connections, metrics, analysis } = state;
  const shown = limitNodes(nodes, connections, metrics);
  renderGraph($("graph"), {
    nodes: shown.nodes, connections: shown.connections, metrics,
    hierarchy: state.layout === "hierarchy", labels: state.labels,
    selected: shown.nodes.some((n) => n.id === state.selected) ? state.selected : null,
    onSelect: (id) => { state.selected = id; renderInspector(); },
  });
  const colors = departmentColors(nodes);
  const legend = Object.entries(colors).map(([d, c]) => h("span", {}, h("i", { class: "dot", style: `background:${c}` }), d));
  if (analysis) legend.push(h("span", {}, h("i", { class: "dot", style: `background:${BOTTLENECK}` }), "bottleneck"));
  $("legend").replaceChildren(...legend);
  $("graph-hint").textContent = "Drag · scroll to zoom · hover for details · click a person to inspect · node size = betweenness" +
    (shown.trimmed ? ` · showing the ${shown.nodes.length} most central of ${nodes.length} people` : "");
}

for (const [id, key] of [["layout-seg", "layout"], ["labels-seg", "labels"]]) {
  $(id).addEventListener("click", (e) => {
    const button = e.target.closest("button");
    if (!button) return;
    state[key] = button.dataset.v;
    for (const b of $(id).children) b.classList.toggle("on", b === button);
    if (state.board) renderGraphPanel();
  });
}

$("zoom-seg").addEventListener("click", (e) => {
  const v = e.target.closest("button")?.dataset.v;
  if (v === "in") zoomGraph(1.3); else if (v === "out") zoomGraph(1 / 1.3); else if (v === "fit") fitGraph();
});

function selectPerson(id, { scroll = false } = {}) {
  state.selected = id;
  renderInspector();
  selectInGraph(id);
  if (scroll) $("inspector").scrollIntoView({ behavior: "smooth", block: "center" });
}

function microBox(label, value, sub, valueStyle = "") {
  return h("div", { class: "micro" }, h("div", { class: "l" }, label), h("div", { class: "v", style: valueStyle }, value), h("div", { class: "s" }, sub));
}

function renderInspector() {
  const { nodes, byId, metrics, bossOf, reportsOf, ranked } = state;
  const box = $("inspector");
  box.replaceChildren(h("div", { class: "panel-title" }, "Person inspector"));
  if (!nodes.length) { box.append(h("p", { class: "muted small" }, "This board has no people yet.")); return; }

  const choices = ranked.slice(0, 400);
  if (!choices.some((n) => n.id === state.selected)) choices.push(byId.get(state.selected));
  box.append(h("select", { "aria-label": "Person", onchange: (e) => selectPerson(e.target.value) },
    choices.map((n) => h("option", { value: n.id, selected: n.id === state.selected }, n.name))));

  const p = byId.get(state.selected);
  const m = metrics.get(p.id);
  const boss = byId.get(bossOf.get(p.id));
  const directs = (reportsOf.get(p.id) ?? []).map((id) => byId.get(id)).filter(Boolean);
  const hot = !!m?.is_bottleneck;
  const color = hot ? BOTTLENECK : departmentColors(nodes)[p.department.trim()];
  const chip = hot ? h("span", { class: "chip bad" }, "Bottleneck")
    : directs.length ? h("span", { class: "chip info" }, "Manager") : h("span", { class: "chip mut" }, "Individual");

  box.append(
    h("div", { class: "person" },
      h("div", { class: "avatar", style: `background:${color}` }, initials(p.name)),
      h("div", { style: "flex:1;min-width:0" }, h("div", { class: "name" }, p.name),
        h("div", { class: "muted small" }, `${p.title} · ${p.department.trim()}`)),
      chip),
    h("div", { class: "micro-grid" },
      microBox("Direct reports", directs.length, "span of control"),
      microBox("Reports to", boss ? boss.name : "— (top)", boss ? boss.title : "no supervisor", "font-size:14px"),
      m ? microBox("Betweenness", fmtB(m.betweenness), `rank ${ranked.findIndex((n) => n.id === p.id) + 1} of ${nodes.length}`, "color:#A5E7FF;font-family:var(--mono)") : null,
      m ? microBox("In / out links", `${m.in_degree} / ${m.out_degree}`, "incoming / outgoing", "font-family:var(--mono)") : null));
  if (!m) box.append(h("p", { class: "hint" }, "Run the analysis to see centrality metrics for this person."));
  if (directs.length) {
    box.append(h("div", { class: "sec" }, "Direct reports"),
      ...directs.slice(0, 6).map((d) => h("div", { class: "row clickable", onclick: () => selectPerson(d.id) }, h("span", {}, d.name), h("span", {}, d.title))),
      ...(directs.length > 6 ? [h("p", { class: "hint" }, `+ ${directs.length - 6} more`)] : []));
  }
}

function renderRanking() {
  const { ranked, metrics, analysis } = state;
  const box = $("ranking");
  box.replaceChildren(h("div", { class: "panel-title" }, "Bottleneck ranking"),
    h("div", { class: "muted small" }, "Who most of the shortest paths run through."));
  const top = ranked.filter((n) => (metrics.get(n.id)?.betweenness ?? 0) > 0).slice(0, 5);
  if (!analysis) box.append(h("p", { class: "hint" }, "Run an analysis to rank people by betweenness."));
  else if (!top.length) box.append(h("p", { class: "hint" }, "No one stands out – the network has no central connectors."));
  const max = metrics.get(top[0]?.id)?.betweenness || 1;
  for (const n of top) {
    const m = metrics.get(n.id);
    box.append(h("div", { class: "rank-row clickable", onclick: () => selectPerson(n.id) },
      h("div", { class: "head" }, h("span", {}, n.name), h("span", { class: "mono muted small" }, fmtB(m.betweenness))),
      h("div", { class: "bar" }, h("i", { style: `width:${Math.round((100 * m.betweenness) / max)}%;background:${m.is_bottleneck ? BOTTLENECK : "#00D2FF"}` }))));
  }
}

function renderRecs() {
  const { recs, analysis } = state;
  const box = $("recs");
  const pending = recs.filter((r) => r.status === "Delivered").length;
  $("recs-count").hidden = !recs.length;
  $("recs-count").textContent = pending ? `${pending} awaiting your decision` : `${recs.length} answered`;
  $("recs-count").className = `count ${pending ? "pending" : ""}`;
  if (!analysis) { box.replaceChildren(h("p", { class: "hint" }, "Run an analysis first.")); return; }
  if (!recs.length) { box.replaceChildren(h("p", { class: "hint" }, "None yet – they are generated automatically after the analysis, or press “Insights”.")); return; }
  const chips = { Delivered: "info", Approved: "ok", Rejected: "mut" };
  box.replaceChildren(...recs.map((r) => {
    const comment = h("input", { placeholder: "Comment (optional)", "aria-label": "Comment" });
    const answer = (response) => async () => {
      if (await attempt(() => call("POST", `/api/v1/recommendations/${r.id}/respond`, { json: { response, comment: comment.value || null } }),
        response === "approve" ? "Approved." : "Rejected.")) await refresh({ quiet: true });
    };
    return h("div", { class: "card rec" },
      h("div", { class: "head" }, h("b", {}, h("i", { class: "dot", style: `background:${BOTTLENECK}` }), r.target_node_name),
        h("span", { class: `chip ${chips[r.status] || "mut"}` }, r.status)),
      h("p", {}, r.content),
      r.status === "Delivered"
        ? [comment, h("div", { class: "btns" },
          h("button", { class: "btn primary", type: "button", onclick: answer("approve") }, "Approve"),
          h("button", { class: "btn", type: "button", onclick: answer("reject") }, "Reject"))]
        : r.user_comment ? h("p", { class: "hint" }, `Comment: ${r.user_comment}`) : null);
  }));
}

const COLUMNS = [
  ["name", "Person"], ["title", "Title"], ["department", "Department"], ["boss", "Reports to"],
  ["directs", "Direct reports", true], ["betweenness", "Betweenness"], ["status", "Status"],
];

function rosterRows() {
  const { ranked, metrics, bossOf, reportsOf, byId } = state;
  const q = state.search.trim().toLowerCase();
  return ranked
    .map((n) => {
      const m = metrics.get(n.id);
      return {
        id: n.id, name: n.name, title: n.title, department: n.department.trim(),
        boss: byId.get(bossOf.get(n.id))?.name ?? "—", directs: reportsOf.get(n.id)?.length ?? 0,
        betweenness: m?.betweenness ?? 0, status: m?.is_bottleneck ? "Bottleneck" : reportsOf.has(n.id) ? "Manager" : "Individual",
      };
    })
    .filter((r) => !q || `${r.name} ${r.title} ${r.department}`.toLowerCase().includes(q))
    .sort((a, b) => {
      const { key, dir } = state.sort;
      const cmp = typeof a[key] === "number" ? a[key] - b[key] : String(a[key]).localeCompare(String(b[key]));
      return cmp * dir || a.name.localeCompare(b.name);
    });
}

function renderRoster() {
  const rows = rosterRows();
  const max = Math.max(0, ...rows.map((r) => r.betweenness)) || 1;
  const arrow = (key) => (state.sort.key === key ? (state.sort.dir > 0 ? " ▲" : " ▼") : "");
  const head = h("thead", {}, h("tr", {}, COLUMNS.map(([key, label, num]) =>
    h("th", { class: num ? "num" : "", onclick: () => {
      state.sort = { key, dir: state.sort.key === key ? -state.sort.dir : (key === "betweenness" || key === "directs" ? -1 : 1) };
      renderRoster();
    } }, label + arrow(key)))));
  const body = h("tbody", {}, rows.slice(0, 500).map((r) => h("tr", { onclick: () => selectPerson(r.id, { scroll: true }) },
    h("td", {}, r.name), h("td", {}, r.title), h("td", {}, r.department), h("td", {}, r.boss),
    h("td", { class: "num" }, r.directs),
    h("td", {}, h("div", { class: "pbar" }, h("div", { class: "track" }, h("i", { style: `width:${Math.round((100 * r.betweenness) / max)}%` })), h("span", {}, fmtB(r.betweenness)))),
    h("td", {}, h("span", { class: `chip ${r.status === "Bottleneck" ? "bad" : r.status === "Manager" ? "info" : "mut"}` }, r.status)))));
  $("roster-table").replaceChildren(head, body);
  $("roster-count").textContent = `Showing ${Math.min(rows.length, 500)} of ${rows.length} matching people`;
}

$("search").addEventListener("input", (e) => { state.search = e.target.value; renderRoster(); });

// ------------------------------------------------------------------ start
if (session.read()?.token) showApp(); else showAuth();
