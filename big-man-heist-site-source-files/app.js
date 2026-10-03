const difficultyLevels = {
  1: { value: 10000, risk: "EASY PICKINGS", description: "Low-value target with light security and limited resistance.", take: 8000 },
  2: { value: 100000, risk: "LOW RISK", description: "Basic alarms, small security presence, and predictable response patterns.", take: 78000 },
  3: { value: 500000, risk: "RISKY", description: "Multiple security layers with meaningful consequences for mistakes.", take: 390000 },
  4: { value: 1000000, risk: "HIGH RISK", description: "Professional security, hardened access points, and active surveillance.", take: 760000 },
  5: { value: 2500000, risk: "EXTREME", description: "Layered security, hardened vault systems, and rapid-response personnel.", take: 1900000 },
  6: { value: 5000000, risk: "INSANE", description: "Elite security architecture, redundant systems, and very little margin for error.", take: 3700000 },
  7: { value: 10000000, risk: "LEGENDARY", description: "A near-impossible score built to break even experienced crews.", take: 7200000 }
};

const currency = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0
});

const compactCurrency = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  notation: "compact",
  maximumFractionDigits: 1
});

const slider = document.querySelector("#difficulty-slider");
const targetValue = document.querySelector("#target-value");
const riskLabel = document.querySelector("#risk-label");
const riskDescription = document.querySelector("#risk-description");
const difficultyNumber = document.querySelector("#difficulty-number");
const projectedTake = document.querySelector("#projected-take");
const payloadPreview = document.querySelector("#launch-payload");
const launchStatus = document.querySelector("#launch-status");
const heistResults = document.querySelector("#heist-results");
const heistPlayback = document.querySelector("#heist-playback");
const unityCanvas = document.querySelector("#unity-canvas");
const unityIdle = document.querySelector("#unity-idle");
const unityLoading = document.querySelector("#unity-loading");
const unityProgressBar = document.querySelector("#unity-progress-bar");
const unityLoadingLabel = document.querySelector("#unity-loading-label");
const UNITY_BUILD_NAME = "big-man-heist-site-source-files";
const UNITY_BUILD_FOLDERS = ["Build", "unity-build", "unity-build/Build"];
const simStatusLeft = document.querySelector("#sim-status-left");
const simStatusRight = document.querySelector("#sim-status-right");
const unityFullscreen = document.querySelector("#unity-fullscreen");

function updateDifficulty() {
  const level = Number(slider.value);
  const data = difficultyLevels[level];
  targetValue.textContent = currency.format(data.value);
  riskLabel.textContent = data.risk;
  riskDescription.textContent = data.description;
  difficultyNumber.textContent = `${level} / 7`;
  projectedTake.textContent = compactCurrency.format(data.take);
  refreshPayloadPreview();
}

slider.addEventListener("input", updateDifficulty);

const statSliders = [...document.querySelectorAll(".stat-row input[type='range']")];
const pointsRemaining = document.querySelector("#points-remaining");
const attributeBudget = 17;
const form = document.querySelector(".character-form");

function currentAttributeSum() {
  return statSliders.reduce((sum, input) => sum + Number(input.value), 0);
}

function updatePointsRemaining() {
  const remaining = attributeBudget - currentAttributeSum();
  pointsRemaining.textContent = `${remaining} point${remaining === 1 ? "" : "s"} remaining`;
}

for (const statSlider of statSliders) {
  const output = statSlider.parentElement.querySelector("output");
  statSlider.addEventListener("input", () => {
    const otherStatsSum = currentAttributeSum() - Number(statSlider.value);
    const maxAllowed = attributeBudget - otherStatsSum;
    if (Number(statSlider.value) > maxAllowed) {
      statSlider.value = String(Math.max(Number(statSlider.min), maxAllowed));
    }

    output.value = statSlider.value;
    output.textContent = statSlider.value;
    updatePointsRemaining();
    refreshPayloadPreview();
  });
}

updatePointsRemaining();

function readStatsFromForm() {
  const byLabel = {};
  for (const input of statSliders) {
    byLabel[input.getAttribute("aria-label")] = Number(input.value);
  }
  return {
    str: byLabel.Strength || 1,
    agi: byLabel.Agility || 1,
    intel: byLabel.Intelligence || 1,
    dex: byLabel.Dexterity || 1,
    cha: byLabel.Charisma || 1,
    per: byLabel.Perception || 1
  };
}

function classFromStats(stats) {
  const ranked = [
    ["str", stats.str, "Bruiser"],
    ["agi", stats.agi, "Stealth"],
    ["intel", stats.intel, "Hacker"],
    ["dex", stats.dex, "Safecracker"]
  ].sort((a, b) => b[1] - a[1]);
  return ranked[0][2];
}

function draftOperative() {
  const stats = readStatsFromForm();
  const name = (form.codename.value || "Unnamed Operative").trim();
  return {
    id: "organizer",
    name,
    className: classFromStats(stats),
    level: 1,
    isOrganizer: true,
    gear: form.equipment.value,
    stats
  };
}

let savedOperative = null;
const hires = [];
const crewSlots = document.querySelector("#crew-slots");
const crewCount = document.querySelector("#crew-count");
let unityInstance = null;
let heistBusy = false;

function memberFromCard(card) {
  return {
    id: card.dataset.id,
    name: card.dataset.name,
    className: card.dataset.class,
    level: Number(card.dataset.level),
    isOrganizer: false,
    gear: card.dataset.gear,
    stats: {
      str: Number(card.dataset.str),
      agi: Number(card.dataset.agi),
      intel: Number(card.dataset.intel),
      dex: Number(card.dataset.dex),
      cha: Number(card.dataset.cha),
      per: Number(card.dataset.per)
    }
  };
}

function launchCrew() {
  const organizer = savedOperative || draftOperative();
  return [organizer, ...hires].slice(0, 4);
}

function buildLaunchPayload() {
  const difficulty = Number(slider.value);
  const crew = launchCrew();
  return {
    difficulty,
    targetValue: difficultyLevels[difficulty].value,
    seed: Date.now() % 100000,
    organizerId: "organizer",
    crew
  };
}

function refreshPayloadPreview() {
  const payload = buildLaunchPayload();
  const compact = {
    difficulty: payload.difficulty,
    targetValue: payload.targetValue,
    organizer: payload.crew[0]?.name,
    crew: payload.crew.map(member => member.name)
  };
  payloadPreview.textContent = JSON.stringify(compact);
}

function renderCrew() {
  crewSlots.innerHTML = "";
  const organizer = savedOperative || draftOperative();

  for (let i = 0; i < 4; i += 1) {
    const slot = document.createElement("div");
    if (i === 0) {
      slot.className = "crew-member";
      slot.innerHTML = `
        <span>01</span>
        <div><strong>${organizer.name}</strong><small>${organizer.className} · Organizer</small></div>
      `;
    } else {
      const member = hires[i - 1];
      if (member) {
        slot.className = "crew-member";
        slot.innerHTML = `
          <span>0${i + 1}</span>
          <div><strong>${member.name}</strong><small>${member.className} · LVL ${member.level}</small></div>
          <button class="remove-member" type="button" aria-label="Remove ${member.name}" data-index="${i - 1}">×</button>
        `;
      } else {
        slot.className = "crew-empty";
        slot.innerHTML = `<span>0${i + 1}</span><p>Open position</p>`;
      }
    }
    crewSlots.appendChild(slot);
  }

  crewCount.textContent = `${launchCrew().length} / 4`;

  for (const card of document.querySelectorAll(".recruit-card")) {
    const button = card.querySelector(".hire-button");
    const alreadyHired = hires.some(member => member.name === card.dataset.name);
    button.disabled = alreadyHired || hires.length >= 3;
    button.textContent = alreadyHired ? "Hired" : "Hire";
  }

  refreshPayloadPreview();
}

document.querySelectorAll(".hire-button").forEach(button => {
  button.addEventListener("click", () => {
    if (hires.length >= 3) return;
    const card = button.closest(".recruit-card");
    hires.push(memberFromCard(card));
    renderCrew();
  });
});

crewSlots.addEventListener("click", event => {
  const removeButton = event.target.closest(".remove-member");
  if (!removeButton) return;
  hires.splice(Number(removeButton.dataset.index), 1);
  renderCrew();
});

function updatePreviewCard(operative) {
  const card = document.querySelector(".preview-card");
  card.querySelector(".class-label").textContent = operative.className;
  card.querySelector("h3").textContent = operative.name;
  const strengths = [
    ["STR", operative.stats.str],
    ["AGI", operative.stats.agi],
    ["INT", operative.stats.intel],
    ["DEX", operative.stats.dex],
    ["CHA", operative.stats.cha],
    ["PER", operative.stats.per]
  ].sort((a, b) => b[1] - a[1]).slice(0, 3);
  const mini = card.querySelector(".mini-stats");
  mini.innerHTML = strengths.map(([label, value]) => `<div><span>${label}</span><strong>${value}</strong></div>`).join("");
}

document.querySelector("#save-operative").addEventListener("click", () => {
  savedOperative = draftOperative();
  form.querySelector(".status-dot").textContent = "Saved";
  updatePreviewCard(savedOperative);
  setLaunchMessage(`Saved ${savedOperative.name} as the organizing operative.`, false);
  renderCrew();
});

document.querySelector("#publish-operative").addEventListener("click", () => {
  if (!savedOperative) savedOperative = draftOperative();
  form.querySelector(".status-dot").textContent = "Published";
  setLaunchMessage(`${savedOperative.name} is listed on the crew network for this session.`, false);
  renderCrew();
});

function setLaunchMessage(message, isError) {
  launchStatus.hidden = !message;
  launchStatus.textContent = message || "";
  launchStatus.classList.toggle("is-error", Boolean(isError));
}

function setSimStatus(left, right) {
  simStatusLeft.textContent = left;
  simStatusRight.textContent = right;
}

function showPlayback(result) {
  unityCanvas.hidden = true;
  unityIdle.hidden = true;
  unityLoading.hidden = true;
  heistPlayback.hidden = false;
  const lines = result.rooms.flatMap(room => [
    `<h4>${room.name}</h4>`,
    ...room.challenges.map(challenge => `<p class="${challenge.passed ? "ok" : "fail"}">${challenge.narration}</p>`)
  ]);
  heistPlayback.innerHTML = `<div class="playback-log">${lines.join("")}</div>`;
}

function renderResults(result) {
  heistResults.hidden = false;
  const status = result.success ? "SCORE SECURED" : "HEIST COLLAPSED";
  const outcomes = (result.crewOutcomes || []).map(outcome => `
    <li>
      <strong>${outcome.name}${outcome.isOrganizer ? " (Organizer)" : ""}</strong>
      <span>${outcome.status.toUpperCase()} · ${currency.format(outcome.share)}</span>
    </li>
  `).join("");

  heistResults.innerHTML = `
    <div class="result-banner ${result.success ? "is-success" : "is-fail"}">${status}</div>
    <p>Recovered ${currency.format(result.recoveredValue)} · Organizer cut ${currency.format(result.organizerShare)} · Heat ${result.heat}</p>
    <ul>${outcomes}</ul>
  `;
}

window.onHeistComplete = function onHeistComplete(json) {
  heistBusy = false;
  const result = typeof json === "string" ? JSON.parse(json) : json;
  renderResults(result);
  setSimStatus(result.success ? "SIMULATION // COMPLETE" : "SIMULATION // FAILED", "RESULT RECEIVED");
};

async function urlExists(url) {
  try {
    const response = await fetch(url, { method: "GET", cache: "no-store" });
    return response.ok;
  } catch (error) {
    return false;
  }
}

function assetUrls(folder, name) {
  const base = `${folder}/${name}`;
  return [
    {
      loaderUrl: `${base}.loader.js`,
      dataUrl: `${base}.data.br`,
      frameworkUrl: `${base}.framework.js.br`,
      codeUrl: `${base}.wasm.br`
    },
    {
      loaderUrl: `${base}.loader.js`,
      dataUrl: `${base}.data`,
      frameworkUrl: `${base}.framework.js`,
      codeUrl: `${base}.wasm`
    }
  ];
}

async function findUnityLoader() {
  for (const folder of UNITY_BUILD_FOLDERS) {
    for (const candidate of assetUrls(folder, UNITY_BUILD_NAME)) {
      if (await urlExists(candidate.loaderUrl)) {
        return candidate;
      }
    }
  }
  return null;
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    const existing = document.querySelector(`script[src="${src}"]`);
    if (existing) {
      resolve();
      return;
    }
    const script = document.createElement("script");
    script.src = src;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Failed to load ${src}`));
    document.body.appendChild(script);
  });
}

async function launchInUnity(payload) {
  if (window.location.protocol === "file:") {
    setLaunchMessage("Open this site over HTTP (python serve.py) so the WebGL player can load.", true);
    return false;
  }

  const files = await findUnityLoader();
  if (!files) return false;

  heistPlayback.hidden = true;
  unityIdle.hidden = true;
  unityCanvas.hidden = false;
  unityLoading.hidden = false;
  unityProgressBar.style.width = "0%";
  unityLoadingLabel.textContent = "Fetching build…";
  setSimStatus("SIMULATION // ONLINE", "UNITY WEBGL");

  if (!unityInstance) {
    await loadScript(files.loaderUrl);
    if (typeof createUnityInstance !== "function") {
      setLaunchMessage("Unity loader script did not expose createUnityInstance.", true);
      return false;
    }

    unityInstance = await createUnityInstance(unityCanvas, {
      dataUrl: files.dataUrl,
      frameworkUrl: files.frameworkUrl,
      codeUrl: files.codeUrl,
      streamingAssetsUrl: "StreamingAssets",
      companyName: "DefaultCompany",
      productName: "Heist Society",
      productVersion: "0.1.0"
    }, progress => {
      unityProgressBar.style.width = `${Math.round(progress * 100)}%`;
      unityLoadingLabel.textContent = `Loading ${Math.round(progress * 100)}%`;
    });
  }

  unityLoading.hidden = true;
  unityFullscreen.disabled = false;
  await new Promise(resolve => setTimeout(resolve, 250));
  unityInstance.SendMessage("HeistBootstrap", "StartHeist", JSON.stringify(payload));
  setLaunchMessage("Unity player mounted. Running heist…", false);
  return true;
}

async function launchHeist() {
  if (heistBusy) return;
  const payload = buildLaunchPayload();
  if (!payload.crew.length) {
    setLaunchMessage("Save an operative before launching.", true);
    return;
  }

  heistBusy = true;
  document.querySelector("#simulation").scrollIntoView({ behavior: "smooth" });
  setLaunchMessage("Launching heist...", false);
  setSimStatus("SIMULATION // LIVE", "RUNNING");

  try {
    const usedUnity = await launchInUnity(payload);
    if (usedUnity) return;

    setSimStatus("SIMULATION // BROWSER FALLBACK", "UNITY BUILD MISSING");
    const result = window.HeistSim.simulate(payload);
    showPlayback(result);
    window.onHeistComplete(result);
    setLaunchMessage("Ran the heist with the browser fallback. Drop a WebGL build into unity-build/ to use Unity.", false);
  } catch (error) {
    heistBusy = false;
    setLaunchMessage(error.message, true);
    setSimStatus("SIMULATION // ERROR", "LAUNCH FAILED");
  }
}

document.querySelector("#launch-heist").addEventListener("click", launchHeist);

const unityBay = document.querySelector("#unity-bay");

function isPlayerFullscreen() {
  return document.fullscreenElement === unityBay || document.webkitFullscreenElement === unityBay;
}

function updateFullscreenLabel() {
  unityFullscreen.textContent = isPlayerFullscreen() ? "Exit Full Screen" : "Full Screen";
}

async function toggleUnityFullscreen() {
  if (unityFullscreen.disabled) return;

  if (isPlayerFullscreen()) {
    if (unityInstance && typeof unityInstance.SetFullscreen === "function") {
      unityInstance.SetFullscreen(0);
    }
    if (document.exitFullscreen) await document.exitFullscreen();
    else if (document.webkitExitFullscreen) document.webkitExitFullscreen();
    return;
  }

  try {
    if (unityBay.requestFullscreen) await unityBay.requestFullscreen();
    else if (unityBay.webkitRequestFullscreen) unityBay.webkitRequestFullscreen();
  } catch (error) {
    setLaunchMessage(error.message || "Fullscreen was blocked by the browser.", true);
  }
}

unityFullscreen.addEventListener("click", toggleUnityFullscreen);
unityCanvas.addEventListener("dblclick", toggleUnityFullscreen);
document.addEventListener("fullscreenchange", updateFullscreenLabel);
document.addEventListener("webkitfullscreenchange", updateFullscreenLabel);

form.codename.addEventListener("input", renderCrew);
form.equipment.addEventListener("change", refreshPayloadPreview);

updateDifficulty();
renderCrew();
