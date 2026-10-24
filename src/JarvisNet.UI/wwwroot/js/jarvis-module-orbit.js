(function () {
  "use strict";

  const stage = document.getElementById("hud-stage");
  const orbitEl = document.getElementById("module-orbit");
  const linksSvg = document.getElementById("hud-orbit-links");
  if (!stage || !orbitEl || !linksSvg) return;

  const MAX_LOG_LINES = 6;
  const ACTIVE_FADE_MS = 2200;
  const accentClasses = ["accent-cyan", "accent-purple", "accent-orange", "accent-magenta"];
  /** Anello esterno disegnato in jarvis-hud.js: raggio 210 su canvas 640. */
  const HUD_OUTER_RING_RATIO = 210 / 320;

  const nodesById = new Map();
  let modules = [];
  let activeTimers = new Map();

  function hashString(s) {
    let h = 2166136261;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return Math.abs(h >>> 0);
  }

  /** Anello esterno (% dal centro layer orbita 140%; raggio palla ~36% in quello spazio). */
  function placement(index, total, id) {
    const h = hashString(id);
    const slots = Math.max(total, 1);
    const baseAngle = (index / slots) * Math.PI * 2 + Math.PI * 0.2;
    const jitter = ((h % 1000) / 1000 - 0.5) * (slots === 1 ? 0.2 : 0.75);
    const angle = baseAngle + jitter;
    const dist = 62 + (h % 400) / 100;
    return {
      x: 50 + Math.cos(angle) * dist,
      y: 50 + Math.sin(angle) * dist,
    };
  }

  function getLinkSvgPoint(clientX, clientY) {
    const svgRect = linksSvg.getBoundingClientRect();
    return {
      x: clientX - svgRect.left,
      y: clientY - svgRect.top,
    };
  }

  function getHudCenterClient() {
    const canvas = document.getElementById("hud");
    if (canvas) {
      const r = canvas.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }
    const core = stage.querySelector(".hud-core");
    const rect = (core || stage).getBoundingClientRect();
    return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
  }

  function getOrbAnchor(nx, ny) {
    const canvas = document.getElementById("hud");
    const canvasRect = canvas
      ? canvas.getBoundingClientRect()
      : stage.getBoundingClientRect();
    const center = getHudCenterClient();
    const cx = center.x;
    const cy = center.y;
    const half = Math.min(canvasRect.width, canvasRect.height) / 2;
    const orbR = half * HUD_OUTER_RING_RATIO;
    const dx = nx - cx;
    const dy = ny - cy;
    const len = Math.hypot(dx, dy) || 1;
    return getLinkSvgPoint(cx + (dx / len) * orbR, cy + (dy / len) * orbR);
  }

  function getNodeAnchor(rect, centerX, centerY) {
    const nx = rect.left + rect.width / 2;
    const ny = rect.top + rect.height / 2;
    const dx = centerX - nx;
    const dy = centerY - ny;
    const len = Math.hypot(dx, dy) || 1;
    const ux = dx / len;
    const uy = dy / len;
    const hw = rect.width / 2;
    const hh = rect.height / 2;
    let t = Infinity;
    if (ux > 0) {
      t = Math.min(t, hw / ux);
    } else if (ux < 0) {
      t = Math.min(t, -hw / ux);
    }
    if (uy > 0) {
      t = Math.min(t, hh / uy);
    } else if (uy < 0) {
      t = Math.min(t, -hh / uy);
    }
    if (!isFinite(t)) {
      t = 0;
    }
    return getLinkSvgPoint(nx + ux * t, ny + uy * t);
  }

  function formatTime() {
    const d = new Date();
    return d.toTimeString().slice(0, 8);
  }

  function escapeHtml(s) {
    return s
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function clearNodes() {
    orbitEl.innerHTML = "";
    linksSvg.innerHTML = "";
    nodesById.clear();
  }

  function drawLinks() {
    linksSvg.innerHTML = "";
    const w = linksSvg.clientWidth;
    const h = linksSvg.clientHeight;
    if (!w || !h) return;

    linksSvg.setAttribute("viewBox", "0 0 " + w + " " + h);

    const center = getHudCenterClient();

    nodesById.forEach(function (entry) {
      const node = entry.el;
      const rect = node.getBoundingClientRect();
      const nxClient = rect.left + rect.width / 2;
      const nyClient = rect.top + rect.height / 2;
      const nodePt = getNodeAnchor(rect, center.x, center.y);
      const nx = nodePt.x;
      const ny = nodePt.y;
      const anchor = getOrbAnchor(nxClient, nyClient);
      const sx = anchor.x;
      const sy = anchor.y;
      const midX = sx + (nx - sx) * 0.55;
      const active = entry.el.classList.contains("active");

      const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      path.setAttribute(
        "d",
        "M " +
          sx +
          " " +
          sy +
          " L " +
          midX +
          " " +
          sy +
          " L " +
          midX +
          " " +
          ny +
          " L " +
          nx +
          " " +
          ny
      );
      path.setAttribute("class", "orbit-link" + (active ? " orbit-link-active" : ""));
      linksSvg.appendChild(path);

      const dot = document.createElementNS("http://www.w3.org/2000/svg", "circle");
      dot.setAttribute("cx", String(sx));
      dot.setAttribute("cy", String(sy));
      dot.setAttribute("r", active ? "3.5" : "2.5");
      dot.setAttribute("class", "orbit-link-node" + (active ? " orbit-link-node-active" : ""));
      linksSvg.appendChild(dot);
    });
  }

  function buildNode(mod, index, total) {
    const pos = placement(index, total, mod.id);
    const accent = accentClasses[hashString(mod.id) % accentClasses.length];
    const node = document.createElement("article");
    node.className = "module-node " + accent;
    node.dataset.moduleId = mod.id;
    node.style.left = pos.x + "%";
    node.style.top = pos.y + "%";

    const kindLabel = mod.kind === "mcp" ? "MCP" : "PLUGIN";
    const title = mod.id.toUpperCase();
    const subtitle =
      mod.description && mod.description.length > 0
        ? mod.description
        : mod.toolCount + " tool";

    node.innerHTML =
      '<div class="module-node-glow" aria-hidden="true"></div>' +
      '<div class="module-node-core">' +
      '<span class="module-node-kind">' +
      escapeHtml(kindLabel) +
      "</span>" +
      '<h3 class="module-node-title">' +
      escapeHtml(title) +
      "</h3>" +
      '<p class="module-node-sub">' +
      escapeHtml(subtitle.length > 48 ? subtitle.slice(0, 45) + "…" : subtitle) +
      "</p>" +
      "</div>" +
      '<div class="module-node-log" aria-live="polite"></div>';

    orbitEl.appendChild(node);
    nodesById.set(mod.id, { el: node, logEl: node.querySelector(".module-node-log") });
  }

  function setModules(list) {
    modules = Array.isArray(list) ? list.slice() : [];
    clearNodes();
    modules.forEach(function (mod, i) {
      buildNode(mod, i, modules.length);
    });
    requestAnimationFrame(function () {
      drawLinks();
      requestAnimationFrame(drawLinks);
    });
  }

  function appendLog(moduleId, text) {
    const entry = nodesById.get(moduleId);
    if (!entry || !entry.logEl) return;
    const line = document.createElement("div");
    line.className = "module-log-line";
    line.innerHTML =
      '<span class="module-log-time">' +
      formatTime() +
      "</span> " +
      escapeHtml(text);
    entry.logEl.appendChild(line);
    while (entry.logEl.children.length > MAX_LOG_LINES) {
      entry.logEl.removeChild(entry.logEl.firstChild);
    }
    entry.logEl.scrollTop = entry.logEl.scrollHeight;
  }

  function setActive(moduleId, active) {
    const entry = nodesById.get(moduleId);
    if (!entry) return;
    entry.el.classList.toggle("active", active);
    drawLinks();
  }

  function pulseModule(moduleId, displayName, phase) {
    const display = displayName || "tool";
    if (phase === "start") {
      setActive(moduleId, true);
      appendLog(moduleId, "> " + display);
      window.mcpActiveTool = display;
      window.jarvisState = "ExecutingMcpTool";

      if (activeTimers.has(moduleId)) {
        clearTimeout(activeTimers.get(moduleId));
      }
      activeTimers.set(
        moduleId,
        setTimeout(function () {
          setActive(moduleId, false);
          activeTimers.delete(moduleId);
          if (window.mcpActiveTool && window.mcpActiveTool.startsWith(moduleId + ".")) {
            window.mcpActiveTool = null;
          }
        }, ACTIVE_FADE_MS)
      );
    } else if (phase === "finish") {
      appendLog(moduleId, "done " + display);
    }
  }

  function onToolEvent(payload, phase) {
    if (!payload) return;
    const moduleId = payload.moduleId || payload.ModuleId;
    const toolName = payload.toolName || payload.ToolName || "";
    const displayName = payload.displayName || payload.DisplayName || toolName;
    if (!moduleId) return;
    if (!nodesById.has(moduleId)) {
      fetchModules();
      return;
    }
    pulseModule(moduleId, displayName, phase);
  }

  async function fetchModules() {
    try {
      const res = await fetch("/api/modules");
      if (!res.ok) return;
      const data = await res.json();
      setModules(data);
    } catch (e) {
      console.error(e);
    }
  }

  window.jarvisModuleOrbit = {
    setModules: setModules,
    onToolStarted: function (payload) {
      onToolEvent(payload, "start");
    },
    onToolFinished: function (payload) {
      onToolEvent(payload, "finish");
    },
    redraw: drawLinks,
  };

  window.addEventListener("resize", function () {
    drawLinks();
  });

  fetchModules();

  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(function () {
      drawLinks();
    });
  }

  window.addEventListener("load", function () {
    drawLinks();
  });
})();
