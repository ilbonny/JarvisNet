(function () {
  "use strict";

  const panelEl = document.getElementById("devices-panel");
  const listEl = document.getElementById("devices-list");
  const emptyEl = document.getElementById("devices-empty");
  const statusEl = document.getElementById("devices-panel-status");

  let onlineThresholdSeconds = 120;
  let devices = [];
  let deviceSnapshotById = new Map();
  let devicesHydrated = false;
  let panelPulseTimer = null;

  const typeLabels = {
    camera: "Camera",
  };

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function deviceTypeLabel(type) {
    return typeLabels[type] || type || "Device";
  }

  function iconForType(type) {
    if (type === "camera") {
      return (
        '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true">' +
        '<path d="M4 7h4l2-2h4l2 2h4v12H4V7z"/><circle cx="12" cy="13" r="3.5"/></svg>'
      );
    }

    return (
      '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true">' +
      '<rect x="5" y="4" width="14" height="16" rx="2"/><path d="M9 8h6M9 12h6M9 16h4"/></svg>'
    );
  }

  function computeStatus(device) {
    if (device.lastSeenUtc) {
      const last = Date.parse(device.lastSeenUtc);
      if (!Number.isNaN(last)) {
        const ageSec = (Date.now() - last) / 1000;
        if (ageSec > onlineThresholdSeconds) {
          return { key: "offline", label: "Offline", operational: false };
        }
      }
    }

    if (device.status === "degraded") {
      return { key: "degraded", label: "Degraded", operational: false };
    }

    return { key: "online", label: "Online", operational: true };
  }

  function statusDot(key, operational) {
    var cls = "device-status-dot";
    if (key === "online" && operational) {
      cls += " device-status-dot-ok";
    } else if (key === "degraded") {
      cls += " device-status-dot-warn";
    } else if (key === "offline") {
      cls += " device-status-dot-off";
    } else {
      cls += " device-status-dot-warn";
    }

    return '<span class="' + cls + '" title="' + escapeHtml(key) + '"></span>';
  }

  function collectActiveDeviceIds(nextDevices) {
    if (!devicesHydrated) {
      return new Set();
    }

    var active = new Set();
    nextDevices.forEach(function (device) {
      var id = device.id;
      if (!id) return;
      var prev = deviceSnapshotById.get(id);
      if (!prev) {
        active.add(id);
        return;
      }
      if (
        device.lastSeenUtc !== prev.lastSeenUtc ||
        device.lastMotionUtc !== prev.lastMotionUtc
      ) {
        active.add(id);
      }
    });
    return active;
  }

  function rememberDeviceSnapshots(nextDevices) {
    deviceSnapshotById = new Map();
    nextDevices.forEach(function (device) {
      if (!device.id) return;
      deviceSnapshotById.set(device.id, {
        lastSeenUtc: device.lastSeenUtc,
        lastMotionUtc: device.lastMotionUtc,
      });
    });
    devicesHydrated = true;
  }

  function pulseDevicesPanel() {
    if (!panelEl) return;
    panelEl.classList.remove("devices-panel--signal");
    void panelEl.offsetWidth;
    panelEl.classList.add("devices-panel--signal");
    if (panelPulseTimer) {
      clearTimeout(panelPulseTimer);
    }
    panelPulseTimer = setTimeout(function () {
      if (panelEl) panelEl.classList.remove("devices-panel--signal");
      panelPulseTimer = null;
    }, 900);
  }

  function flashDeviceItem(li) {
    if (!li) return;
    li.classList.remove("device-item--signal");
    void li.offsetWidth;
    li.classList.add("device-item--signal");
    li.addEventListener(
      "animationend",
      function onEnd() {
        li.classList.remove("device-item--signal");
        li.removeEventListener("animationend", onEnd);
      },
      { once: true }
    );
  }

  function render(activeDeviceIds) {
    if (!listEl) return;

    var signalIds =
      activeDeviceIds instanceof Set ? activeDeviceIds : new Set();

    listEl.innerHTML = "";

    if (!devices.length) {
      if (emptyEl) emptyEl.hidden = false;
      if (statusEl) statusEl.textContent = "0 online";
      return;
    }

    if (emptyEl) emptyEl.hidden = true;

    var onlineCount = 0;
    devices.forEach(function (device) {
      var st = computeStatus(device);
      if (st.key === "online" && st.operational) {
        onlineCount++;
      }

      var li = document.createElement("li");
      li.className = "device-item device-item-" + st.key;
      li.setAttribute("role", "listitem");
      if (device.id) {
        li.dataset.deviceId = device.id;
      }
      li.innerHTML =
        '<div class="device-item-icon">' +
        iconForType(device.type) +
        "</div>" +
        '<div class="device-item-body">' +
        '<div class="device-item-title-row">' +
        '<span class="device-item-name">' +
        escapeHtml(device.displayName || deviceTypeLabel(device.type)) +
        "</span>" +
        statusDot(st.key, st.operational) +
        "</div>" +
        '<span class="device-item-meta">' +
        escapeHtml(deviceTypeLabel(device.type)) +
        " · " +
        escapeHtml(device.ipAddress || "—") +
        (device.motionDetectionEnabled ? " · motion ON" : " · motion OFF") +
        "</span>" +
        '<span class="device-item-status-label">' +
        escapeHtml(st.label) +
        "</span>" +
        "</div>";

      listEl.appendChild(li);

      if (device.id && signalIds.has(device.id)) {
        flashDeviceItem(li);
      }
    });

    if (signalIds.size > 0) {
      pulseDevicesPanel();
    }

    if (statusEl) {
      statusEl.textContent =
        onlineCount + "/" + devices.length + " online";
    }
  }

  function applyDevices(next) {
    var incoming = Array.isArray(next) ? next.slice() : [];
    incoming.sort(function (a, b) {
      return String(a.displayName || "").localeCompare(
        String(b.displayName || ""),
        "en",
        { sensitivity: "base" }
      );
    });
    var activeIds = collectActiveDeviceIds(incoming);
    devices = incoming;
    render(activeIds);
    rememberDeviceSnapshots(incoming);
  }

  window.jarvisDevicesPanel = {
    applyDevices: applyDevices,
    setOnlineThreshold: function (seconds) {
      if (typeof seconds === "number" && seconds > 0) {
        onlineThresholdSeconds = seconds;
        render();
      }
    },
  };

  async function loadDevices() {
    try {
      const res = await fetch("/api/devices");
      if (!res.ok) return;
      const data = await res.json();
      if (data.onlineThresholdSeconds) {
        onlineThresholdSeconds = data.onlineThresholdSeconds;
      }
      applyDevices(data.devices);
    } catch (e) {
      console.error(e);
    }
  }

  loadDevices();
  setInterval(render, 5000);
  setInterval(loadDevices, 30000);
})();
