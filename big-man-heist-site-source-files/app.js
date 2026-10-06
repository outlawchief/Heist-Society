const nativeFetch = window.fetch.bind(window);

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
const UNITY_BUILD_NAMES = ["unity-build", "big-man-heist-site-source-files"];
const UNITY_BUILD_FOLDERS = ["unity-build/Build", "Build", "unity-build"];
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
const attributeBudget = 26;
const form = document.querySelector(".character-form");
const backgroundEffect = document.querySelector("#background-effect");
const equipmentEffect = document.querySelector("#equipment-effect");
const previewBackground = document.querySelector("#preview-background");
const previewGear = document.querySelector("#preview-gear");

const BACKGROUNDS = {
  "Career Criminal": {
    blurb: "Raised on jobs, not classrooms.",
    mods: { dex: 1, cha: 1, intel: -1 }
  },
  "Security Consultant": {
    blurb: "Used to walk the floor with a badge and a clipboard.",
    mods: { per: 1, intel: 1, str: -1 }
  },
  "Former Enforcer": {
    blurb: "Collections, doors, and people who owed the wrong crew.",
    mods: { str: 1, cha: 1, dex: -1 }
  },
  "Systems Engineer": {
    blurb: "Trusts racks and cables more than faces.",
    mods: { intel: 1, dex: 1, cha: -1 }
  },
  "Ex-Cop": {
    blurb: "Knows the beat, the radios, and every excuse a cop uses.",
    mods: { per: 1, str: 1, cha: -1 }
  },
  "Con Artist": {
    blurb: "A smile, a story, and a name that never sticks.",
    mods: { cha: 1, per: 1, str: -1 }
  },
  "Second-Story": {
    blurb: "Windows, ledges, and quiet feet above the street.",
    mods: { agi: 1, dex: 1, str: -1 }
  },
  "Wheelman": {
    blurb: "Gets the crew in, out, and off the map.",
    mods: { agi: 1, per: 1, intel: -1 }
  },
  "Spec Ops Washout": {
    blurb: "Trained for raids. Left before the medals.",
    mods: { str: 1, agi: 1, cha: -1 }
  },
  "Inside Man": {
    blurb: "Worked the building. Still has the keys in their head.",
    mods: { intel: 1, cha: 1, agi: -1 }
  }
};

const EQUIPMENT = {
  "Lockpick Set": {
    blurb: "Tiny tools for stubborn tumblers and quiet vault work.",
    mods: { dex: 1, str: -1 },
    checks: "DEX / vault"
  },
  "Signal Jammer": {
    blurb: "Kills radios, cameras, and anything that beeps at the wrong time.",
    mods: { intel: 1, cha: -1 },
    checks: "INT"
  },
  "Breaching Kit": {
    blurb: "Rams, charges, and the shortest path through a locked door.",
    mods: { str: 1, dex: -1 },
    checks: "STR"
  },
  "Disguise Kit": {
    blurb: "A badge, a name tag, and a face that belongs here.",
    mods: { cha: 1, per: -1 },
    checks: "CHA"
  },
  "Climbing Harness": {
    blurb: "Hooks, line, and a way onto the roof nobody locked.",
    mods: { agi: 1, str: -1 },
    checks: "AGI"
  },
  "Earpiece": {
    blurb: "Hears patrols, radios, and the click before a door opens.",
    mods: { per: 1, intel: -1 },
    checks: "PER"
  },
  "EMP Charge": {
    blurb: "One pocket nuke for lasers, panels, and cheap electronics.",
    mods: { intel: 1, agi: -1 },
    checks: "INT / lasers"
  },
  "Forged Papers": {
    blurb: "The right badge for the wrong person.",
    mods: { cha: 1, str: -1 },
    checks: "CHA"
  },
  "Silent Shoes": {
    blurb: "Soft soles. No echo. No second chance to hear you.",
    mods: { agi: 1, cha: -1 },
    checks: "AGI"
  },
  "Sledge Key": {
    blurb: "If the lock will not turn, the frame will.",
    mods: { str: 1, per: -1 },
    checks: "STR"
  }
};

function selectedBackground() {
  return BACKGROUNDS[form.background.value] || BACKGROUNDS["Career Criminal"];
}

function selectedGear() {
  return EQUIPMENT[form.equipment.value] || EQUIPMENT["Lockpick Set"];
}

function formatBackgroundMods(mods) {
  return Object.entries(mods || {})
    .filter(([, value]) => value)
    .map(([stat, value]) => `${value > 0 ? "+" : ""}${value} ${stat.toUpperCase()}`)
    .join("  ·  ");
}

function combinedMods() {
  const mods = { str: 0, agi: 0, intel: 0, dex: 0, cha: 0, per: 0 };
  for (const source of [selectedBackground(), selectedGear()]) {
    for (const [key, value] of Object.entries(source.mods || {})) {
      mods[key] = (mods[key] || 0) + value;
    }
  }
  return mods;
}

function applyBackground(base) {
  const mods = combinedMods();
  const stats = {};
  for (const key of ["str", "agi", "intel", "dex", "cha", "per"]) {
    stats[key] = Math.max(1, Math.min(10, (Number(base[key]) || 1) + (mods[key] || 0)));
  }
  return stats;
}

function refreshBackgroundUi() {
  const bg = selectedBackground();
  const gear = selectedGear();
  if (backgroundEffect) {
    backgroundEffect.textContent = `${bg.blurb}  ${formatBackgroundMods(bg.mods)}`;
  }
  if (equipmentEffect) {
    equipmentEffect.textContent = `${gear.blurb}  ${formatBackgroundMods(gear.mods)}  ·  checks ${gear.checks}`;
  }
  const mods = combinedMods();
  for (const el of document.querySelectorAll(".stat-mod")) {
    const delta = mods[el.dataset.stat] || 0;
    el.textContent = delta ? `${delta > 0 ? "+" : ""}${delta}` : "";
    el.classList.toggle("is-down", delta < 0);
  }
  const effective = applyBackground(readStatsFromForm());
  const map = {
    Strength: "str",
    Agility: "agi",
    Intelligence: "intel",
    Dexterity: "dex",
    Charisma: "cha",
    Perception: "per"
  };
  for (const statSlider of statSliders) {
    const output = statSlider.parentElement.querySelector("output");
    const key = map[statSlider.getAttribute("aria-label")];
    output.value = String(effective[key]);
    output.textContent = String(effective[key]);
  }
}

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
    refreshBackgroundUi();
    updatePointsRemaining();
    updateArchetypePreview();
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

const specialistArchetypes = {
  str: "Bruiser",
  agi: "Ghost",
  intel: "Hacker",
  dex: "Safecracker",
  cha: "Face",
  per: "Lookout"
};

const comboArchetypes = {
  "agi+str": "Enforcer",
  "agi+intel": "Phantom",
  "agi+dex": "Cat Burglar",
  "agi+cha": "Grifter",
  "agi+per": "Scout",
  "intel+str": "Saboteur",
  "dex+str": "Wrecker",
  "cha+str": "Intimidator",
  "per+str": "Pointman",
  "dex+intel": "Technician",
  "cha+intel": "Social Engineer",
  "intel+per": "Analyst",
  "cha+dex": "Flimflam",
  "dex+per": "Locksmith",
  "cha+per": "Handler"
};

const classBlurbs = {
  Bruiser: "Close-quarters specialist built to break resistance and force a way through security.",
  Ghost: "Infiltration specialist built to move unseen and slip past detection.",
  Hacker: "Systems specialist built to crack networks and shut down electronic security.",
  Safecracker: "Precision specialist built to defeat mechanical locks and recover high-value targets.",
  Face: "Social specialist built to talk past checkpoints and keep the crew's cover intact.",
  Lookout: "Awareness specialist built to read a room, mark cameras, and call danger early.",
  Enforcer: "Agility and muscle in one package: hits hard, then vanishes before the response lands.",
  Phantom: "Silent systems work. Cracks the network without ever appearing on a camera.",
  "Cat Burglar": "Second-story work. Climbs, slips, and opens what was never meant to be opened.",
  Grifter: "A charming shadow. Walks in looking like they belong, then walks out with the take.",
  Scout: "The first one through. Maps patrols, finds vents, and keeps the crew a step ahead.",
  Saboteur: "Brute-force electronics. Kicks in the door and kills the alarm in the same breath.",
  Wrecker: "Strength with a lockpick. Forces entries that finesse alone cannot finish.",
  Intimidator: "The loud option. Muscle and presence that makes guards pick a different fight.",
  Pointman: "Leads the stack. Spots the threat, then puts a body in front of it.",
  Technician: "Hands and head. Wires, tumblers, and panels all yield to the same calm method.",
  "Social Engineer": "Hacks people as cleanly as machines. A badge, a story, a bypassed door.",
  Analyst: "Sees the whole floor. Cameras, schedules, and weak points before anyone else does.",
  Flimflam: "Light fingers and a lighter story. Distracts, dips, and leaves no name behind.",
  Locksmith: "Eyes and hands on the vault. Notices the trap, then opens the box anyway.",
  Handler: "Reads the room and works the crowd. The crew's cover, contacts, and exit story.",
  "All-Rounder": "No single specialty. Flexible enough to fill whatever hole the plan leaves open.",
  Stealth: "Infiltration specialist built to move unseen and slip past detection."
};

function classFromStats(stats) {
  const ranked = ["str", "agi", "intel", "dex", "cha", "per"]
    .map(key => [key, Number(stats[key]) || 1])
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  const [top, second] = ranked;
  const tiedForLead = ranked.filter(row => row[1] === top[1]).length;
  if (tiedForLead >= 3) return "All-Rounder";
  if (top[0] === "per" && tiedForLead === 1) return "Lookout";
  if (top[1] >= second[1] + 2) return specialistArchetypes[top[0]];
  const pair = [top[0], second[0]].sort().join("+");
  return comboArchetypes[pair] || specialistArchetypes[top[0]];
}

function updateArchetypePreview() {
  const operative = draftOperative();
  updatePreviewCard(operative);
  const card = document.querySelector(".preview-card");
  const blurb = card.querySelector(".preview-blurb") || card.querySelector("p");
  blurb.textContent = classBlurbs[operative.className] || classBlurbs["All-Rounder"];
  if (previewBackground) {
    const bg = selectedBackground();
    previewBackground.textContent = `${operative.background}: ${formatBackgroundMods(bg.mods)}`;
  }
  if (previewGear) {
    const gear = selectedGear();
    previewGear.textContent = `${operative.gear}: ${formatBackgroundMods(gear.mods)}`;
  }
}

function operativeNameFromForm() {
  return form.codename.value.trim() || "Unnamed Operative";
}

function updateNamePreview() {
  document.querySelector(".preview-card h3").textContent = operativeNameFromForm();
}

function draftOperative() {
  const stats = applyBackground(readStatsFromForm());
  const name = operativeNameFromForm();
  return {
    id: "organizer",
    name,
    className: classFromStats(stats),
    level: 1,
    isOrganizer: true,
    gear: form.equipment.value,
    background: form.background.value,
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
    origin: window.location.origin,
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
  refreshJoinCrewOptions();
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
  ].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 3);
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
    const head = await nativeFetch(url, { method: "HEAD", cache: "no-store" });
    if (head.ok) return true;
  } catch (error) {
    /* some servers skip HEAD */
  }
  try {
    const response = await nativeFetch(url, {
      method: "GET",
      cache: "no-store",
      headers: { Range: "bytes=0-0" }
    });
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
      dataUrl: `${base}.data`,
      frameworkUrl: `${base}.framework.js`,
      codeUrl: `${base}.wasm`
    },
    {
      loaderUrl: `${base}.loader.js`,
      dataUrl: `${base}.data.br`,
      frameworkUrl: `${base}.framework.js.br`,
      codeUrl: `${base}.wasm.br`
    }
  ];
}

function streamingAssetsUrl(folder) {
  const root = folder.replace(/\/Build$/, "");
  return `${root}/StreamingAssets`;
}

async function findUnityLoader() {
  for (const folder of UNITY_BUILD_FOLDERS) {
    for (const name of UNITY_BUILD_NAMES) {
      for (const candidate of assetUrls(folder, name)) {
        if (
          await urlExists(candidate.loaderUrl) &&
          await urlExists(candidate.dataUrl) &&
          await urlExists(candidate.frameworkUrl) &&
          await urlExists(candidate.codeUrl)
        ) {
          candidate.streamingAssetsUrl = streamingAssetsUrl(folder);
          return candidate;
        }
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

function isLanRelayHost() {
  const host = window.location.hostname;
  if (host === "localhost" || host === "127.0.0.1" || host === "[::1]" || host === "::1") return true;
  if (/^10\.\d+\.\d+\.\d+$/.test(host)) return true;
  if (/^192\.168\.\d+\.\d+$/.test(host)) return true;
  const match = host.match(/^172\.(\d+)\.\d+\.\d+$/);
  return Boolean(match && Number(match[1]) >= 16 && Number(match[1]) <= 31);
}

function makeJoinCode() {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 4; i += 1) {
    code += chars[Math.floor(Math.random() * chars.length)];
  }
  return code;
}

function coopRelayOk(response) {
  if (!response || response.status < 200 || response.status >= 300) return false;
  const relay = response.headers.get("X-Heist-Relay");
  if (relay === "serve.py") return true;
  if (response.status === 204) return true;
  const type = response.headers.get("Content-Type") || "";
  return type.includes("json");
}

async function registerLanSession(payload) {
  const code = String(payload?.joinCode || "").trim().toUpperCase();
  if (!code) return true;
  payload.joinCode = code;
  window.__heistLaunchPayload = payload;
  const body = JSON.stringify(payload);
  const url = `${window.location.origin}/coop/${code}/launch`;
  const headers = { "Content-Type": "application/json" };

  const attempts = [
    () => nativeFetch(url, { method: "POST", headers, body }),
    () => nativeFetch(url, { method: "PUT", headers, body }),
    () => nativeFetch(`${url}?body=${encodeURIComponent(body)}`)
  ];

  let lastStatus = 0;
  for (const send of attempts) {
    try {
      const response = await send();
      lastStatus = response.status;
      if (coopRelayOk(response)) return true;
      if (lastStatus !== 405 && lastStatus !== 501 && lastStatus !== 404) break;
    } catch (error) {
      lastStatus = 0;
    }
  }

  console.warn(`LAN session ${code} not registered (HTTP ${lastStatus}).`);
  return false;
}

async function launchInUnity(payload, method = "StartHeist") {
  if (window.location.protocol === "file:") {
    setLaunchMessage("Open this site over HTTP (python serve.py) so the WebGL player can load.", true);
    return false;
  }

  const files = await findUnityLoader();
  if (!files) {
    setLaunchMessage("No complete WebGL build found. Export to unity-build/Build (loader, data, framework, wasm).", true);
    return false;
  }

  heistPlayback.hidden = true;
  unityIdle.hidden = true;
  unityCanvas.hidden = false;
  unityLoading.hidden = false;
  unityProgressBar.style.width = "0%";
  unityLoadingLabel.textContent = "Fetching build…";
  setSimStatus("SIMULATION // ONLINE", "UNITY WEBGL");

  if (!unityInstance) {
    try {
      setLaunchMessage(`Loading ${files.dataUrl}…`, false);
      await loadScript(files.loaderUrl);
      if (typeof createUnityInstance !== "function") {
        setLaunchMessage("Unity loader script did not expose createUnityInstance.", true);
        return false;
      }

      unityInstance = await createUnityInstance(unityCanvas, {
        dataUrl: files.dataUrl,
        frameworkUrl: files.frameworkUrl,
        codeUrl: files.codeUrl,
        streamingAssetsUrl: files.streamingAssetsUrl || "StreamingAssets",
        companyName: "DefaultCompany",
        productName: "Heist Society",
        productVersion: "0.1.0",
        showBanner: (msg, type) => setLaunchMessage(msg, type === "error")
      }, progress => {
        const pct = Math.round(progress * 100);
        unityProgressBar.style.width = `${pct}%`;
        unityLoadingLabel.textContent = pct === 0
          ? "Downloading build files…"
          : `Loading ${pct}%`;
      });
    } catch (error) {
      unityLoading.hidden = true;
      setLaunchMessage(error.message || "Unity WebGL failed to load. Hard-refresh after a new export.", true);
      setSimStatus("SIMULATION // ERROR", "WEBGL LOAD FAILED");
      return false;
    }
  }

  unityLoading.hidden = true;
  unityFullscreen.disabled = false;
  await new Promise(resolve => setTimeout(resolve, 250));
  if (method === "StartHeist" && !window.__heistLaunchPayload) window.__heistLaunchPayload = payload;
  unityInstance.SendMessage("HeistBootstrap", method, JSON.stringify(payload));
  setLaunchMessage("Unity player mounted. Running heist…", false);
  return true;
}

window.onHeistJoinCode = function onHeistJoinCode(code) {
  const joinCode = String(code || "").trim().toUpperCase();
  const line = document.querySelector("#join-code-display");
  if (joinCode) {
    line.hidden = false;
    line.textContent = `Join code: ${joinCode}`;
    document.querySelector("#join-code").value = joinCode;
    setSimStatus("SIMULATION // LIVE", `CODE ${joinCode}`);
  }
  if (!isLanRelayHost()) {
    setLaunchMessage(joinCode
      ? `Heist running. Use join code ${joinCode} in the Unity editor (Photon).`
      : "Heist running.", false);
    return;
  }
  const launch = {
    ...(window.__heistLaunchPayload || buildLaunchPayload()),
    joinCode
  };
  if (launch.joinCode) {
    registerLanSession(launch).then(ok => {
      if (ok) {
        setLaunchMessage(`LAN session ${launch.joinCode} registered. Editor: same code, origin ${window.location.origin}`, false);
        showLanShare(launch.joinCode);
      } else {
        setLaunchMessage(`Heist is running. LAN join is unavailable on this host.`, false);
      }
    });
  }
};

async function showLanShare(code) {
  const line = document.querySelector("#join-code-display");
  try {
    const response = await nativeFetch("/lan");
    if (!response.ok) return;
    const data = await response.json();
    if (data.url) {
      line.textContent = `Co-op join code: ${code}   Teammate URL: ${data.url}`;
    }
  } catch (error) {
    /* local file or missing /lan */
  }
}
function refreshJoinCrewOptions(crew = launchCrew()) {
  const select = document.querySelector("#join-crew");
  if (!select) return;
  const previous = select.value;
  const members = Array.isArray(crew) ? crew : [];
  select.innerHTML = members.map(member =>
    `<option value="${member.id}">${member.name}</option>`
  ).join("");
  if (members.some(member => member.id === previous)) {
    select.value = previous;
    return;
  }
  const guest = members.find(member => member.id !== "organizer") || members[1];
  if (guest) select.value = guest.id;
}

async function joinHeistSession() {
  const code = document.querySelector("#join-code").value.trim().toUpperCase();
  if (!code) {
    setLaunchMessage("Enter a join code from the host.", true);
    return;
  }
  try {
    const response = await nativeFetch(`/coop/${code}/launch`);
    if (!response.ok) throw new Error("No session found for that code. Host must launch first, and you must open this site from the host's URL or the host laptop's LAN URL instead of a second copy of the project.");
    const launch = JSON.parse(await response.text());
    if (!launch.crew || !launch.crew.length) {
      throw new Error("That session has no crew yet. Wait a second after the host launches, then try again.");
    }
    refreshJoinCrewOptions(launch.crew);
    launch.joinCode = code;
    launch.possessId = document.querySelector("#join-crew").value;
    launch.origin = window.location.origin;
    document.querySelector("#simulation").scrollIntoView({ behavior: "smooth" });
    const usedUnity = await launchInUnity(launch, "JoinHeist");
    if (!usedUnity) setLaunchMessage("Unity WebGL is required to join a live heist.", true);
    else setLaunchMessage(`Joined ${code}. Pick a different crewmate than the host.`, false);
  } catch (error) {
    setLaunchMessage(error.message, true);
  }
}

document.querySelector("#join-heist").addEventListener("click", joinHeistSession);

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
    payload.origin = window.location.origin;
    payload.joinCode = makeJoinCode();
    window.__heistLaunchPayload = payload;
    if (isLanRelayHost()) {
      await registerLanSession(payload);
    }
    window.onHeistJoinCode(payload.joinCode);
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

form.codename.addEventListener("input", () => {
  updateArchetypePreview();
  renderCrew();
});
form.equipment.addEventListener("change", () => {
  refreshBackgroundUi();
  updateArchetypePreview();
  refreshPayloadPreview();
});
form.background.addEventListener("change", () => {
  refreshBackgroundUi();
  updateArchetypePreview();
  refreshPayloadPreview();
});

updateDifficulty();
updateArchetypePreview();
updateNamePreview();
refreshBackgroundUi();
renderCrew();
const AUTH_STORAGE_KEY = "heist-auth-session";
const API_BASE_URL = "http://localhost:5000";

const AUTH_LOGIN_URL = `${API_BASE_URL}/api/auth/login`;
const AUTH_REGISTER_URL = `${API_BASE_URL}/api/auth/register`;
const AUTH_LOGOUT_URL = `${API_BASE_URL}/api/auth/logout`;
const loginButton = document.querySelector("#operative-login");
const signupButton = document.querySelector("#operative-signup");
const logoutButton = document.querySelector("#operative-logout");
const authSession = document.querySelector("#auth-session");
const authCallsign = document.querySelector("#auth-callsign");
const loginScreen = document.querySelector("#login-screen");
const loginForm = document.querySelector("#login-form");
const loginStatus = document.querySelector("#login-status");
const signupScreen = document.querySelector("#signup-screen");
const signupForm = document.querySelector("#signup-form");
const signupStatus = document.querySelector("#signup-status");

function readAuthSession() {
  try {
    const raw = sessionStorage.getItem(AUTH_STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch (error) {
    return null;
  }
}

function writeAuthSession(session) {
  if (session) sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
  else sessionStorage.removeItem(AUTH_STORAGE_KEY);
}

function renderAuthControls() {
  const session = readAuthSession();
  const loggedIn = Boolean(session?.token);
  loginButton.hidden = loggedIn;
  signupButton.hidden = loggedIn;
  authSession.hidden = !loggedIn;
  authCallsign.textContent = loggedIn ? session.codename : "";
}

function setLoginMessage(message, isError) {
  loginStatus.textContent = message || "";
  loginStatus.classList.toggle("is-error", Boolean(isError));
}

function setLoginBusy(isBusy) {
  const submit = loginForm.querySelector("[type='submit']");
  loginForm.codename.disabled = isBusy;
  loginForm.password.disabled = isBusy;
  submit.disabled = isBusy;
  submit.textContent = isBusy ? "Connecting…" : "Sign In";
}

function closeLogin() {
  if (loginScreen.open) loginScreen.close();
  setLoginBusy(false);
  setLoginMessage("");
  loginForm.password.value = "";
}

function sessionFromLoginPayload(payload, codename) {
  const token = payload?.token || payload?.accessToken;
  if (!token) return null;
  const name = payload?.operative?.username || payload?.operative?.codename || payload?.username || payload?.codename || payload?.name || codename;
  return { token, codename: String(name) };
}

function authError(response, payload, fallback) {
  const message = payload?.message || payload?.error;
  if (response.status === 404 || response.status === 501 || response.status === 502 || response.status === 503) {
    return new Error(message || "The operations backend is not online yet.");
  }
  return new Error(message || fallback);
}

async function requestLogin(codename, password) {
  let response;
  try {
    response = await fetch(AUTH_LOGIN_URL, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ codename, password })
    });
  } catch (error) {
    throw new Error("Could not reach the operations backend. The sign-in request was sent, but the server is not online yet.");
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    if (response.status === 401 || response.status === 403) {
      throw new Error(payload?.message || payload?.error || "Access denied. Check the codename and password.");
    }
    throw authError(response, payload, `The operations backend rejected the login (${response.status}).`);
  }

  const session = sessionFromLoginPayload(payload, codename);
  if (!session) throw new Error("The operations backend did not return a session token.");
  return session;
}

async function requestLogout(session) {
  try {
    await fetch(AUTH_LOGOUT_URL, {
      method: "POST",
      headers: {
        Accept: "application/json",
        Authorization: `Bearer ${session.token}`
      }
    });
  } catch (error) {
    // Clearing the local session still signs the operative out.
  }
}

function setSignupMessage(message, isError) {
  signupStatus.textContent = message || "";
  signupStatus.classList.toggle("is-error", Boolean(isError));
}

function setSignupBusy(isBusy) {
  const submit = signupForm.querySelector("[type='submit']");
  signupForm.username.disabled = isBusy;
  signupForm.email.disabled = isBusy;
  signupForm.password.disabled = isBusy;
  submit.disabled = isBusy;
  submit.textContent = isBusy ? "Registering…" : "Create Account";
}

function closeSignup() {
  if (signupScreen.open) signupScreen.close();
  setSignupBusy(false);
  setSignupMessage("");
  signupForm.password.value = "";
}

function openLogin() {
  if (signupScreen.open) closeSignup();
  setLoginMessage("");
  loginScreen.showModal();
  loginForm.codename.focus();
}

function openSignup() {
  if (loginScreen.open) closeLogin();
  setSignupMessage("");
  signupScreen.showModal();
  signupForm.username.focus();
}

async function requestRegister(username, email, password) {
  let response;
  try {
    response = await fetch(AUTH_REGISTER_URL, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ username, email, password })
    });
  } catch (error) {
    throw new Error("Could not reach the operations backend. The registration request was sent, but the server is not online yet.");
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(payload?.message || payload?.error || "That username or email is already registered.");
    }
    if (response.status === 400) {
      throw new Error(payload?.message || payload?.error || "Check the username, email, and password.");
    }
    throw authError(response, payload, `The operations backend rejected the registration (${response.status}).`);
  }

  return {
    session: sessionFromLoginPayload(payload, username),
    username
  };
}

loginButton.addEventListener("click", openLogin);
signupButton.addEventListener("click", openSignup);
document.querySelector("#open-signup").addEventListener("click", openSignup);
document.querySelector("#open-login").addEventListener("click", openLogin);

document.querySelector("#login-close").addEventListener("click", closeLogin);
document.querySelector("#login-cancel").addEventListener("click", closeLogin);

loginScreen.addEventListener("click", event => {
  if (event.target === loginScreen) closeLogin();
});

loginScreen.addEventListener("close", () => {
  setLoginBusy(false);
  loginForm.password.value = "";
});

loginForm.addEventListener("submit", async event => {
  event.preventDefault();
  const codename = loginForm.codename.value.trim();
  const password = loginForm.password.value;
  if (!codename || !password) {
    setLoginMessage("Codename and password are required.", true);
    return;
  }

  setLoginBusy(true);
  setLoginMessage("Contacting the operations backend…", false);
  try {
    const session = await requestLogin(codename, password);
    writeAuthSession(session);
    renderAuthControls();
    closeLogin();
  } catch (error) {
    setLoginMessage(error.message, true);
    setLoginBusy(false);
  }
});

document.querySelector("#signup-close").addEventListener("click", closeSignup);
document.querySelector("#signup-cancel").addEventListener("click", closeSignup);

signupScreen.addEventListener("click", event => {
  if (event.target === signupScreen) closeSignup();
});

signupScreen.addEventListener("close", () => {
  setSignupBusy(false);
  signupForm.password.value = "";
});

signupForm.addEventListener("submit", async event => {
  event.preventDefault();
  const username = signupForm.username.value.trim();
  const email = signupForm.email.value.trim();
  const password = signupForm.password.value;
  if (!username || !email || !password) {
    setSignupMessage("Username, email, and password are required.", true);
    return;
  }
  if (password.length < 8) {
    setSignupMessage("Password must be at least 8 characters.", true);
    return;
  }

  setSignupBusy(true);
  setSignupMessage("Contacting the operations backend…", false);
  try {
    const result = await requestRegister(username, email, password);
    if (result.session) {
      writeAuthSession(result.session);
      renderAuthControls();
      closeSignup();
      return;
    }
    closeSignup();
    loginForm.codename.value = result.username;
    loginScreen.showModal();
    setLoginMessage("Account created. Sign in with your username.", false);
  } catch (error) {
    setSignupMessage(error.message, true);
    setSignupBusy(false);
  }
});

logoutButton.addEventListener("click", async () => {
  const session = readAuthSession();
  logoutButton.disabled = true;
  if (session) await requestLogout(session);
  writeAuthSession(null);
  logoutButton.disabled = false;
  renderAuthControls();
});

renderAuthControls();
