(function () {
  "use strict";

  const POLL_MS = 2500;

  const fields = [
    { valueId: "sys-ai-model", statusKey: "aiModelStatus", format: (d) => d.aiModel },
    { valueId: "sys-language", statusKey: "languageStatus", format: (d) => d.language },
    {
      valueId: "sys-cpu",
      statusKey: "cpuStatus",
      format: (d) => formatPercent(d.cpuUsagePercent),
    },
    {
      valueId: "sys-memory",
      statusKey: "memoryStatus",
      format: (d) => formatPercent(d.memoryUsagePercent),
    },
    {
      valueId: "sys-gpu-memory",
      statusKey: "gpuMemoryStatus",
      format: (d) => formatPercent(d.gpuMemoryUsagePercent),
    },
    {
      valueId: "sys-network",
      statusKey: "networkStatus",
      format: (d) => formatNetwork(d.networkKbps),
    },
    {
      valueId: "sys-power",
      statusKey: "powerDetail",
      format: (d) => d.powerStatus,
    },
    {
      valueId: "sys-telegram",
      statusKey: "telegramStatus",
      format: (d) => d.telegramBot || "Off",
    },
  ];

  function formatPercent(value) {
    if (value === null || value === undefined) {
      return "—";
    }
    return Math.round(value) + "%";
  }

  function formatNetwork(kbps) {
    if (kbps === null || kbps === undefined) {
      return "0 Kbps";
    }
    if (kbps >= 1024) {
      return (kbps / 1024).toFixed(1) + " Mbps";
    }
    return Math.round(kbps) + " Kbps";
  }

  function applyTelegramMetric(data) {
    const metric = document.getElementById("sys-telegram-metric");
    if (!metric || !data) return;
    const online =
      data.telegramEnabled === true &&
      (data.telegramStatus === "Online" || data.telegramStatus === "Starting");
    metric.classList.toggle("sys-metric-on", online);
    metric.classList.toggle("sys-metric-off", !online);
    metric.setAttribute(
      "title",
      online
        ? "Bot Telegram attivo: " + (data.telegramBot || "")
        : "Bot Telegram disabilitato (config/jarvisnet.telegram)"
    );
  }

  function applyData(data) {
    if (!data) return;
    applyTelegramMetric(data);
    fields.forEach(function (field) {
      const valueEl = document.getElementById(field.valueId);
      if (valueEl) {
        valueEl.textContent = field.format(data);
      }
      const statusEl = document.querySelector(
        '[data-sys-status="' + field.statusKey + '"]'
      );
      if (statusEl && data[field.statusKey] != null) {
        statusEl.textContent = data[field.statusKey];
      }
    });
  }

  async function refresh() {
    try {
      const res = await fetch("/api/system-status");
      if (!res.ok) return;
      const data = await res.json();
      applyData(data);
    } catch (e) {
      console.error(e);
    }
  }

  refresh();
  setInterval(refresh, POLL_MS);
})();
