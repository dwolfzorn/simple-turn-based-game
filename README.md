# Simple Turn Based Game — Game Overview

## Structure
The game is a turn-based combat game played between two teams of four characters each: **Player Team** and **Enemy Team**. Combat plays out in rounds, with each character acting in turn order determined by their Speed stat.

---

## Characters

Each character is generated with the following:

- **Name** — assigned by team and index (e.g. Player 1, Enemy 3)
- **6 randomised stats** — each rolled between 1 and 10
- **HP** — derived from Constitution
- **Stamina** — derived from Resilience
- **5 body parts** — each with HP proportional to the character's total HP

---

## Stats
| Stat | Abbr | Effect |
|---|---|---|
| Power | POW | Increases damage dealt by attacks |
| Accuracy | ACC | Increases hit chance |
| Constitution | CON | Increases total HP pool |
| Evasion | EVA | Increases chance to dodge incoming attacks |
| Speed | SPD | Determines turn order |
| Resilience | RES | Increases max stamina and stamina recovery per turn |

### Derived Values
- **Max HP** = 10 + (CON − 1) × 2
- **Max Stamina** = 3 + floor(RES / 4)
- **Stamina recovery per turn** = 1 + floor(RES / 4)

---

## Turn Order

Characters are sorted by Speed at the start of combat, highest first. Within each turn, a character may take as many actions as their stamina allows. The player must manually click **End Turn** to pass to the next character.

---

## Actions

### Basic Attack
- **Cost:** 1 Stamina
- Deals `5 + floor(POW / 2)` damage to the target's overall HP.

### Targeted Attack
- **Cost:** 2 Stamina
- Choose a target, then select a specific body part to attack.
- Deals `5 + floor(POW / 2)` damage to the chosen body part.
- Also deals `ceil(bodyPartDamage / 2)` damage to overall HP.

---

## Hit & Dodge Resolution

Every attack is resolved in two steps before damage is applied:

1. **Hit roll** — chance to hit = min(95%, 70% + ACC × 2.5%). If failed, the attack **misses**.
2. **Dodge roll** — chance to dodge = min(40%, EVA × 2.5%). If succeeded, the attack is **dodged**.

Both attacker's ACC and defender's EVA use **effective stats** (i.e. after injury penalties are applied).

---

## Body Parts

Each character has five body parts, with HP set as a percentage of their total max HP:

| Body Part | HP (% of Max HP) |
|---|---|
| Head | 50% |
| Torso | 100% |
| Arm 1 | 60% |
| Arm 2 | 60% |
| Legs | 80% |

### Body Part States

As a body part takes damage, it transitions through states based on remaining HP:

| State | Threshold |
|---|---|
| *(Normal)* | HP ≥ 60% of max |
| **Wounded** | HP < 60% of max |
| **Crippled** | HP < 30% of max |
| **Destroyed** | HP ≤ 0 |

State changes are announced in the combat log. The current state is shown on the character card.

---

## Injuries & Stat Penalties

When a body part enters a new state, it applies stat penalties to the character. These replace (not stack with) the previous state's penalties. Stats cannot be reduced below 1 by injuries.

### Head
| State | Penalties |
|---|---|
| Wounded *(Dizziness)* | −1 ACC, −1 EVA, −1 SPD |
| Crippled *(Concussion)* | −2 ACC, −3 EVA, −2 SPD, −1 POW, −2 RES |
| Destroyed *(Incapacitated)* | −3 ACC, −5 EVA, −3 SPD, −2 POW, −4 RES |

### Torso
| State | Penalties |
|---|---|
| Wounded *(Contused)* | −1 RES, −1 POW, −2 CON |
| Crippled *(Internal Damage)* | −2 RES, −2 POW, −3 CON |
| Destroyed *(Critical Blood Loss)* | −3 RES, −3 POW, −4 CON |

### Arms (Arm 1 & Arm 2)
| State | Penalties |
|---|---|
| Wounded *(Weakened Grip)* | −1 POW, −1 ACC, −1 SPD |
| Crippled *(Broken)* | −2 POW, −2 ACC, −1 SPD |
| Destroyed *(Removed Arm)* | −3 POW, −3 ACC, −1 SPD |

### Legs
| State | Penalties |
|---|---|
| Wounded *(Limping)* | −2 SPD, −2 EVA, −1 POW |
| Crippled *(Immobile)* | −4 SPD, −4 EVA, −2 POW |
| Destroyed *(Severely Immobile)* | −5 SPD, −5 EVA |

Injury descriptions (e.g. *Dizziness*, *Limping*) are displayed on the character card below their name. Stat values shown on the card reflect current effective values, with any reduced stats highlighted in red.

CON penalties also reduce effective max HP. If a character's current HP exceeds their effective max HP after an attack, their HP is clamped down to the new maximum.

---

## Stamina

Each character starts each combat with full stamina. At the start of their turn, they recover stamina based on their Resilience stat. Stamina cannot exceed the character's max stamina.

The stamina bar on each character card shows filled (green) and depleted (gray) pips. If a character has insufficient stamina for an action, that action is grayed out.

---

## Death & Victory

A character dies when their overall HP reaches 0. Dead characters are collapsed on the board and struck through in the turn order.

The game ends when all characters on one team are defeated:
- All enemies defeated → **Player Victory**
- All players defeated → **Defeat**
