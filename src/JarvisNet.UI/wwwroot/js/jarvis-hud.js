(function () {
  "use strict";

  const canvas = document.getElementById("hud");
  if (!canvas) return;

  const ctx = canvas.getContext("2d");
  let cx = canvas.width / 2;
  let cy = canvas.height / 2;

  function maxDrawRadius(extraMargin) {
    return Math.min(cx, cy) - (extraMargin || 0);
  }

  window.jarvisState = window.jarvisState || "Idle";
  window.audioLevel = window.audioLevel || 0;

  let outerAngle = 0;
  let midAngle = 0;
  let innerAngle = 0;
  let tickAngle = 0;
  let pulsePhase = 0;
  let smoothLevel = 0;
  let waveTravelPhase = 0;

  const WAVE_BANDS = 96;
  const waveBands = new Float32Array(WAVE_BANDS);

  const stateConfig = {
    Idle: { outer: 0.3, mid: 0.5, inner: 0.8, innerDir: 1, glow: 0.6 },
    Listening: { outer: 0.6, mid: 0.8, inner: 1.2, innerDir: 1, glow: 0.85 },
    Thinking: { outer: 0.8, mid: -1.5, inner: -2.5, innerDir: -1, glow: 1.0 },
    Speaking: { outer: 0.5, mid: -0.8, inner: -1.8, innerDir: -1, glow: 1.2 },
  };

  function getConfig() {
    return stateConfig[window.jarvisState] || stateConfig.Idle;
  }

  function updateSpeechWaveform(level) {
    const target = Math.min(1, Math.max(0, level));
    smoothLevel = smoothLevel * 0.78 + target * 0.22;

    if (window.jarvisState !== "Speaking" && target < 0.02) {
      for (let i = 0; i < WAVE_BANDS; i++) {
        waveBands[i] *= 0.92;
      }
      return;
    }

    waveBands.copyWithin(0, 1);
    waveBands[WAVE_BANDS - 1] = smoothLevel;

    for (let i = 1; i < WAVE_BANDS - 1; i++) {
      waveBands[i] = waveBands[i] * 0.94 + (waveBands[i - 1] + waveBands[i + 1]) * 0.03;
    }
  }

  function drawCircularSoundwave(baseRadius, rotation, energy) {
    if (energy < 0.02 && window.jarvisState !== "Speaking") {
      return;
    }

    const points = 180;
    const amp = Math.min(6 + energy * 40, 38);
    const glowBlur = Math.min(10 + energy * 18, 22);
    const cap = maxDrawRadius(glowBlur + 8);
    const layers = [
      { radius: baseRadius, alpha: 0.85, width: 2.2, hue: "#00e5ff" },
      { radius: baseRadius - 10, alpha: 0.55, width: 1.4, hue: "#00b8d4" },
      { radius: baseRadius + 8, alpha: 0.35, width: 1, hue: "#66ffff" },
    ];

    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate((rotation * Math.PI) / 180);
    ctx.shadowColor = "#00e5ff";
    ctx.shadowBlur = glowBlur;

    for (const layer of layers) {
      ctx.strokeStyle = layer.hue;
      ctx.globalAlpha = layer.alpha;
      ctx.lineWidth = layer.width;
      ctx.beginPath();

      for (let i = 0; i <= points; i++) {
        const t = (i / points) * Math.PI * 2;
        const bandIndex = Math.floor((i / points) * WAVE_BANDS) % WAVE_BANDS;
        const band = waveBands[bandIndex] || 0;

        const ripple =
          Math.sin(t * 10 + waveTravelPhase) * amp * 0.35 * (0.4 + band) +
          Math.sin(t * 22 - waveTravelPhase * 1.4) * amp * 0.2 * energy +
          band * amp * 0.85;

        let r = layer.radius + ripple;
        r = Math.min(r, cap);
        r = Math.max(r, 48);
        const x = Math.cos(t) * r;
        const y = Math.sin(t) * r;
        if (i === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
      }

      ctx.closePath();
      ctx.stroke();
    }

    ctx.restore();
  }

  function drawRing(radius, width, rotation, segments, color, alpha, dash) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate((rotation * Math.PI) / 180);
    ctx.strokeStyle = color;
    ctx.globalAlpha = alpha;
    ctx.lineWidth = width;
    ctx.setLineDash(dash || []);
    ctx.beginPath();
    const step = (Math.PI * 2) / segments;
    for (let i = 0; i < segments; i++) {
      const a0 = i * step;
      const a1 = a0 + step * 0.72;
      ctx.arc(0, 0, radius, a0, a1);
    }
    ctx.stroke();
    ctx.restore();
  }

  function drawTicks(rotation, count, radius) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate((rotation * Math.PI) / 180);
    ctx.strokeStyle = "#00e5ff";
    ctx.globalAlpha = 0.45;
    ctx.lineWidth = 1;
    for (let i = 0; i < count; i++) {
      const a = (i / count) * Math.PI * 2;
      const x0 = Math.cos(a) * (radius - 8);
      const y0 = Math.sin(a) * (radius - 8);
      const x1 = Math.cos(a) * radius;
      const y1 = Math.sin(a) * radius;
      ctx.beginPath();
      ctx.moveTo(x0, y0);
      ctx.lineTo(x1, y1);
      ctx.stroke();
    }
    ctx.restore();
  }

  function drawOrangeArc(rotation) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate((rotation * Math.PI) / 180);
    ctx.strokeStyle = "#ff9900";
    ctx.globalAlpha = 0.9;
    ctx.lineWidth = 6;
    ctx.shadowColor = "#ff9900";
    ctx.shadowBlur = 14;
    ctx.beginPath();
    ctx.arc(0, 0, 165, Math.PI * 0.55, Math.PI * 0.85);
    ctx.stroke();
    ctx.restore();
  }

  function drawLabel(glowMul) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.fillStyle = "#e8faff";
    ctx.font = "bold 28px Consolas, monospace";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.shadowColor = "#00e5ff";
    ctx.shadowBlur = 18 * glowMul;
    ctx.fillText("J.A.R.V.I.S.", 0, 0);
    ctx.restore();
  }

  function frame() {
    const cfg = getConfig();
    const level = Math.min(1, Math.max(0, window.audioLevel || 0));
    updateSpeechWaveform(level);
    pulsePhase += 0.04;
    waveTravelPhase += 0.06 + smoothLevel * 0.22;

    const pulse = 1 + Math.sin(pulsePhase) * 0.04 * cfg.glow;
    const levelBoost =
      window.jarvisState === "Listening"
        ? level * 0.15
        : window.jarvisState === "Speaking"
          ? smoothLevel * 0.12
          : 0;

    outerAngle += cfg.outer * (1 + levelBoost);
    midAngle += cfg.mid;
    innerAngle += cfg.inner;
    tickAngle += window.jarvisState === "Thinking" ? 2.2 : 0.4 + smoothLevel * 0.8;

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const hudCap = maxDrawRadius(20 * cfg.glow * pulse);
    const outerR = Math.min(210 * pulse, hudCap);

    ctx.save();
    ctx.shadowColor = "#00e5ff";
    ctx.shadowBlur = Math.min(22 * cfg.glow * pulse, 18);

    drawRing(outerR, 2, outerAngle, 48, "#00e5ff", 0.35, [4, 8]);
    drawTicks(tickAngle, 80, 198);

    if (window.jarvisState === "Speaking" || smoothLevel > 0.03) {
      drawCircularSoundwave(200, midAngle * 0.6, smoothLevel);
    }

    drawRing(185 * (1 + levelBoost), 14, midAngle, 24, "#00c8ff", 0.55, []);
    drawOrangeArc(outerAngle * 0.5);
    drawRing(155, 8, innerAngle, 16, "#00e5ff", 0.75, [12, 6]);
    drawRing(125, 3, -outerAngle * 0.7, 32, "#0088ff", 0.5, []);

    ctx.restore();

    const labelGlow = cfg.glow * pulse;
    if (window.jarvisState === "Thinking") {
      drawLabel(labelGlow * (0.7 + Math.random() * 0.3));
    } else {
      drawLabel(labelGlow + smoothLevel * 0.35);
    }

    requestAnimationFrame(frame);
  }

  requestAnimationFrame(frame);
})();
