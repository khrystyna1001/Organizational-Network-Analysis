// Network view on vis-network (vendored in vendor/). No data is ever put in innerHTML.
export const BG = "#0A0E15";
export const BOTTLENECK = "#F43F5E";
const PALETTE = ["#00D2FF", "#6366F1", "#10B981", "#A78BFA", "#F59E0B", "#94A3B8", "#2DD4BF", "#F472B6", "#60A5FA", "#A3E635"];
export const MAX_NODES = 400;

export function departmentColors(nodes) {
  const names = [...new Set(nodes.map((n) => n.department.trim()))].sort();
  return Object.fromEntries(names.map((d, i) => [d, PALETTE[i % PALETTE.length]]));
}

/** Big graphs: keep the most central people, then the biggest hubs, plus their direct neighbours. */
export function limitNodes(nodes, connections, metrics) {
  if (nodes.length <= MAX_NODES) return { nodes, connections, trimmed: false };
  const degree = new Map();
  for (const c of connections) {
    degree.set(c.source_node_id, (degree.get(c.source_node_id) ?? 0) + 1);
    degree.set(c.target_node_id, (degree.get(c.target_node_id) ?? 0) + 1);
  }
  const score = (n) => [metrics.get(n.id)?.betweenness ?? 0, degree.get(n.id) ?? 0];
  const ranked = [...nodes].sort((a, b) => {
    const [ba, da] = score(a), [bb, db] = score(b);
    return bb - ba || db - da;
  });
  const keep = new Set(ranked.slice(0, MAX_NODES / 4).map((n) => n.id));
  for (const c of connections) {
    if (keep.size >= MAX_NODES) break;
    if (keep.has(c.source_node_id) || keep.has(c.target_node_id)) { keep.add(c.source_node_id); keep.add(c.target_node_id); }
  }
  return {
    nodes: nodes.filter((n) => keep.has(n.id)),
    connections: connections.filter((c) => keep.has(c.source_node_id) && keep.has(c.target_node_id)),
    trimmed: true,
  };
}

/** A tree reads best as a hierarchy; many separate pieces would become one endless row, so use the force layout. */
export function suggestedLayout(nodes, connections) {
  const hasParent = new Set(connections.map((c) => c.target_node_id));
  const roots = nodes.filter((n) => !hasParent.has(n.id)).length;
  return roots > 12 ? "force" : "hierarchy";
}

function options(hierarchy) {
  const o = {
    nodes: { shape: "dot", borderWidth: 2, borderWidthSelected: 4,
      font: { color: "#e8ecff", size: 15, face: "Inter, sans-serif", strokeWidth: 4, strokeColor: BG }, shadow: { enabled: false } },
    edges: {
      arrows: { to: { enabled: true, scaleFactor: 0.45 } },
      color: { color: "rgba(148,163,184,0.42)", highlight: "#00D2FF", hover: "#00D2FF" },
      width: 1.2, selectionWidth: 2.2,
      smooth: hierarchy ? { enabled: true, type: "cubicBezier", forceDirection: "vertical", roundness: 0.55 }
                        : { enabled: true, type: "continuous", roundness: 0.2 },
    },
    interaction: { hover: true, hoverConnectedEdges: true, tooltipDelay: 60, navigationButtons: false, keyboard: { enabled: true }, zoomSpeed: 0.6 },
  };
  if (hierarchy) {
    o.layout = { hierarchical: { enabled: true, direction: "UD", sortMethod: "directed", shakeTowards: "roots",
      levelSeparation: 170, nodeSpacing: 110, treeSpacing: 150 } };
    o.physics = { enabled: false };
  } else {
    o.physics = { solver: "forceAtlas2Based",
      forceAtlas2Based: { gravitationalConstant: -70, centralGravity: 0.012, springLength: 110, springConstant: 0.06, damping: 0.5 },
      stabilization: { iterations: 250 } };
  }
  return o;
}

let network = null;

/** metrics: Map(node_id -> {betweenness, in_degree, out_degree, is_bottleneck}) – may be empty. */
export function renderGraph(container, { nodes, connections, metrics, hierarchy, labels, selected, onSelect }) {
  if (network) { network.destroy(); network = null; }
  const colors = departmentColors(nodes);
  const maxB = Math.max(0, ...[...metrics.values()].map((m) => m.betweenness)) || 1;
  const few = nodes.length <= 60;

  const items = nodes.map((n) => {
    const m = metrics.get(n.id);
    const hot = !!m?.is_bottleneck;
    const base = hot ? BOTTLENECK : colors[n.department.trim()];
    const showLabel = labels === "all" || (labels === "smart" && (few || hot));
    const item = {
      id: n.id,
      label: showLabel ? n.name : " ",
      title: `${n.name}\n${n.title} · ${n.department.trim()}` +
        (m ? `\nBetweenness ${m.betweenness.toFixed(4)} · in ${m.in_degree} · out ${m.out_degree}` : "") + (hot ? "\nBOTTLENECK" : ""),
      size: metrics.size ? 12 + 26 * Math.sqrt((m?.betweenness ?? 0) / maxB) : 16,
      color: { background: base, border: hot ? "#ffffff" : "rgba(255,255,255,0.55)",
        highlight: { background: base, border: "#ffffff" }, hover: { background: base, border: "#ffffff" } },
    };
    if (hot) {
      item.shadow = { enabled: true, color: "rgba(244,63,94,0.55)", size: 20, x: 0, y: 0 };
      item.font = { color: "#ffffff", size: 17, face: "Inter, sans-serif", bold: true, strokeWidth: 5, strokeColor: BG };
    }
    return item;
  });
  const edges = connections.map((c) => ({ from: c.source_node_id, to: c.target_node_id }));

  network = new vis.Network(container, { nodes: new vis.DataSet(items), edges: new vis.DataSet(edges) }, options(hierarchy));
  network.on("selectNode", (e) => onSelect?.(e.nodes[0]));
  if (!hierarchy) {  // freeze the physics once it has settled so the picture stays calm
    network.once("stabilizationIterationsDone", () => { network.setOptions({ physics: false }); network.fit({ animation: true }); });
  }
  if (selected) network.selectNodes([selected]);
  return network;
}

export function selectInGraph(id) {
  if (network && id) { try { network.selectNodes([id]); } catch { /* node not drawn (trimmed) */ } }
}

export function zoomGraph(factor) {
  if (network) network.moveTo({ scale: network.getScale() * factor, animation: { duration: 200, easingFunction: "easeInOutQuad" } });
}
export function fitGraph() {
  if (network) network.fit({ animation: { duration: 300, easingFunction: "easeInOutQuad" } });
}
