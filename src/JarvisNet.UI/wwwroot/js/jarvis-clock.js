(function () {
  "use strict";

  const timeEl = document.getElementById("hud-time");
  const dateEl = document.getElementById("hud-date");
  if (!timeEl || !dateEl) return;

  const dateFmt = new Intl.DateTimeFormat("en-US", {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: "numeric",
  });

  function pad2(n) {
    return String(n).padStart(2, "0");
  }

  function tick() {
    const now = new Date();
    timeEl.textContent =
      pad2(now.getHours()) +
      ":" +
      pad2(now.getMinutes()) +
      ":" +
      pad2(now.getSeconds());
    timeEl.dateTime = now.toISOString();
    dateEl.textContent = dateFmt.format(now);
    dateEl.dateTime = now.toISOString().slice(0, 10);
  }

  tick();
  setInterval(tick, 1000);
})();
