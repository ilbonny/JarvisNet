(function () {
  "use strict";

  const statusEl = document.getElementById("connection-status");
  const chatLog = document.getElementById("chat-log");
  const btnStart = document.getElementById("btn-start");
  const btnStop = document.getElementById("btn-stop");

  let jarvisLine = null;
  let jarvisText = "";

  function setStatus(connected) {
    if (!statusEl) return;
    statusEl.textContent = connected ? "CONNECTED" : "DISCONNECTED";
    statusEl.classList.toggle("status-connected", connected);
    statusEl.classList.toggle("status-disconnected", !connected);
  }

  function appendMcpLogLine(text) {
    if (!chatLog) return;
    const line = document.createElement("div");
    line.className = "chat-line mcp-log-line";
    line.textContent = text;
    chatLog.appendChild(line);
    chatLog.scrollTop = chatLog.scrollHeight;
  }

  function appendUserLine(text) {
    if (!chatLog) return;
    const line = document.createElement("div");
    line.className = "chat-line";
    line.innerHTML =
      '<span class="prefix-user">USER:</span> "' +
      escapeHtml(text) +
      '"';
    chatLog.appendChild(line);
    chatLog.scrollTop = chatLog.scrollHeight;
  }

  function beginJarvisLine() {
    if (!chatLog) return;
    jarvisText = "";
    jarvisLine = document.createElement("div");
    jarvisLine.className = "chat-line";
    jarvisLine.innerHTML =
      '<span class="prefix-jarvis">JARVIS:</span> "<span class="jarvis-body"></span><span class="typewriter-cursor">|</span>"';
    chatLog.appendChild(jarvisLine);
  }

  function appendJarvisChunk(chunk) {
    if (!chunk) return;
    if (!jarvisLine) {
      beginJarvisLine();
    }
    jarvisText += chunk;
    const body = jarvisLine.querySelector(".jarvis-body");
    if (body) {
      body.textContent = jarvisText;
    }
    chatLog.scrollTop = chatLog.scrollHeight;
  }

  function finalizeJarvisLine() {
    if (!jarvisLine) return;
    const cursor = jarvisLine.querySelector(".typewriter-cursor");
    if (cursor) {
      cursor.remove();
    }
    jarvisLine = null;
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
  });

  connection.on("McpToolExecuting", (toolName) => {
    window.mcpActiveTool = toolName;
    window.jarvisState = "ExecutingMcpTool";
    appendMcpLogLine("[MCP Executing: " + toolName + "]");
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

  if (btnStart) {
    btnStart.addEventListener("click", async () => {
      try {
        await fetch("/api/start", { method: "POST" });
      } catch (e) {
        console.error(e);
      }
    });
  }

  if (btnStop) {
    btnStop.addEventListener("click", async () => {
      try {
        await fetch("/api/stop", { method: "POST" });
      } catch (e) {
        console.error(e);
      }
    });
  }

  startConnection();
})();
