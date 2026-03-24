const BODY_PART_NAMES = {
  head: 'Head',
  torso: 'Torso',
  arm1: 'Arm 1',
  arm2: 'Arm 2',
  legs: 'Legs'
};

function getBodyPartState(hp, maxHp) {
  if (hp <= 0) return 'Destroyed';
  if (hp / maxHp < 0.3) return 'Crippled';
  if (hp / maxHp < 0.6) return 'Wounded';
  return null;
}

const BODY_PART_PENALTIES = {
  head: {
    Wounded:   { accuracy: -1, evasion: -1, speed: -1 },
    Crippled:  { accuracy: -2, evasion: -3, speed: -2, power: -1, resilience: -2 },
    Destroyed: { accuracy: -3, evasion: -5, speed: -3, power: -2, resilience: -4 }
  },
  torso: {
    Wounded:   { resilience: -1, power: -1, constitution: -2 },
    Crippled:  { resilience: -2, power: -2, constitution: -3 },
    Destroyed: { resilience: -3, power: -3, constitution: -4 }
  },
  arm1: {
    Wounded:   { power: -1, accuracy: -1, speed: -1 },
    Crippled:  { power: -2, accuracy: -2, speed: -1 },
    Destroyed: { power: -3, accuracy: -3, speed: -1 }
  },
  arm2: {
    Wounded:   { power: -1, accuracy: -1, speed: -1 },
    Crippled:  { power: -2, accuracy: -2, speed: -1 },
    Destroyed: { power: -3, accuracy: -3, speed: -1 }
  },
  legs: {
    Wounded:   { speed: -2, evasion: -2, power: -1 },
    Crippled:  { speed: -4, evasion: -4, power: -2 },
    Destroyed: { speed: -5, evasion: -5 }
  }
};

const INJURY_DESCRIPTIONS = {
  head:  { Wounded: 'Dizziness', Crippled: 'Concussion', Destroyed: 'Incapacitated' },
  torso: { Wounded: 'Contused', Crippled: 'Internal Damage', Destroyed: 'Critical Blood Loss' },
  arm1:  { Wounded: 'Weakened Grip', Crippled: 'Broken', Destroyed: 'Removed Arm' },
  arm2:  { Wounded: 'Weakened Grip', Crippled: 'Broken', Destroyed: 'Removed Arm' },
  legs:  { Wounded: 'Limping', Crippled: 'Immobile', Destroyed: 'Severely Immobile' }
};

function generateStats() {
  const names = ['power', 'accuracy', 'constitution', 'evasion', 'speed', 'resilience'];
  const stats = {};
  names.forEach(n => stats[n] = Math.floor(Math.random() * 10) + 1);
  return stats;
}

class Character {
  constructor(name, team) {
    this.name = name;
    this.team = team;
    this.stats = generateStats();
    this.speed = this.stats.speed;
    const hpBonus = (this.stats.constitution - 1) * 2;
    this.maxHP = 10 + hpBonus;
    this.hp = this.maxHP;
    this.maxStamina = 3 + Math.floor(this.stats.resilience / 4);
    this.stamina = this.maxStamina;
    this.isDead = false;
    const bp = (pct) => { const v = Math.round(this.maxHP * pct); return { hp: v, maxHp: v }; };
    this.bodyParts = {
      head:  bp(0.50),
      torso: bp(1.00),
      arm1:  bp(0.60),
      arm2:  bp(0.60),
      legs:  bp(0.80)
    };
  }

  getEffectiveStats() {
    const eff = { ...this.stats };
    for (const [key, part] of Object.entries(this.bodyParts)) {
      const state = getBodyPartState(part.hp, part.maxHp);
      const penalties = state && BODY_PART_PENALTIES[key]?.[state];
      if (penalties) {
        for (const [stat, val] of Object.entries(penalties)) {
          eff[stat] = Math.max(1, (eff[stat] || 0) + val);
        }
      }
    }
    return eff;
  }

  getActiveInjuries() {
    const injuries = [];
    for (const [key, part] of Object.entries(this.bodyParts)) {
      const state = getBodyPartState(part.hp, part.maxHp);
      const desc = state && INJURY_DESCRIPTIONS[key]?.[state];
      if (desc) injuries.push(desc);
    }
    return injuries;
  }

  takeDamage(amount, bodyPartKey = 'whole') {
    if (bodyPartKey === 'whole') {
      this.hp = Math.max(0, this.hp - amount);
      if (this.hp === 0) this.isDead = true;
      return { stateChanges: [] };
    } else {
      const part = this.bodyParts[bodyPartKey];
      const prevState = getBodyPartState(part.hp, part.maxHp);
      part.hp = Math.max(0, part.hp - amount);
      const newState = getBodyPartState(part.hp, part.maxHp);
      const stateChanges = [];
      if (prevState !== newState && newState !== null) {
        stateChanges.push({ bodyPartKey, newState });
      }
      return { stateChanges };
    }
  }
}

function resolveAttack(attacker, defender) {
  const atkEff = attacker.getEffectiveStats();
  const defEff = defender.getEffectiveStats();
  const hitChance = Math.min(0.95, 0.70 + atkEff.accuracy * 0.025);
  if (Math.random() > hitChance) return 'miss';
  const dodgeChance = Math.min(0.40, defEff.evasion * 0.025);
  if (Math.random() < dodgeChance) return 'dodge';
  return 'hit';
}

function calcDamage(attacker, base) {
  return base + Math.floor(attacker.getEffectiveStats().power / 2);
}

function createTeam(name) {
  return [1, 2, 3, 4].map(idx => new Character(`${name} ${idx}`, name));
}

let allCharacters = [...createTeam('Player'), ...createTeam('Enemy')];
let turnOrder = [...allCharacters].sort((a, b) => b.speed - a.speed);
let currentTurnIndex = 0;
let round = 1;
let turnsTakenThisRound = 0;
let gameState = 'selectAction';
let attackType = null;
let selectedTarget = null;
let combatLog = [];

function getCurrentCharacter() {
  return turnOrder[currentTurnIndex];
}

function addLog(message) {
  combatLog.push(message);
}

function initiateBasicAttack() {
  if (getCurrentCharacter().stamina < 1) return;
  attackType = 'basic';
  gameState = 'selectTarget';
  render();
}

function initiateTargetedAttack() {
  if (getCurrentCharacter().stamina < 3) return;
  attackType = 'targeted';
  gameState = 'selectTarget';
  render();
}

function selectTarget(target) {
  selectedTarget = target;
  if (attackType === 'basic') {
    performBasicAttack(target);
  } else {
    gameState = 'selectBodyPart';
    render();
  }
}

function selectBodyPart(bodyPart) {
  performTargetedAttack(selectedTarget, bodyPart);
}

function cancelAttack() {
  gameState = 'selectAction';
  attackType = null;
  selectedTarget = null;
  render();
}

function performBasicAttack(target) {
  const attacker = getCurrentCharacter();
  attacker.stamina -= 1;

  const outcome = resolveAttack(attacker, target);

  if (outcome === 'miss') {
    addLog(`${attacker.name}'s Basic Attack on ${target.name} missed!`);
  } else if (outcome === 'dodge') {
    addLog(`${target.name} dodged ${attacker.name}'s Basic Attack!`);
  } else {
    const damage = calcDamage(attacker, 5);
    target.takeDamage(damage, 'whole');
    let logMessage = `${attacker.name} uses Basic Attack on ${target.name} for ${damage} damage`;
    if (target.isDead) logMessage += ` <span class="log-kill">[DEFEATED]</span>`;
    addLog(logMessage);
  }

  afterAction();
}

function performTargetedAttack(target, bodyPartKey) {
  const attacker = getCurrentCharacter();
  attacker.stamina -= 3;

  const outcome = resolveAttack(attacker, target);
  const partName = BODY_PART_NAMES[bodyPartKey];

  if (outcome === 'miss') {
    addLog(`${attacker.name}'s Targeted Attack on ${target.name}'s ${partName} missed!`);
  } else if (outcome === 'dodge') {
    addLog(`${target.name} dodged ${attacker.name}'s Targeted Attack!`);
  } else {
    const bodyPartDamage = calcDamage(attacker, 5);
    const overallDamage = Math.ceil(bodyPartDamage / 2);
    const result = target.takeDamage(bodyPartDamage, bodyPartKey);
    target.hp = Math.max(0, target.hp - overallDamage);
    if (target.hp === 0) target.isDead = true;
    let logMessage = `${attacker.name} attacks ${target.name}'s ${partName} for ${bodyPartDamage} dmg (${overallDamage} overall)`;
    if (target.isDead) logMessage += ` <span class="log-kill">[DEFEATED]</span>`;
    addLog(logMessage);
    for (const change of result.stateChanges) {
      addLog(`<span class="log-injury">${target.name}'s ${BODY_PART_NAMES[change.bodyPartKey]} is now ${change.newState}!</span>`);
    }
  }

  afterAction();
}

function afterAction() {
  gameState = 'selectAction';
  attackType = null;
  selectedTarget = null;

  for (const c of allCharacters) {
    if (!c.isDead) {
      const effCON = c.getEffectiveStats().constitution;
      const effMaxHP = Math.max(1, 10 + (effCON - 1) * 2);
      if (c.hp > effMaxHP) {
        c.hp = effMaxHP;
        if (c.hp <= 0) { c.hp = 0; c.isDead = true; }
      }
    }
  }

  const playerAlive = turnOrder.some(c => c.team === 'Player' && !c.isDead);
  const enemyAlive = turnOrder.some(c => c.team === 'Enemy' && !c.isDead);
  if (!playerAlive || !enemyAlive) { endGame(); return; }

  render();
}

function endTurn() {
  const playerAlive = turnOrder.some(c => c.team === 'Player' && !c.isDead);
  const enemyAlive = turnOrder.some(c => c.team === 'Enemy' && !c.isDead);
  if (!playerAlive || !enemyAlive) { endGame(); return; }
  nextPlayer();
}

function nextPlayer() {
  let attempts = 0;
  do {
    currentTurnIndex = (currentTurnIndex + 1) % turnOrder.length;
    attempts++;
  } while (turnOrder[currentTurnIndex].isDead && attempts < turnOrder.length);

  const next = turnOrder[currentTurnIndex];
  const recovery = 1 + Math.floor(next.getEffectiveStats().resilience / 4);
  next.stamina = Math.min(next.maxStamina, next.stamina + recovery);

  turnsTakenThisRound++;
  if (turnsTakenThisRound >= turnOrder.filter(c => !c.isDead).length) {
    round++;
    turnsTakenThisRound = 0;
  }

  gameState = 'selectAction';
  render();
}

function endGame() {
  const playerAlive = turnOrder.filter(c => c.team === 'Player' && !c.isDead).length > 0;
  const enemyAlive = turnOrder.filter(c => c.team === 'Enemy' && !c.isDead).length > 0;

  let winner = playerAlive && !enemyAlive ? 'PLAYER VICTORY!' : 'DEFEAT - Enemies Victorious';

  document.getElementById('game-over-screen').innerHTML = `
    <div class="game-over">
      <h2>${winner}</h2>
      <p>Combat has ended.</p>
      <button onclick="location.reload()">Start Over</button>
    </div>
  `;
  document.getElementById('current-turn-info').innerHTML = '';
}

function renderPlayerCard(character) {
  const isActiveTurn = character === getCurrentCharacter() && !character.isDead ? 'active' : '';
  const isDead = character.isDead ? 'dead' : '';
  const teamClass = character.team === 'Player' ? 'player-team' : 'enemy-team';
  const current = getCurrentCharacter();
  const isSelectable = gameState === 'selectTarget' && character.team !== current.team && !character.isDead ? 'selectable' : '';

  const bodyPartsHTML = `<div class="body-parts-section"><div class="body-parts-title">Body Parts</div><div class="body-parts-grid">${Object.entries(character.bodyParts).map(([key, data]) => {
    const state = getBodyPartState(data.hp, data.maxHp);
    const hpPct = Math.max(0, (data.hp / data.maxHp) * 100).toFixed(1);
    return `<div class="body-part-row"><div class="body-part-name">${BODY_PART_NAMES[key]}</div><div class="body-part-hp-bar"><div class="body-part-hp-fill" style="width:${hpPct}%"></div><div class="body-part-hp-text">${data.hp}/${data.maxHp}</div></div><div class="body-part-status">${state || ''}</div></div>`;
  }).join('')}</div></div>`;

  const s = character.stats;
  const eff = character.getEffectiveStats();
  const effMaxHP = Math.max(1, 10 + (eff.constitution - 1) * 2);
  const hpPct = Math.max(0, (character.hp / effMaxHP) * 100).toFixed(1);
  const pipsHTML = Array.from({length: character.maxStamina}, (_, i) =>
    `<div class="stamina-pip${i < character.stamina ? '' : ' empty'}"></div>`
  ).join('');
  const hpBarHTML = `<div class="hp-stamina-wrapper"><div class="hp-bar-container"><div class="hp-bar-fill" style="width:${hpPct}%"></div><div class="hp-bar-text">${character.hp}/${effMaxHP}</div></div><div class="stamina-pips">${pipsHTML}</div></div>`;
  const statVal = (key, label) => {
    const reduced = eff[key] < s[key];
    return `<div class="stat-abbr-item">${label} <b${reduced ? ' style="color:#CC0000"' : ''}>${eff[key]}</b></div>`;
  };
  const abbrHTML = `<div class="stat-abbr-grid">${statVal('power','POW')}${statVal('accuracy','ACC')}${statVal('constitution','CON')}${statVal('evasion','EVA')}${statVal('speed','SPD')}${statVal('resilience','RES')}</div>`;

  const injuries = character.getActiveInjuries();
  const injuryHTML = injuries.length > 0
    ? `<div class="card-injuries">${injuries.join(' · ')}</div>`
    : '';
  return `<div class="player-card ${isActiveTurn} ${isDead} ${teamClass} ${isSelectable}" ${isSelectable ? `onclick="selectTarget(window.allCharacters.find(c => c.name === '${character.name}'))"` : ''}><div class="player-name">${character.name}</div>${injuryHTML}${hpBarHTML}<div class="player-stats">${abbrHTML}</div>${character.isDead ? '<div class="dead-label">DEAD</div>' : bodyPartsHTML}</div>`;
}

function renderTurnOrder() {
  return turnOrder.map((c, idx) => {
    let classes = 'turn-item';
    if (c.team === 'Player') classes += ' player-team';
    if (c.team === 'Enemy') classes += ' enemy-team';
    if (idx === currentTurnIndex && !c.isDead) classes += ' current';
    if (idx < currentTurnIndex) classes += ' done';
    if (c.isDead) classes += ' dead';
    const teamPrefix = c.team === 'Player' ? 'P' : 'E';
    const number = c.name.match(/\d+/)?.[0] || '';
    const shortLabel = `${teamPrefix}${number}`;
    return `<div class="${classes}"><span>${shortLabel}</span></div>`;
  }).join('');
}

function renderCurrentTurnInfo() {
  const current = getCurrentCharacter();

  if (current.isDead) {
    return `<div class="current-player-name">${current.name}</div><div class="current-player-turn">This character is defeated.</div><div class="button-group"><button onclick="nextPlayer()">Next Player</button></div>`;
  }

  if (gameState === 'selectAction') {
    const canBasic = current.stamina >= 1;
    const canTargeted = current.stamina >= 3;
    const basicDmg = calcDamage(current, 5);
    const targetedPartDmg = calcDamage(current, 5);
    const targetedOverallDmg = Math.ceil(targetedPartDmg / 2);
    return `<div class="current-player-name">${current.name}'s Turn</div><div class="current-player-turn">${current.team} Team • ${current.stamina}/${current.maxStamina} Stamina</div><div class="attack-options"><div class="attack-card ${canBasic ? '' : 'disabled'}" onclick="initiateBasicAttack()"><div class="attack-title">Basic Attack</div><div class="attack-detail">1 Stamina</div><div class="attack-detail">${basicDmg} dmg</div></div><div class="attack-card ${canTargeted ? '' : 'disabled'}" onclick="initiateTargetedAttack()"><div class="attack-title">Targeted Attack</div><div class="attack-detail">3 Stamina</div><div class="attack-detail">${targetedPartDmg} dmg to part</div><div class="attack-detail">${targetedOverallDmg} dmg overall</div></div></div><div class="button-group" style="margin-top:1rem;"><button onclick="endTurn()">End Turn</button></div>`;
  } else if (gameState === 'selectTarget') {
    const opposingTeam = current.team === 'Player' ? 'Enemy' : 'Player';
    return `<div class="current-player-name">Select Target</div><div class="action-prompt">Click on a ${opposingTeam} to attack them</div><div class="button-group"><button onclick="cancelAttack()">Cancel</button></div>`;
  } else if (gameState === 'selectBodyPart') {
    return `<div class="current-player-name">Select Body Part</div><div class="action-prompt">Choose which body part to attack</div><div class="body-part-selection"><button class="body-part-button" onclick="selectBodyPart('head')">Head</button><button class="body-part-button" onclick="selectBodyPart('torso')">Torso</button><button class="body-part-button" onclick="selectBodyPart('arm1')">Arm 1</button><button class="body-part-button" onclick="selectBodyPart('arm2')">Arm 2</button><button class="body-part-button" onclick="selectBodyPart('legs')">Legs</button></div><div class="button-group" style="margin-top: 1rem;"><button onclick="cancelAttack()">Cancel</button></div>`;
  }
}

function render() {
  document.getElementById('round-display').textContent = `Round ${round}`;
  const playerTeam = allCharacters.filter(c => c.team === 'Player');
  const enemyTeam = allCharacters.filter(c => c.team === 'Enemy');
  document.getElementById('player-team').innerHTML = playerTeam.map(renderPlayerCard).join('');
  document.getElementById('enemy-team').innerHTML = enemyTeam.map(renderPlayerCard).join('');
  document.getElementById('turn-order').innerHTML = renderTurnOrder();
  document.getElementById('current-turn-info').innerHTML = renderCurrentTurnInfo();
  const logHtml = combatLog.slice(-5).map(entry => `<div class="log-entry">${entry}</div>`).join('');
  document.getElementById('combat-log').innerHTML = logHtml;
}

window.allCharacters = allCharacters;
render();
