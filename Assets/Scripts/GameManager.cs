using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CombatLogType { Normal, Ability, Injury, Kill }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public List<CharacterData> playerTeam = new List<CharacterData>();
    public List<CharacterData> enemyTeam  = new List<CharacterData>();
    public List<CharacterData> turnOrder  = new List<CharacterData>();

    public int  currentTurnIndex;
    public int  currentRound = 1;
    public bool gameOver;
    public bool playerWon;
    public GamePhase phase = GamePhase.SelectAction;

    public ActionType      pendingAction;
    public CharacterData   selectedTarget;
    public BodyPartType    selectedBodyPart;

    public List<(string msg, CombatLogType type)> logEntries = new List<(string, CombatLogType)>();

    public System.Action OnStateChanged;

    void Awake() { Instance = this; InitGame(); }
    void Start()  { }

    void InitGame()
    {
        StopAllCoroutines();
        playerTeam.Clear();
        enemyTeam.Clear();
        turnOrder.Clear();
        logEntries.Clear();
        currentRound = 1;
        currentTurnIndex = 0;
        gameOver = false;
        phase = GamePhase.SelectAction;

        var pClasses = RandomClasses();
        var eClasses = RandomClasses();

        for (int i = 0; i < 4; i++)
        {
            playerTeam.Add(CreateCharacter("Player " + (i + 1), pClasses[i], true));
            enemyTeam .Add(CreateCharacter("Enemy "  + (i + 1), eClasses[i], false));
        }

        var all = new List<CharacterData>(playerTeam);
        all.AddRange(enemyTeam);
        all.Sort((a, b) => b.EffectiveSpeed.CompareTo(a.EffectiveSpeed));
        turnOrder = all;

        PushLog("Combat Arena begins! Fight!", CombatLogType.Normal);
        OnStateChanged?.Invoke();

        if (!CurrentCharacter.isPlayerTeam)
            StartCoroutine(EnemyTurnRoutine());
    }

    List<CharacterClass> RandomClasses()
    {
        var list = new List<CharacterClass>
            { CharacterClass.Duelist, CharacterClass.Physician,
              CharacterClass.Gladiator, CharacterClass.Veteran };
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CharacterClass tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
        return list;
    }

    CharacterData CreateCharacter(string charName, CharacterClass cls, bool isPlayer)
    {
        var c = new CharacterData();
        c.characterName  = charName;
        c.characterClass = cls;
        c.isPlayerTeam   = isPlayer;
        int[] s = GenerateStats();
        c.basePower = s[0]; c.baseAccuracy = s[1]; c.baseConstitution = s[2];
        c.baseEvasion = s[3]; c.baseSpeed = s[4]; c.baseResilience = s[5];
        c.InitBodyParts();
        c.currentHp      = c.MaxHp;
        c.currentStamina = c.MaxStamina;
        return c;
    }

    int[] GenerateStats()
    {
        int[] stats   = new int[6];
        int target    = Random.Range(25, 31);
        int remaining = target;
        for (int i = 0; i < 5; i++)
        {
            int lo = Mathf.Max(2,  remaining - 11 * (5 - i));
            int hi = Mathf.Min(11, remaining -  2 * (5 - i));
            stats[i] = Random.Range(lo, hi + 1);
            remaining -= stats[i];
        }
        stats[5] = remaining;
        return stats;
    }

    public CharacterData CurrentCharacter => turnOrder[currentTurnIndex];

    public List<CharacterData> GetValidTargets()
    {
        if (pendingAction == ActionType.ClassAbility && CurrentCharacter.AbilityTargetsAlly)
            return playerTeam.FindAll(p => !p.isDead);
        return enemyTeam.FindAll(e => !e.isDead);
    }

    // ── Player input ──────────────────────────────────────────────

    public void PlayerSelectAction(ActionType action)
    {
        if (gameOver || !CurrentCharacter.isPlayerTeam) return;
        if (phase != GamePhase.SelectAction) return;

        pendingAction = action;

        if (action == ActionType.EndTurn)
        {
            PushLog(CurrentCharacter.characterName + " ends their turn.", CombatLogType.Normal);
            AdvanceTurn();
            return;
        }
        if (action == ActionType.BasicAttack || action == ActionType.TargetedAttack)
        {
            phase = GamePhase.SelectTarget;
            OnStateChanged?.Invoke();
            return;
        }
        if (action == ActionType.ClassAbility)
        {
            if (!CurrentCharacter.AbilityNeedsTarget)
            {
                DoClassAbility(CurrentCharacter, null, BodyPartType.Head);
                return;
            }
            phase = GamePhase.SelectTarget;
            OnStateChanged?.Invoke();
        }
    }

    public void PlayerSelectTarget(CharacterData target)
    {
        if (phase != GamePhase.SelectTarget) return;
        selectedTarget = target;

        bool needsBp = pendingAction == ActionType.TargetedAttack ||
                       (pendingAction == ActionType.ClassAbility && CurrentCharacter.AbilityNeedsBodyPart);
        if (needsBp)
        {
            phase = GamePhase.SelectBodyPart;
            OnStateChanged?.Invoke();
            return;
        }

        if (pendingAction == ActionType.BasicAttack)
            DoBasicAttack(CurrentCharacter, target);
        else
            DoClassAbility(CurrentCharacter, target, BodyPartType.Head);
    }

    public void PlayerSelectBodyPart(BodyPartType bp)
    {
        if (phase != GamePhase.SelectBodyPart) return;
        selectedBodyPart = bp;

        if (pendingAction == ActionType.TargetedAttack)
            DoTargetedAttack(CurrentCharacter, selectedTarget, bp);
        else
            DoClassAbility(CurrentCharacter, selectedTarget, bp);
    }

    // ── Combat ────────────────────────────────────────────────────

    bool RollHit(CharacterData atk, CharacterData def)
        => Random.value < (atk.HitChance - def.DodgeChance);

    void ApplyHpDamage(CharacterData t, float dmg)
    {
        t.currentHp = Mathf.Max(0, t.currentHp - dmg);
        if (t.currentHp <= 0 && !t.isDead)
        {
            t.isDead = true;
            t.currentHp = 0;
            PushLog(t.characterName + " has been defeated!", CombatLogType.Kill);
        }
    }

    void ApplyBpDamage(CharacterData t, BodyPartType bpt, float dmg)
    {
        BodyPart bp = t.GetBodyPart(bpt);
        bp.currentHp = Mathf.Max(0, bp.currentHp - dmg);
        t.UpdateInjuryState(bp);
        if (bp.injuryState != InjuryState.Healthy)
            PushLog(t.characterName + "'s " + bp.DisplayName() + " is " + bp.InjuryDescription() + "!", CombatLogType.Injury);
    }

    void DoBasicAttack(CharacterData atk, CharacterData def)
    {
        atk.currentStamina -= 1;
        if (RollHit(atk, def))
        {
            float dmg = atk.BasicAttackDamage;
            ApplyHpDamage(def, dmg);
            PushLog(atk.characterName + " attacks " + def.characterName + " for " + dmg.ToString("F1") + " damage.", CombatLogType.Normal);
        }
        else
        {
            PushLog(atk.characterName + "'s attack on " + def.characterName + " missed!", CombatLogType.Normal);
        }
        CheckThenAdvance();
    }

    void DoTargetedAttack(CharacterData atk, CharacterData def, BodyPartType bpt)
    {
        atk.currentStamina -= 3;
        if (RollHit(atk, def))
        {
            float bpDmg = atk.BasicAttackDamage * 2f;
            float hpDmg = bpDmg * 0.5f;
            ApplyBpDamage(def, bpt, bpDmg);
            ApplyHpDamage(def, hpDmg);
            PushLog(atk.characterName + " targets " + def.GetBodyPart(bpt).DisplayName() + " on " + def.characterName + " for " + bpDmg.ToString("F1") + "!", CombatLogType.Normal);
        }
        else
        {
            PushLog(atk.characterName + "'s targeted attack on " + def.characterName + " missed!", CombatLogType.Normal);
        }
        CheckThenAdvance();
    }

    void DoClassAbility(CharacterData atk, CharacterData target, BodyPartType bpt)
    {
        atk.currentStamina -= atk.AbilityStaminaCost;
        ExecuteAbilityEffect(atk, target, bpt);
        CheckThenAdvance();
    }

    void ExecuteAbilityEffect(CharacterData atk, CharacterData target, BodyPartType bpt)
    {
        if (atk.characterClass == CharacterClass.Duelist)
        {
            if (RollHit(atk, target))
            {
                float dmg = atk.BasicAttackDamage * 2f;
                ApplyHpDamage(target, dmg);
                PushLog(atk.characterName + " uses Critical Strike on " + target.characterName + " for " + dmg.ToString("F1") + "!", CombatLogType.Ability);
            }
            else
                PushLog(atk.characterName + "'s Critical Strike missed " + target.characterName + "!", CombatLogType.Ability);
            return;
        }
        if (atk.characterClass == CharacterClass.Physician)
        {
            float heal = atk.BasicAttackDamage;
            target.currentHp = Mathf.Min(target.MaxHp, target.currentHp + heal);
            PushLog(atk.characterName + " heals " + target.characterName + " for " + heal.ToString("F1") + " HP!", CombatLogType.Ability);
            return;
        }
        if (atk.characterClass == CharacterClass.Gladiator)
        {
            if (RollHit(atk, target))
            {
                float bpDmg = atk.BasicAttackDamage * 2f;
                ApplyBpDamage(target, bpt, bpDmg);
                ApplyHpDamage(target, bpDmg * 0.5f);
                PushLog(atk.characterName + " uses Brutal Target on " + target.GetBodyPart(bpt).DisplayName() + " for " + bpDmg.ToString("F1") + "!", CombatLogType.Ability);
            }
            else
                PushLog(atk.characterName + "'s Brutal Target missed " + target.characterName + "!", CombatLogType.Ability);
            return;
        }
        if (atk.characterClass == CharacterClass.Veteran)
        {
            List<CharacterData> allyTeam = atk.isPlayerTeam ? playerTeam : enemyTeam;
            for (int ai = 0; ai < allyTeam.Count; ai++)
            {
                CharacterData ally = allyTeam[ai];
                if (!ally.isDead)
                {
                    ally.bonusPower++;    ally.bonusAccuracy++;
                    ally.bonusConstitution++;
                    ally.bonusEvasion++;  ally.bonusSpeed++;
                    ally.bonusResilience++;
                }
            }
            PushLog(atk.characterName + " uses Leadership! All allies gain +1 to all stats!", CombatLogType.Ability);
        }
    }

    void CheckThenAdvance()
    {
        CheckGameOver();
        if (!gameOver) AdvanceTurn();
    }

    void AdvanceTurn()
    {
        CurrentCharacter.hasActedThisRound = true;

        int next = -1;
        for (int i = 1; i <= turnOrder.Count; i++)
        {
            int idx = (currentTurnIndex + i) % turnOrder.Count;
            if (!turnOrder[idx].isDead && !turnOrder[idx].hasActedThisRound)
            { next = idx; break; }
        }

        if (next == -1)
        {
            currentRound++;
            for (int i = 0; i < turnOrder.Count; i++)
                turnOrder[i].hasActedThisRound = false;
            for (int i = 0; i < turnOrder.Count; i++)
                if (!turnOrder[i].isDead) { next = i; break; }
        }

        if (next == -1) return;

        currentTurnIndex = next;
        CharacterData cur = CurrentCharacter;
        cur.currentStamina = Mathf.Min(cur.MaxStamina, cur.currentStamina + cur.StaminaRecovery);

        phase = GamePhase.SelectAction;
        OnStateChanged?.Invoke();

        if (!gameOver && !cur.isPlayerTeam)
            StartCoroutine(EnemyTurnRoutine());
    }

    void CheckGameOver()
    {
        bool allEnemiesDead = enemyTeam .TrueForAll(c => c.isDead);
        bool allPlayersDead = playerTeam.TrueForAll(c => c.isDead);
        if (!allEnemiesDead && !allPlayersDead) return;

        gameOver  = true;
        playerWon = allEnemiesDead;
        phase     = GamePhase.GameOver;
        PushLog(playerWon ? "PLAYER VICTORY!" : "DEFEAT - Enemies Victorious", CombatLogType.Normal);
        OnStateChanged?.Invoke();
    }

    void PushLog(string msg, CombatLogType type)
    {
        logEntries.Insert(0, (msg, type));
        if (logEntries.Count > 5)
            logEntries.RemoveAt(logEntries.Count - 1);
    }

    // ── Enemy AI ──────────────────────────────────────────────────

    IEnumerator EnemyTurnRoutine()
    {
        phase = GamePhase.EnemyTurn;
        OnStateChanged?.Invoke();
        yield return new WaitForSeconds(1.2f);

        CharacterData cur = CurrentCharacter;
        if (cur.isDead || cur.isPlayerTeam) { AdvanceTurn(); yield break; }

        List<CharacterData> living = playerTeam.FindAll(p => !p.isDead);
        if (living.Count == 0) { CheckGameOver(); yield break; }

        CharacterData aiTarget = living[Random.Range(0, living.Count)];

        if (cur.characterClass == CharacterClass.Physician && cur.currentStamina >= 3)
        {
            CharacterData wounded = enemyTeam.Find(e => !e.isDead && e.currentHp < e.MaxHp * 0.5f && e != cur);
            if (wounded != null) { DoClassAbility(cur, wounded, BodyPartType.Head); yield break; }
        }

        if (cur.characterClass == CharacterClass.Veteran && cur.currentStamina >= 2)
        { DoClassAbility(cur, null, BodyPartType.Head); yield break; }

        BodyPartType[] bpOptions = new BodyPartType[]
            { BodyPartType.Head, BodyPartType.Torso, BodyPartType.Arm1, BodyPartType.Arm2, BodyPartType.Legs };
        BodyPartType randBp = bpOptions[Random.Range(0, bpOptions.Length)];
        int roll = Random.Range(0, 3);

        if (roll == 0 && cur.currentStamina >= 3)
            DoTargetedAttack(cur, aiTarget, randBp);
        else if (roll == 1 && cur.currentStamina >= cur.AbilityStaminaCost && cur.characterClass != CharacterClass.Physician)
            DoClassAbility(cur, aiTarget, randBp);
        else if (cur.currentStamina >= 1)
            DoBasicAttack(cur, aiTarget);
        else
        {
            PushLog(cur.characterName + " ends their turn.", CombatLogType.Normal);
            AdvanceTurn();
        }
    }

    public void RestartGame()
    {
        InitGame();
    }
}
