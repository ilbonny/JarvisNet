(function () {
  "use strict";

  const statusWrap = document.getElementById("connection-status-wrap");
  const statusEl = document.getElementById("connection-status");
  const chatLog = document.getElementById("chat-log");
  const btnMic = document.getElementById("btn-mic");
  const micLabel = btnMic ? btnMic.querySelector(".mic-label") : null;
  const btnClearChat = document.getElementById("btn-clear-chat");

  let jarvisRow = null;
  let jarvisTextEl = null;
  let jarvisText = "";
  let sessionActive = false;

  const micStateLabels = {
    Idle: "STANDBY",
    Listening: "LISTENING",
    Thinking: "THINKING",
    Speaking: "SPEAKING",
    ExecutingMcpTool: "MCP",
  };

  function setStatus(connected) {
    if (statusEl) {
      statusEl.textContent = connected ? "CONNECTED" : "DISCONNECTED";
    }
    if (!statusWrap) return;
    statusWrap.classList.toggle("status-connected", connected);
    statusWrap.classList.toggle("status-disconnected", !connected);
  }

  function setMicSession(active) {
    sessionActive = active;
    if (btnMic) {
      btnMic.classList.toggle("mic-active", active);
      btnMic.setAttribute("aria-pressed", active ? "true" : "false");
      btnMic.setAttribute(
        "aria-label",
        active ? "Disabilita microfono" : "Abilita microfono"
      );
    }
    if (!active && micLabel) {
      micLabel.textContent = "IDLE";
    }
  }

  function updateMicLabel(state) {
    if (!micLabel || !sessionActive) return;
    micLabel.textContent = micStateLabels[state] || String(state).toUpperCase();
  }

  function formatTime() {
    const d = new Date();
    return d.toTimeString().slice(0, 8);
  }

  function scrollChatToBottom() {
    if (!chatLog) return;
    chatLog.scrollTop = chatLog.scrollHeight;
  }

  function appendUserLine(text) {
    if (!chatLog) return;
    const row = document.createElement("div");
    row.className = "msg-row msg-user";
    row.innerHTML =
      '<div class="msg-bubble">' +
      '<p class="msg-text">' +
      escapeHtml(text) +
      "</p>" +
      '<time class="msg-time">' +
      formatTime() +
      "</time>" +
      "</div>" +
      '<div class="msg-avatar user-avatar" aria-hidden="true"></div>';
    chatLog.appendChild(row);
    scrollChatToBottom();
  }

  function beginJarvisLine() {
    if (!chatLog) return;
    jarvisText = "";
    jarvisRow = document.createElement("div");
    jarvisRow.className = "msg-row msg-jarvis";
    jarvisRow.innerHTML =
      '<div class="msg-avatar jarvis-avatar" aria-hidden="true"></div>' +
      '<div class="msg-bubble">' +
      '<p class="msg-text"><span class="jarvis-body"></span><span class="typewriter-cursor">|</span></p>' +
      '<time class="msg-time">' +
      formatTime() +
      "</time>" +
      "</div>";
    chatLog.appendChild(jarvisRow);
    jarvisTextEl = jarvisRow.querySelector(".jarvis-body");
  }

  function appendJarvisChunk(chunk) {
    if (!chunk) return;
    if (!jarvisRow) {
      beginJarvisLine();
    }
    jarvisText += chunk;
    if (jarvisTextEl) {
      jarvisTextEl.textContent = jarvisText;
    }
    scrollChatToBottom();
  }

  function finalizeJarvisLine() {
    if (!jarvisRow) return;
    const cursor = jarvisRow.querySelector(".typewriter-cursor");
    if (cursor) {
      cursor.remove();
    }
    jarvisRow = null;
    jarvisTextEl = null;
    jarvisText = "";
  }

  function escapeHtml(s) {
    return s
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub")
    .withAutomaticReconnect()
    .build();

  connection.on("StateChanged", (state) => {
    window.mcpActiveTool = null;
    window.jarvisState = state;
    updateMicLabel(state);
  });

  connection.on("ModulesCatalog", (modules) => {
    if (window.jarvisModuleOrbit) {
      window.jarvisModuleOrbit.setModules(modules);
    }
  });

  connection.on("ModuleToolStarted", (payload) => {
    if (window.jarvisModuleOrbit) {
      window.jarvisModuleOrbit.onToolStarted(payload);
    }
  });

  connection.on("ModuleToolFinished", (payload) => {
    if (window.jarvisModuleOrbit) {
      window.jarvisModuleOrbit.onToolFinished(payload);
    }
  });

  connection.on("McpToolExecuting", (toolName) => {
    window.mcpActiveTool = toolName;
    window.jarvisState = "ExecutingMcpTool";
  });

  connection.on("AudioLevel", (level) => {
    window.audioLevel = level;
  });

  connection.on("UserSpeech", (text) => {
    finalizeJarvisLine();
    appendUserLine(text);
  });

  connection.on("JarvisChunk", (chunk) => {
    if (window.mcpActiveTool) {
      window.mcpActiveTool = null;
    }
    appendJarvisChunk(chunk);
  });

  connection.on("JarvisChunkEnd", () => {
    finalizeJarvisLine();
  });

  connection.onreconnecting(() => setStatus(false));
  connection.onreconnected(() => setStatus(true));
  connection.onclose(() => setStatus(false));

  async function startConnection() {
    try {
      await connection.start();
      setStatus(true);
    } catch (err) {
      console.error(err);
      setStatus(false);
      setTimeout(startConnection, 3000);
    }
  }

  if (btnMic) {
    btnMic.addEventListener("click", async () => {
      const turningOn = !sessionActive;
      const endpoint = turningOn ? "/api/start" : "/api/stop";
      const previousActive = sessionActive;

      setMicSession(turningOn);
      if (turningOn) {
        updateMicLabel(window.jarvisState || "Idle");
      }

      try {
        const res = await fetch(endpoint, { method: "POST" });
        if (!res.ok) {
          setMicSession(previousActive);
          if (previousActive) {
            updateMicLabel(window.jarvisState || "Idle");
          }
          return;
        }
      } catch (e) {
        console.error(e);
        setMicSession(previousActive);
        if (previousActive) {
          updateMicLabel(window.jarvisState || "Idle");
        }
      }
    });
  }

  if (btnClearChat) {
    btnClearChat.addEventListener("click", () => {
      finalizeJarvisLine();
      if (chatLog) {
        chatLog.innerHTML = "";
      }
    });
  }

  startConnection();
})();
