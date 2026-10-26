(function () {
  "use strict";

  var languageSelect = document.getElementById("audio-language-select");
  var voiceSelect = document.getElementById("audio-voice-select");
  var statusEl = document.getElementById("voice-panel-status");
  if (!languageSelect || !voiceSelect) return;

  var suppressChange = false;
  var allVoices = [];
  var currentLanguage = "";
  var currentVoice = "";

  function setStatus(text) {
    if (statusEl) statusEl.textContent = text;
  }

  function formatVoiceLabel(v) {
    var gender =
      v.gender === "male" ? "M" : v.gender === "female" ? "F" : v.gender || "";
    var parts = [v.name || v.key];
    if (gender) parts.push(gender);
    if (v.quality) parts.push(v.quality);
    return parts.join(" · ");
  }

  function voicesForLanguage(languageId) {
    return allVoices.filter(function (v) {
      return v.installed && v.locale === languageId;
    });
  }

  function refillVoiceSelect(languageId, preferredKey) {
    suppressChange = true;
    voiceSelect.innerHTML = "";
    var list = voicesForLanguage(languageId);
    if (list.length === 0) {
      var empty = document.createElement("option");
      empty.value = "";
      empty.textContent = "Nessuna voce";
      voiceSelect.appendChild(empty);
      voiceSelect.disabled = true;
      suppressChange = false;
      return;
    }

    list.forEach(function (v) {
      var opt = document.createElement("option");
      opt.value = v.key;
      opt.textContent = formatVoiceLabel(v);
      voiceSelect.appendChild(opt);
    });

    var pick =
      preferredKey && list.some(function (v) { return v.key === preferredKey; })
        ? preferredKey
        : list[0].key;
    voiceSelect.value = pick;
    voiceSelect.disabled = false;
    currentVoice = pick;
    suppressChange = false;
  }

  function applySettings(data) {
    suppressChange = true;
    allVoices = data.voices || [];
    currentLanguage = data.currentLanguage || "";
    currentVoice = data.currentVoice || "";

    languageSelect.innerHTML = "";
    var languages = (data.languages || []).filter(function (l) {
      return l.installed;
    });
    if (languages.length === 0) {
      var emptyLang = document.createElement("option");
      emptyLang.value = "";
      emptyLang.textContent = "N/D";
      languageSelect.appendChild(emptyLang);
      languageSelect.disabled = true;
      voiceSelect.disabled = true;
      setStatus("Unavailable");
      suppressChange = false;
      return;
    }

    languages.forEach(function (l) {
      var opt = document.createElement("option");
      opt.value = l.id;
      opt.textContent = l.label;
      languageSelect.appendChild(opt);
    });

    languageSelect.disabled = false;
    if (!languages.some(function (l) { return l.id === currentLanguage; })) {
      currentLanguage = languages[0].id;
    }
    languageSelect.value = currentLanguage;
    suppressChange = false;

    refillVoiceSelect(currentLanguage, currentVoice);
    setStatus("Ready");
  }

  async function loadSettings() {
    setStatus("Loading");
    languageSelect.disabled = true;
    voiceSelect.disabled = true;
    try {
      var res = await fetch("/api/audio/settings");
      if (!res.ok) {
        setStatus("Error");
        return;
      }
      var data = await res.json();
      applySettings(data);
    } catch (e) {
      console.error(e);
      setStatus("Error");
    }
  }

  languageSelect.addEventListener("change", async function () {
    if (suppressChange) return;
    var nextLang = languageSelect.value;
    if (!nextLang || nextLang === currentLanguage) return;

    var prevLang = currentLanguage;
    var prevVoice = currentVoice;
    languageSelect.disabled = true;
    voiceSelect.disabled = true;
    setStatus("Switching");

    try {
      var res = await fetch("/api/audio/language", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ languageId: nextLang }),
      });
      if (!res.ok) {
        var err = await res.json().catch(function () { return {}; });
        throw new Error(err.error || "Language change failed");
      }
      var settingsRes = await fetch("/api/audio/settings");
      if (settingsRes.ok) {
        applySettings(await settingsRes.json());
      } else {
        currentLanguage = nextLang;
        refillVoiceSelect(nextLang, "");
      }
      setStatus("Ready");
    } catch (e) {
      console.error(e);
      suppressChange = true;
      languageSelect.value = prevLang;
      currentLanguage = prevLang;
      refillVoiceSelect(prevLang, prevVoice);
      suppressChange = false;
      setStatus("Error");
    } finally {
      languageSelect.disabled = false;
    }
  });

  voiceSelect.addEventListener("change", async function () {
    if (suppressChange) return;
    var nextVoice = voiceSelect.value;
    if (!nextVoice || nextVoice === currentVoice) return;

    var prevVoice = currentVoice;
    voiceSelect.disabled = true;
    setStatus("Switching");

    try {
      var res = await fetch("/api/tts/voice", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ voiceKey: nextVoice }),
      });
      if (!res.ok) {
        var err = await res.json().catch(function () { return {}; });
        throw new Error(err.error || "Voice change failed");
      }
      currentVoice = nextVoice;
      setStatus("Ready");
    } catch (e) {
      console.error(e);
      suppressChange = true;
      voiceSelect.value = prevVoice;
      suppressChange = false;
      setStatus("Error");
    } finally {
      voiceSelect.disabled = false;
    }
  });

  loadSettings();
})();
