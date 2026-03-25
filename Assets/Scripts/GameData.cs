using System.Collections.Generic;
using UnityEngine;

public enum CharacterClass { Duelist, Physician, Gladiator, Veteran }
public enum BodyPartType { Head, Torso, Arm1, Arm2, Legs }
public enum InjuryState { Healthy, Wounded, Crippled, Destroyed }
public enum ActionType { BasicAttack, TargetedAttack, ClassAbility, EndTurn }
public enum GamePhase { SelectAction, SelectTarget, SelectBodyPart, EnemyTurn, GameOver }

[System.Serializable]
public class BodyPart
{
    public BodyPartType type;
    public float maxHp;
    public float currentHp;
    public InjuryState injuryState = InjuryState.Healthy;
    public float HpPercent => maxHp > 0 ? currentHp / maxHp : 0f;

    public string DisplayName()
    {
        switch (type)
        {
            case BodyPartType.Head:  return "Head";
            case BodyPartType.Torso: return "Torso";
            case BodyPartType.Arm1:  return "Arm L";
            case BodyPartType.Arm2:  return "Arm R";
            case BodyPartType.Legs:  return "Legs";
            default: return "";
        }
    }

    public string InjuryDescription()
    {
        if (injuryState == InjuryState.Healthy) return "";
        switch (type)
        {
            case BodyPartType.Head:
                return injuryState == InjuryState.Wounded ? "Dizziness" :
                       injuryState == InjuryState.Crippled ? "Concussion" : "Incapacitated";
            case BodyPartType.Torso:
                return injuryState == InjuryState.Wounded ? "Contused" :
                       injuryState == InjuryState.Crippled ? "Internal Damage" : "Critical Blood Loss";
            case BodyPartType.Arm1:
            case BodyPartType.Arm2:
                return injuryState == InjuryState.Wounded ? "Weakened Grip" :
                       injuryState == InjuryState.Crippled ? "Broken" : "Removed Arm";
            case BodyPartType.Legs:
                return injuryState == InjuryState.Wounded ? "Limping" :
                       injuryState == InjuryState.Crippled ? "Immobile" : "Severely Immobile";
            default: return "";
        }
    }
}

public class CharacterData
{
    public string characterName;
    public CharacterClass characterClass;
    public bool isPlayerTeam;
    public bool isDead;
    public bool hasActedThisRound;

    // Base stats
    public int basePower, baseAccuracy, baseConstitution, baseEvasion, baseSpeed, baseResilience;
    // Bonus stats (Leadership)
    public int bonusPower, bonusAccuracy, bonusConstitution, bonusEvasion, bonusSpeed, bonusResilience;

    // Body parts
    public BodyPart head, torso, arm1, arm2, legs;

    // Resources
    public float currentHp;
    public float currentStamina;

    // ── Derived stats ──────────────────────────────────────────────
    public float MaxHp         => 10f + (EffectiveConstitution - 1) * 2f;
    public float MaxStamina    => 3f  + Mathf.FloorToInt(EffectiveResilience / 4f);
    public float StaminaRecovery => 1f + Mathf.FloorToInt(EffectiveResilience / 4f);
    public float BasicAttackDamage => 5f + EffectivePower / 2f;
    public float HitChance     => Mathf.Min(0.95f, 0.70f + EffectiveAccuracy  * 0.025f);
    public float DodgeChance   => Mathf.Min(0.40f, EffectiveEvasion * 0.025f);

    public int EffectivePower       => Mathf.Max(0, basePower       + bonusPower       - Penalty("power"));
    public int EffectiveAccuracy    => Mathf.Max(0, baseAccuracy    + bonusAccuracy    - Penalty("accuracy"));
    public int EffectiveConstitution=> Mathf.Max(1, baseConstitution+ bonusConstitution- Penalty("constitution"));
    public int EffectiveEvasion     => Mathf.Max(0, baseEvasion     + bonusEvasion     - Penalty("evasion"));
    public int EffectiveSpeed       => Mathf.Max(0, baseSpeed       + bonusSpeed       - Penalty("speed"));
    public int EffectiveResilience  => Mathf.Max(0, baseResilience  + bonusResilience  - Penalty("resilience"));

    // Short display name for turn order (e.g. "P1")
    public string ShortName => characterName.Replace("Player ", "P").Replace("Enemy ", "E");

    // ── Body-part helpers ──────────────────────────────────────────
    public List<BodyPart> GetBodyParts()
    {
        var list = new List<BodyPart>();
        if (head  != null) list.Add(head);
        if (torso != null) list.Add(torso);
        if (arm1  != null) list.Add(arm1);
        if (arm2  != null) list.Add(arm2);
        if (legs  != null) list.Add(legs);
        return list;
    }

    public BodyPart GetBodyPart(BodyPartType t)
    {
        switch (t)
        {
            case BodyPartType.Head:  return head;
            case BodyPartType.Torso: return torso;
            case BodyPartType.Arm1:  return arm1;
            case BodyPartType.Arm2:  return arm2;
            case BodyPartType.Legs:  return legs;
            default: return null;
        }
    }

    public void InitBodyParts()
    {
        float mhp = MaxHp;
        head  = new BodyPart { type = BodyPartType.Head,  maxHp = mhp * 0.5f, currentHp = mhp * 0.5f };
        torso = new BodyPart { type = BodyPartType.Torso, maxHp = mhp,        currentHp = mhp        };
        arm1  = new BodyPart { type = BodyPartType.Arm1,  maxHp = mhp * 0.6f, currentHp = mhp * 0.6f };
        arm2  = new BodyPart { type = BodyPartType.Arm2,  maxHp = mhp * 0.6f, currentHp = mhp * 0.6f };
        legs  = new BodyPart { type = BodyPartType.Legs,  maxHp = mhp * 0.8f, currentHp = mhp * 0.8f };
    }

    public void UpdateInjuryState(BodyPart bp)
    {
        if      (bp.currentHp <= 0)        bp.injuryState = InjuryState.Destroyed;
        else if (bp.HpPercent < 0.30f)     bp.injuryState = InjuryState.Crippled;
        else if (bp.HpPercent < 0.60f)     bp.injuryState = InjuryState.Wounded;
        else                               bp.injuryState = InjuryState.Healthy;
    }

    public List<string> ActiveInjuryDescriptions()
    {
        var list = new List<string>();
        foreach (var bp in GetBodyParts())
            if (bp.injuryState != InjuryState.Healthy)
                list.Add(bp.InjuryDescription());
        return list;
    }

    // ── Penalty calculation ────────────────────────────────────────
    private int Penalty(string stat)
    {
        int total = 0;
        foreach (var bp in GetBodyParts())
            total += BpPenalty(bp, stat);
        return total;
    }

    private int BpPenalty(BodyPart bp, string stat)
    {
        int s = (int)bp.injuryState;
        if (s == 0) return 0;
        switch (bp.type)
        {
            case BodyPartType.Head:
                if (stat == "accuracy")    return s == 1 ? 1 : s == 2 ? 2 : 3;
                if (stat == "evasion")     return s == 1 ? 1 : s == 2 ? 3 : 5;
                if (stat == "speed")       return s == 1 ? 1 : s == 2 ? 2 : 3;
                if (stat == "power")       return s >= 2 ? (s == 2 ? 1 : 2) : 0;
                if (stat == "resilience")  return s == 1 ? 0 : s == 2 ? 2 : 4;
                break;
            case BodyPartType.Torso:
                if (stat == "resilience")  return s;
                if (stat == "power")       return s;
                if (stat == "constitution")return s == 1 ? 2 : s == 2 ? 3 : 4;
                break;
            case BodyPartType.Arm1:
            case BodyPartType.Arm2:
                if (stat == "power")       return s == 1 ? 1 : s == 2 ? 2 : 3;
                if (stat == "accuracy")    return s == 1 ? 1 : s == 2 ? 2 : 3;
                if (stat == "speed")       return 1;
                break;
            case BodyPartType.Legs:
                if (stat == "speed")       return s == 1 ? 2 : s == 2 ? 4 : 5;
                if (stat == "evasion")     return s == 1 ? 2 : s == 2 ? 4 : 5;
                if (stat == "power")       return s == 1 ? 1 : s == 2 ? 2 : 0;
                break;
        }
        return 0;
    }

    // ── Class info ─────────────────────────────────────────────────
    public string ClassName => characterClass.ToString();

    public string AbilityName
    {
        get
        {
            switch (characterClass)
            {
                case CharacterClass.Duelist:   return "Critical Strike";
                case CharacterClass.Physician: return "Heal";
                case CharacterClass.Gladiator: return "Brutal Target";
                case CharacterClass.Veteran:   return "Leadership";
                default: return "Ability";
            }
        }
    }

    public string AbilityDescription
    {
        get
        {
            switch (characterClass)
            {
                case CharacterClass.Duelist:   return "Deal 2x damage on hit";
                case CharacterClass.Physician: return "Heal ally for basic attack damage";
                case CharacterClass.Gladiator: return "2x damage to targeted body part";
                case CharacterClass.Veteran:   return "Grant +1 all stats to all allies";
                default: return "";
            }
        }
    }

    public int  AbilityStaminaCost  => characterClass == CharacterClass.Veteran ? 2 : 3;
    public bool AbilityNeedsTarget  => characterClass != CharacterClass.Veteran;
    public bool AbilityNeedsBodyPart=> characterClass == CharacterClass.Gladiator;
    public bool AbilityTargetsAlly  => characterClass == CharacterClass.Physician;
}
