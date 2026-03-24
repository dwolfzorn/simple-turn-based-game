# Simple Turn Based Game — Overview

## Structure

The game is a turn-based combat game played between two teams of four characters each: **Player Team** and **Enemy Team**. Combat plays out in rounds. Within each turn, a character may take as many actions as their stamina allows. The player must manually click **End Turn** to pass to the next character.

---

## Stats
| Stat | Abbr | Effect | Derived Value |
|---|---|---|---|
| Power | POW | Increases damage dealt by attacks | Damage = base + floor(POW / 2) |
| Accuracy | ACC | Increases hit chance | Hit chance = min(95%, 70% + ACC × 2.5%) |
| Constitution | CON | Increases total HP pool | Max HP = 10 + (CON − 1) × 2 |
| Evasion | EVA | Increases chance to dodge incoming attacks | Dodge chance = min(40%, EVA × 2.5%) |
| Speed | SPD | Determines turn order | Higher SPD acts first |
| Resilience | RES | Increases max stamina and stamina recovery per turn | Max Stamina = 3 + floor(RES / 4); Recovery = 1 + floor(RES / 4) per turn |

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

| Body Part | State | Injury Description | Penalties |
| Head |---|---|
| Head | Wounded | *(Dizziness)* | −1 ACC, −1 EVA, −1 SPD |
| Head | Crippled | *(Concussion)* | −2 ACC, −3 EVA, −2 SPD, −1 POW, −2 RES |
| Head | Destroyed | *(Incapacitated)* | −3 ACC, −5 EVA, −3 SPD, −2 POW, −4 RES |
| Torso | Wounded | *(Contused)* | −1 RES, −1 POW, −2 CON |
| Torso | Crippled | *(Internal Damage)* | −2 RES, −2 POW, −3 CON |
| Torso | Destroyed | *(Critical Blood Loss)* | −3 RES, −3 POW, −4 CON |
| Arms | Wounded | *(Weakened Grip)* | −1 POW, −1 ACC, −1 SPD |
| Arms | Crippled | *(Broken)* | −2 POW, −2 ACC, −1 SPD |
| Arms | Destroyed | *(Removed Arm)* | −3 POW, −3 ACC, −1 SPD |
| Legs | Wounded | *(Limping)* | −2 SPD, −2 EVA, −1 POW |
| Legs | Crippled | *(Immobile)* | −4 SPD, −4 EVA, −2 POW |
| Legs | Destroyed | *(Severely Immobile)* | −5 SPD, −5 EVA |

Injury descriptions are displayed on the character card below their name. Stat values shown on the card reflect current effective values, with any reduced stats highlighted in red.

CON penalties reduce effective max HP. If a character's current HP exceeds their effective max HP after an attack, their HP is decreased to the new maximum.

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