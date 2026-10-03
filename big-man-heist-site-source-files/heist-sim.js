(function (root) {
  const ROOM_NAMES = [
    "Service Alley", "Lobby", "Security Wing", "Archives",
    "Executive Floor", "Vault Approach", "Cash Room", "Inner Vault"
  ];

  const CATALOG = [
    ["Forced Door", "door", "str"],
    ["Vent Crawl", "door", "agi"],
    ["Laser Grid", "door", "agi"],
    ["Camera Grid", "cameras", "intel"],
    ["Alarm Panel", "cameras", "intel"],
    ["Guard Desk", "social", "cha"],
    ["Bluff the Patrol", "social", "cha"],
    ["Vault Lock", "vault", "dex"],
    ["Hidden Passage", "bypass", "per"]
  ];

  const ORGANIZER_CUT = 0.25;
  const CONSOLATION_RATE = 0.02;
  const HEAT_PER_FAILURE = 2;
  const CAPTURE_BASE = 0.12;
  const CAPTURE_PER_HEAT = 0.07;
  const KILL_CHANCE = 0.22;

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function createRng(seed) {
    let t = seed >>> 0;
    return function next() {
      t += 0x6D2B79F5;
      let r = Math.imul(t ^ (t >>> 15), 1 | t);
      r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
      return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
    };
  }

  function nextInt(rng, minInclusive, maxExclusive) {
    return minInclusive + Math.floor(rng() * (maxExclusive - minInclusive));
  }

  function getStat(stats, skill) {
    const value = stats?.[skill];
    return Number.isFinite(value) ? value : 1;
  }

  function gearBonus(gear, challenge) {
    if (!gear || !challenge) return 0;
    if (gear === "Lockpick Set" && (challenge.skill === "dex" || challenge.type === "vault")) return 1;
    if (gear === "Signal Jammer" && challenge.skill === "intel") return 1;
    if (gear === "Breaching Kit" && challenge.skill === "str") return 1;
    if (gear === "Disguise Kit" && challenge.skill === "cha") return 1;
    return 0;
  }

  function makeChallenge(rng, difficulty, vault, bypass) {
    let pick;
    if (vault) pick = CATALOG[7];
    else if (bypass) pick = CATALOG[8];
    else pick = CATALOG[nextInt(rng, 0, 7)];

    const minT = clamp(3 + difficulty, 3, 10);
    const maxT = clamp(4 + difficulty, minT, 10);
    return {
      name: pick[0],
      type: pick[1],
      skill: pick[2],
      threshold: nextInt(rng, minT, maxT + 1),
      isBypass: pick[1] === "bypass",
      skipped: false,
      passed: false,
      actorId: "",
      actorName: "",
      roll: 0,
      bonus: 0,
      narration: ""
    };
  }

  function generate(difficulty, seed) {
    const rng = createRng(seed || 1);
    const roomCount = clamp(difficulty + 1, 2, 8);
    const rooms = [];

    for (let i = 0; i < roomCount; i += 1) {
      const room = { name: ROOM_NAMES[i], challenges: [] };
      let extra = 0;
      if (difficulty >= 3 && rng() < 0.4 + difficulty * 0.05) extra += 1;
      if (difficulty >= 6 && rng() < 0.35) extra += 1;
      const count = 1 + extra;
      const lastRoom = i === roomCount - 1;

      for (let c = 0; c < count; c += 1) {
        const vault = lastRoom && c === count - 1;
        const bypass = !vault && c === 0 && !lastRoom && rng() < 0.28;
        room.challenges.push(makeChallenge(rng, difficulty, vault, bypass));
      }
      rooms.push(room);
    }

    return rooms;
  }

  function resolveChallenge(challenge, crew, rng) {
    if (!crew.length) {
      challenge.passed = false;
      challenge.narration = "No crew was sent. The challenge stands unanswered.";
      return;
    }

    let best = crew[0];
    let bestScore = -1;
    for (const member of crew) {
      const score = getStat(member.stats, challenge.skill) + gearBonus(member.gear, challenge);
      if (score > bestScore) {
        bestScore = score;
        best = member;
      }
    }

    const bonus = gearBonus(best.gear, challenge);
    const roll = nextInt(rng, 1, 7);
    const total = getStat(best.stats, challenge.skill) + bonus + roll;
    challenge.actorId = best.id;
    challenge.actorName = best.name;
    challenge.roll = roll;
    challenge.bonus = bonus;
    challenge.passed = total >= challenge.threshold;

    const skillLabel = challenge.skill.toUpperCase();
    if (challenge.passed) {
      challenge.narration = challenge.isBypass
        ? `${best.name} spots a hidden route (${skillLabel} ${getStat(best.stats, challenge.skill)}+${roll}).`
        : `${best.name} clears ${challenge.name} (${skillLabel} ${getStat(best.stats, challenge.skill)}+${roll} vs ${challenge.threshold}).`;
    } else {
      challenge.narration = `${best.name} fails ${challenge.name} (${skillLabel} ${getStat(best.stats, challenge.skill)}+${roll} vs ${challenge.threshold}). Heat rises.`;
    }
  }

  function resolve(launch, rooms) {
    const rng = createRng((launch.seed || 1) + 91);
    const crew = launch.crew || [];
    let heat = 0;
    const heatCap = launch.difficulty + 4;
    let challengeCount = 0;
    let failedCount = 0;
    let vaultFailed = false;

    for (const room of rooms) {
      let skipNext = false;
      for (const challenge of room.challenges) {
        challengeCount += 1;
        if (skipNext) {
          challenge.skipped = true;
          challenge.passed = true;
          challenge.narration = "Bypassed via the hidden path.";
          skipNext = false;
          continue;
        }

        resolveChallenge(challenge, crew, rng);
        if (!challenge.passed) {
          failedCount += 1;
          heat += HEAT_PER_FAILURE;
          if (challenge.type === "vault") vaultFailed = true;
        } else if (challenge.isBypass) {
          skipNext = true;
          heat = Math.max(0, heat - 1);
        }
      }
    }

    const success = !vaultFailed && heat < heatCap;
    const failRatio = challengeCount === 0 ? 0 : failedCount / challengeCount;
    let recovered = 0;
    if (success) {
      recovered = Math.round(launch.targetValue * 0.76 * (1 - 0.12 * failRatio));
    }

    const consolation = Math.max(1, Math.round(launch.targetValue * CONSOLATION_RATE));
    const organizerShare = success ? Math.round(recovered * ORGANIZER_CUT) : consolation;
    if (!success) recovered = organizerShare;

    const statuses = {};
    for (const member of crew) {
      let status = "ok";
      if (!success) {
        const captureChance = CAPTURE_BASE + heat * CAPTURE_PER_HEAT;
        if (rng() < captureChance) status = "captured";
        if (heat >= heatCap && rng() < KILL_CHANCE) status = "killed";
      }
      statuses[member.id] = status;
    }

    const remainder = success ? Math.max(0, recovered - organizerShare) : 0;
    const survivors = crew.filter(member => statuses[member.id] === "ok");
    const split = survivors.length === 0 ? 0 : Math.floor(remainder / survivors.length);

    const crewOutcomes = crew.map(member => {
      let share = statuses[member.id] === "ok" ? split : 0;
      if (member.isOrganizer) share += organizerShare;
      return {
        id: member.id,
        name: member.name,
        isOrganizer: member.isOrganizer,
        status: statuses[member.id],
        share
      };
    });

    return {
      success,
      heat,
      targetValue: launch.targetValue,
      recoveredValue: recovered,
      organizerShare,
      crewOutcomes,
      rooms
    };
  }

  function simulate(launch) {
    const payload = {
      ...launch,
      difficulty: clamp(Number(launch.difficulty) || 1, 1, 7),
      seed: launch.seed || Math.abs((launch.targetValue + launch.difficulty * 17) | 1)
    };
    const rooms = generate(payload.difficulty, payload.seed);
    return resolve(payload, rooms);
  }

  root.HeistSim = { simulate, generate, resolve };
})(window);
