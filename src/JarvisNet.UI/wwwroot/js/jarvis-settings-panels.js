(function () {
  "use strict";

  document.querySelectorAll(".settings-panel-trigger").forEach(function (trigger) {
    var panel = trigger.closest(".settings-panel");
    if (!panel) return;

    function setExpanded(expanded) {
      panel.classList.toggle("is-collapsed", !expanded);
      trigger.setAttribute("aria-expanded", expanded ? "true" : "false");
    }

    function toggle() {
      setExpanded(panel.classList.contains("is-collapsed"));
    }

    trigger.addEventListener("click", function (e) {
      if (e.target.closest("select, input, button, label")) return;
      toggle();
    });

    trigger.addEventListener("keydown", function (e) {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        toggle();
      }
    });
  });
})();
