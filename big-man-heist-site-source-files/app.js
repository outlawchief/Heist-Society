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

function updateDifficulty() {
  const level = Number(slider.value);
  const data = difficultyLevels[level];
  targetValue.textContent = currency.format(data.value);
  riskLabel.textContent = data.risk;
  riskDescription.textContent = data.description;
  difficultyNumber.textContent = `${level} / 7`;
  projectedTake.textContent = compactCurrency.format(data.take);
}

slider.addEventListener("input", updateDifficulty);
updateDifficulty();

for (const statSlider of document.querySelectorAll(".stat-row input[type='range']")) {
  const output = statSlider.parentElement.querySelector("output");
  statSlider.addEventListener("input", () => {
    output.value = statSlider.value;
    output.textContent = statSlider.value;
  });
}

const crew = [];
const crewSlots = document.querySelector("#crew-slots");
const crewCount = document.querySelector("#crew-count");

function renderCrew() {
  crewSlots.innerHTML = "";

  for (let i = 0; i < 4; i += 1) {
    const member = crew[i];
    const slot = document.createElement("div");

    if (member) {
      slot.className = "crew-member";
      slot.innerHTML = `
        <span>0${i + 1}</span>
        <div><strong>${member.name}</strong><small>${member.className} · LVL ${member.level}</small></div>
        <button class="remove-member" type="button" aria-label="Remove ${member.name}" data-index="${i}">×</button>
      `;
    } else {
      slot.className = "crew-empty";
      slot.innerHTML = `<span>0${i + 1}</span><p>Open position</p>`;
    }

    crewSlots.appendChild(slot);
  }

  crewCount.textContent = `${crew.length} / 4`;

  for (const card of document.querySelectorAll(".recruit-card")) {
    const button = card.querySelector(".hire-button");
    const alreadyHired = crew.some(member => member.name === card.dataset.name);
    button.disabled = alreadyHired || crew.length >= 4;
    button.textContent = alreadyHired ? "Hired" : "Hire";
  }
}

document.querySelectorAll(".hire-button").forEach(button => {
  button.addEventListener("click", () => {
    if (crew.length >= 4) return;
    const card = button.closest(".recruit-card");
    crew.push({
      name: card.dataset.name,
      className: card.dataset.class,
      level: card.dataset.level
    });
    renderCrew();
  });
});

crewSlots.addEventListener("click", event => {
  const removeButton = event.target.closest(".remove-member");
  if (!removeButton) return;
  crew.splice(Number(removeButton.dataset.index), 1);
  renderCrew();
});

renderCrew();
