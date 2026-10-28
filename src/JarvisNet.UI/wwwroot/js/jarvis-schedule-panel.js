(function () {
  "use strict";

  var listEl = document.getElementById("schedule-list");
  var enabledEl = document.getElementById("schedules-enabled");
  var statusEl = document.getElementById("schedule-panel-status");
  var btnAdd = document.getElementById("btn-schedule-add");
  var btnSave = document.getElementById("btn-schedules-save");
  if (!listEl || !enabledEl || !btnSave) return;

  var WEEKDAYS = [
    { id: "Monday", label: "Lun" },
    { id: "Tuesday", label: "Mar" },
    { id: "Wednesday", label: "Mer" },
    { id: "Thursday", label: "Gio" },
    { id: "Friday", label: "Ven" },
    { id: "Saturday", label: "Sab" },
    { id: "Sunday", label: "Dom" },
  ];

  var state = { enabled: true, items: [] };
  var savedJson = "";
  var dirty = false;
  var expandedIds = Object.create(null);

  function setStatus(text, isError) {
    if (!statusEl) return;
    statusEl.textContent = text;
    statusEl.style.color = isError ? "#ff8a8a" : "";
  }

  function snapshotJson() {
    return JSON.stringify({
      enabled: state.enabled,
      items: state.items.map(function (it) {
        return {
          id: it.id,
          name: it.name,
          enabled: it.enabled,
          time: it.time,
          daysOfWeek: it.daysOfWeek.slice().sort(),
          runOnStartup: it.runOnStartup,
          speakResponse: it.speakResponse,
          prompt: it.prompt,
        };
      }),
    });
  }

  function markDirty() {
    dirty = snapshotJson() !== savedJson;
    btnSave.disabled = !dirty;
  }

  function newId() {
    if (typeof crypto !== "undefined" && crypto.randomUUID) {
      return crypto.randomUUID();
    }
    return "sched-" + Date.now().toString(36);
  }

  function normalizeItem(raw) {
    return {
      id: raw.id || newId(),
      name: raw.name || "Nuovo prompt",
      enabled: raw.enabled !== false,
      time: raw.time || "08:00",
      daysOfWeek: Array.isArray(raw.daysOfWeek) && raw.daysOfWeek.length
        ? raw.daysOfWeek.slice()
        : ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
      runOnStartup: !!raw.runOnStartup,
      speakResponse: !!raw.speakResponse,
      prompt: raw.prompt || "",
    };
  }

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function render() {
    listEl.innerHTML = "";
    if (!state.items.length) {
      var empty = document.createElement("p");
      empty.className = "schedule-empty";
      empty.textContent = "Nessun prompt schedulato.";
      listEl.appendChild(empty);
      return;
    }

    state.items.forEach(function (item, index) {
      var card = document.createElement("article");
      var isExpanded = !!expandedIds[item.id];
      card.className = "schedule-item" + (isExpanded ? " is-expanded" : "");
      card.setAttribute("role", "listitem");

      var daysHtml = WEEKDAYS.map(function (d) {
        var checked = item.daysOfWeek.indexOf(d.id) >= 0 ? " checked" : "";
        return (
          '<label class="schedule-day-chip">' +
          '<input type="checkbox" data-index="' + index + '" data-day="' + d.id + '"' + checked + " />" +
          "<span>" + d.label + "</span></label>"
        );
      }).join("");

      card.innerHTML =
        '<div class="schedule-item-head" role="button" tabindex="0" aria-expanded="' + (isExpanded ? "true" : "false") + '">' +
        '<span class="schedule-item-chevron" aria-hidden="true"></span>' +
        '<input type="checkbox" class="schedule-item-enabled" data-index="' + index + '"' +
        (item.enabled ? " checked" : "") +
        ' aria-label="Abilita schedule" />' +
        '<span class="schedule-item-name">' + escapeHtml(item.name) + "</span>" +
        '<span class="schedule-item-time">' + escapeHtml(item.time) + "</span>" +
        '<button type="button" class="schedule-item-remove" data-index="' + index + '" aria-label="Elimina">×</button>' +
        "</div>" +
        '<div class="schedule-item-body">' +
        '<label class="schedule-field"><span class="schedule-field-label">Nome</span>' +
        '<input class="schedule-input schedule-name" data-index="' + index + '" value="' + escapeHtml(item.name) + '" /></label>' +
        '<label class="schedule-field"><span class="schedule-field-label">Ora (HH:mm)</span>' +
        '<input class="schedule-input schedule-time" data-index="' + index + '" type="time" value="' + escapeHtml(item.time) + '" /></label>' +
        '<div class="schedule-field"><span class="schedule-field-label">Giorni</span><div class="schedule-days">' + daysHtml + "</div></div>" +
        '<div class="schedule-flags">' +
        '<label class="schedule-flag"><input type="checkbox" class="schedule-run-startup" data-index="' + index + '"' +
        (item.runOnStartup ? " checked" : "") +
        " /><span>All'avvio se l'ora è già passata</span></label>" +
        '<label class="schedule-flag"><input type="checkbox" class="schedule-speak" data-index="' + index + '"' +
        (item.speakResponse ? " checked" : "") +
        " /><span>Leggi risposta con TTS</span></label>" +
        "</div>" +
        '<label class="schedule-field"><span class="schedule-field-label">Prompt</span>' +
        '<textarea class="schedule-textarea schedule-prompt" data-index="' + index + '">' +
        escapeHtml(item.prompt) +
        "</textarea></label>" +
        "</div>";

      listEl.appendChild(card);
    });

    bindItemEvents();
  }

  function toggleItemExpanded(card, itemId) {
    var expanded = card.classList.toggle("is-expanded");
    if (expanded) {
      expandedIds[itemId] = true;
    } else {
      delete expandedIds[itemId];
    }
    var head = card.querySelector(".schedule-item-head");
    if (head) {
      head.setAttribute("aria-expanded", expanded ? "true" : "false");
    }
  }

  function bindItemEvents() {
    listEl.querySelectorAll(".schedule-item").forEach(function (card) {
      var idx = Number(card.querySelector("[data-index]")?.getAttribute("data-index"));
      var item = state.items[idx];
      if (!item) return;

      var head = card.querySelector(".schedule-item-head");
      if (!head) return;

      head.addEventListener("click", function (e) {
        if (e.target.closest(".schedule-item-enabled, .schedule-item-remove")) return;
        toggleItemExpanded(card, item.id);
      });

      head.addEventListener("keydown", function (e) {
        if (e.key !== "Enter" && e.key !== " ") return;
        if (e.target.closest(".schedule-item-enabled, .schedule-item-remove")) return;
        e.preventDefault();
        toggleItemExpanded(card, item.id);
      });
    });

    listEl.querySelectorAll(".schedule-item-enabled").forEach(function (el) {
      el.addEventListener("click", function (e) {
        e.stopPropagation();
      });
      el.addEventListener("change", function () {
        var idx = Number(el.getAttribute("data-index"));
        state.items[idx].enabled = el.checked;
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-name").forEach(function (el) {
      el.addEventListener("input", function () {
        var idx = Number(el.getAttribute("data-index"));
        state.items[idx].name = el.value;
        var nameEl = el.closest(".schedule-item").querySelector(".schedule-item-name");
        if (nameEl) nameEl.textContent = el.value || "Senza nome";
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-time").forEach(function (el) {
      el.addEventListener("change", function () {
        var idx = Number(el.getAttribute("data-index"));
        state.items[idx].time = el.value || "08:00";
        var timeEl = el.closest(".schedule-item").querySelector(".schedule-item-time");
        if (timeEl) timeEl.textContent = state.items[idx].time;
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-prompt").forEach(function (el) {
      el.addEventListener("input", function () {
        var idx = Number(el.getAttribute("data-index"));
        state.items[idx].prompt = el.value;
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-run-startup").forEach(function (el) {
      el.addEventListener("change", function () {
        var idx = Number(el.getAttribute("data-index"));
        state.items[idx].runOnStartup = el.checked;
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-speak").forEach(function (el) {
      el.addEventListener("change", function () {
        var idx = Number(el.getAttribute("data-index"));
        state.items[idx].speakResponse = el.checked;
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-day-chip input").forEach(function (el) {
      el.addEventListener("change", function () {
        var idx = Number(el.getAttribute("data-index"));
        var day = el.getAttribute("data-day");
        var days = state.items[idx].daysOfWeek;
        if (el.checked) {
          if (days.indexOf(day) < 0) days.push(day);
        } else {
          state.items[idx].daysOfWeek = days.filter(function (d) { return d !== day; });
        }
        markDirty();
      });
    });

    listEl.querySelectorAll(".schedule-item-remove").forEach(function (el) {
      el.addEventListener("click", function (e) {
        e.stopPropagation();
        var idx = Number(el.getAttribute("data-index"));
        var removed = state.items[idx];
        if (removed) delete expandedIds[removed.id];
        state.items.splice(idx, 1);
        render();
        markDirty();
      });
    });
  }

  function applyPayload(data) {
    state.enabled = data.enabled !== false;
    state.items = (data.items || []).map(normalizeItem);
    enabledEl.checked = state.enabled;
    savedJson = snapshotJson();
    dirty = false;
    btnSave.disabled = true;
    render();
    setStatus(state.items.length ? "Ready" : "Empty");
  }

  async function load() {
    setStatus("Loading");
    try {
      var res = await fetch("/api/schedules");
      if (!res.ok) throw new Error("Load failed");
      applyPayload(await res.json());
    } catch (e) {
      setStatus("Error", true);
    }
  }

  async function save() {
    btnSave.disabled = true;
    setStatus("Saving…");
    try {
      var body = {
        enabled: enabledEl.checked,
        items: state.items.map(function (it) {
          return {
            id: it.id,
            name: it.name,
            enabled: it.enabled,
            time: it.time,
            daysOfWeek: it.daysOfWeek,
            runOnStartup: it.runOnStartup,
            speakResponse: it.speakResponse,
            prompt: it.prompt,
          };
        }),
      };
      var res = await fetch("/api/schedules", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        var err = await res.json().catch(function () { return {}; });
        throw new Error(err.error || "Save failed");
      }
      applyPayload(await res.json());
      setStatus("Saved");
    } catch (e) {
      setStatus(e.message || "Error", true);
      markDirty();
    }
  }

  enabledEl.addEventListener("change", function () {
    state.enabled = enabledEl.checked;
    markDirty();
  });

  if (btnAdd) {
    btnAdd.addEventListener("click", function () {
      var id = newId();
      expandedIds[id] = true;
      state.items.push(
        normalizeItem({
          id: id,
          name: "Nuovo prompt",
          prompt: "",
          runOnStartup: false,
        })
      );
      var schedulePanel = document.querySelector(".schedule-panel");
      if (schedulePanel && schedulePanel.classList.contains("is-collapsed")) {
        schedulePanel.classList.remove("is-collapsed");
        var trigger = schedulePanel.querySelector(".settings-panel-trigger");
        if (trigger) trigger.setAttribute("aria-expanded", "true");
      }
      render();
      markDirty();
    });
  }

  btnSave.addEventListener("click", function () {
    if (dirty) save();
  });

  load();
})();
