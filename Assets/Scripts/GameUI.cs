using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GameUI : MonoBehaviour
{
    private GameManager gm;
    private VisualElement root;

    private Label          roundLabel;
    private VisualElement  turnOrderContainer;
    private VisualElement  playerCardsContainer;
    private VisualElement  enemyCardsContainer;
    private Label          currentTurnLabel;
    private Button         btnBasic, btnTargeted, btnAbility, btnEndTurn;
    private Label          selectionPrompt;
    private VisualElement  combatLogContainer;
    private VisualElement  gameOverPanel;
    private Label          gameOverText;
    private Button         btnRestart;

    void Start()
    {
        StartCoroutine(InitAfterFrame());
    }

    IEnumerator InitAfterFrame()
    {
        yield return null; // wait one frame so UIDocument finishes loading

        gm = GameManager.Instance;
        var doc = GetComponent<UIDocument>();
        root = doc.rootVisualElement;
        QueryElements();
        BindButtons();
        gm.OnStateChanged += RefreshUI;
        if (gm.turnOrder != null && gm.turnOrder.Count > 0)
            RefreshUI();
    }

    void OnDestroy()
    {
        if (gm != null) gm.OnStateChanged -= RefreshUI;
    }

    void QueryElements()
    {
        roundLabel           = root.Q<Label>("round-label");
        turnOrderContainer   = root.Q<VisualElement>("turn-order-container");
        playerCardsContainer = root.Q<VisualElement>("player-cards");
        enemyCardsContainer  = root.Q<VisualElement>("enemy-cards");
        currentTurnLabel     = root.Q<Label>("current-turn-label");
        btnBasic             = root.Q<Button>("btn-basic");
        btnTargeted          = root.Q<Button>("btn-targeted");
        btnAbility           = root.Q<Button>("btn-ability");
        btnEndTurn           = root.Q<Button>("btn-endturn");
        selectionPrompt      = root.Q<Label>("selection-prompt");
        combatLogContainer   = root.Q<VisualElement>("combat-log");
        gameOverPanel        = root.Q<VisualElement>("game-over-panel");
        gameOverText         = root.Q<Label>("game-over-text");
        btnRestart           = root.Q<Button>("btn-restart");
    }

    void BindButtons()
    {
        btnBasic   .clicked += () => gm.PlayerSelectAction(ActionType.BasicAttack);
        btnTargeted.clicked += () => gm.PlayerSelectAction(ActionType.TargetedAttack);
        btnAbility .clicked += () => gm.PlayerSelectAction(ActionType.ClassAbility);
        btnEndTurn .clicked += () => gm.PlayerSelectAction(ActionType.EndTurn);
        btnRestart .clicked += () => gm.RestartGame();
    }

    void RefreshUI()
    {
        if (root == null || gm == null || gm.turnOrder == null || gm.turnOrder.Count == 0) return;
        RefreshRound();
        RefreshTurnOrder();
        RefreshCards();
        RefreshActionPanel();
        RefreshLog();
        RefreshGameOver();
    }

    void RefreshRound()
    {
        roundLabel.text = "Round " + gm.currentRound;
    }

    void RefreshTurnOrder()
    {
        turnOrderContainer.Clear();
        for (int i = 0; i < gm.turnOrder.Count; i++)
        {
            CharacterData c = gm.turnOrder[i];
            var lbl = new Label(c.ShortName);
            lbl.AddToClassList("turn-indicator");
            lbl.AddToClassList(c.isPlayerTeam ? "ti-player" : "ti-enemy");
            if (i == gm.currentTurnIndex) lbl.AddToClassList("ti-current");
            if (c.hasActedThisRound)      lbl.AddToClassList("ti-acted");
            if (c.isDead)                 lbl.AddToClassList("ti-dead");
            turnOrderContainer.Add(lbl);
        }
    }

    void RefreshCards()
    {
        playerCardsContainer.Clear();
        enemyCardsContainer .Clear();
        foreach (var c in gm.playerTeam) playerCardsContainer.Add(BuildCard(c));
        foreach (var c in gm.enemyTeam)  enemyCardsContainer .Add(BuildCard(c));
    }

    VisualElement BuildCard(CharacterData c)
    {
        var card = new VisualElement();
        card.AddToClassList("char-card");
        card.AddToClassList(c.isPlayerTeam ? "card-player" : "card-enemy");
        if (c.isDead)                              card.AddToClassList("card-dead");
        if (!c.isDead && gm.CurrentCharacter == c) card.AddToClassList("card-active");

        if (!gm.gameOver && gm.phase == GamePhase.SelectTarget && !c.isDead)
        {
            List<CharacterData> valid = gm.GetValidTargets();
            if (valid.Contains(c))
            {
                card.AddToClassList("card-targetable");
                card.RegisterCallback<ClickEvent>(_ => gm.PlayerSelectTarget(c));
            }
        }

        var nameLbl  = new Label(c.characterName); nameLbl.AddToClassList("card-name"); card.Add(nameLbl);
        var classLbl = new Label(c.ClassName);     classLbl.AddToClassList("card-class"); card.Add(classLbl);

        if (c.isDead)
        {
            var deadLbl = new Label("DEAD"); deadLbl.AddToClassList("card-dead-label"); card.Add(deadLbl);
            return card;
        }

        List<string> injuries = c.ActiveInjuryDescriptions();
        if (injuries.Count > 0)
        {
            var injLbl = new Label(string.Join(", ", injuries));
            injLbl.AddToClassList("card-injuries"); card.Add(injLbl);
        }

        card.Add(BuildBar(c.currentHp / c.MaxHp, "hp-bar-bg", "hp-bar-fill", ""));
        var hpLbl = new Label("HP: " + ((int)c.currentHp) + " / " + ((int)c.MaxHp));
        hpLbl.AddToClassList("card-hp-label"); card.Add(hpLbl);

        var staminaRow = new VisualElement(); staminaRow.AddToClassList("stamina-row");
        int maxSt = Mathf.RoundToInt(c.MaxStamina);
        int curSt = Mathf.RoundToInt(c.currentStamina);
        for (int i = 0; i < maxSt; i++)
        {
            var pip = new VisualElement(); pip.AddToClassList("stamina-pip");
            if (i >= curSt) pip.AddToClassList("pip-empty");
            staminaRow.Add(pip);
        }
        card.Add(staminaRow);

        var statsGrid = new VisualElement(); statsGrid.AddToClassList("stats-grid");
        AddStatItem(statsGrid, "POW", c.EffectivePower,        c.basePower        + c.bonusPower);
        AddStatItem(statsGrid, "ACC", c.EffectiveAccuracy,     c.baseAccuracy     + c.bonusAccuracy);
        AddStatItem(statsGrid, "CON", c.EffectiveConstitution, c.baseConstitution + c.bonusConstitution);
        AddStatItem(statsGrid, "EVA", c.EffectiveEvasion,      c.baseEvasion      + c.bonusEvasion);
        AddStatItem(statsGrid, "SPD", c.EffectiveSpeed,        c.baseSpeed        + c.bonusSpeed);
        AddStatItem(statsGrid, "RES", c.EffectiveResilience,   c.baseResilience   + c.bonusResilience);
        card.Add(statsGrid);

        var bpSection = new VisualElement(); bpSection.AddToClassList("bp-section");
        foreach (BodyPart bp in c.GetBodyParts())
        {
            var row = new VisualElement(); row.AddToClassList("bp-row");
            if (!gm.gameOver && gm.phase == GamePhase.SelectBodyPart && gm.selectedTarget == c)
            {
                row.AddToClassList("bp-targetable");
                BodyPartType bptCopy = bp.type;
                row.RegisterCallback<ClickEvent>(_ => gm.PlayerSelectBodyPart(bptCopy));
            }

            var lbl = new Label(bp.DisplayName()); lbl.AddToClassList("bp-label"); row.Add(lbl);

            string injClass = bp.injuryState == InjuryState.Healthy  ? "bp-healthy"  :
                              bp.injuryState == InjuryState.Wounded   ? "bp-wounded"  :
                              bp.injuryState == InjuryState.Crippled  ? "bp-crippled" : "bp-destroyed";
            row.Add(BuildBar(bp.HpPercent, "bp-bar-bg", "bp-bar-fill", injClass));
            bpSection.Add(row);
        }
        card.Add(bpSection);

        return card;
    }

    VisualElement BuildBar(float pct, string bgClass, string fillClass, string extraFillClass)
    {
        var bg   = new VisualElement(); bg.AddToClassList(bgClass);
        var fill = new VisualElement(); fill.AddToClassList(fillClass);
        if (!string.IsNullOrEmpty(extraFillClass)) fill.AddToClassList(extraFillClass);
        fill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100f);
        bg.Add(fill);
        return bg;
    }

    void AddStatItem(VisualElement parent, string statName, int effective, int baseVal)
    {
        var el = new Label(statName + ":" + effective);
        el.AddToClassList("stat-item");
        if      (effective > baseVal) el.AddToClassList("stat-buffed");
        else if (effective < baseVal) el.AddToClassList("stat-penalized");
        parent.Add(el);
    }

    void RefreshActionPanel()
    {
        if (gm.gameOver) return;

        CharacterData cur      = gm.CurrentCharacter;
        bool          isPlayer = cur.isPlayerTeam && gm.phase != GamePhase.EnemyTurn;

        currentTurnLabel.text = isPlayer
            ? cur.characterName + " (" + cur.ClassName + ") - Your Turn"
            : cur.characterName + " (" + cur.ClassName + ") - Enemy Turn...";

        float basicDmg = cur.BasicAttackDamage;
        btnBasic   .text = "Basic Attack\nDmg: " + basicDmg.ToString("F0") + " | Cost: 1 SP";
        btnTargeted.text = "Targeted Attack\nDmg: " + (basicDmg * 2f).ToString("F0") + " to BP | Cost: 3 SP";
        btnAbility .text = cur.AbilityName + "\n" + cur.AbilityDescription + " | Cost: " + cur.AbilityStaminaCost + " SP";

        bool canAct = isPlayer && gm.phase == GamePhase.SelectAction;
        btnBasic   .SetEnabled(canAct && cur.currentStamina >= 1);
        btnTargeted.SetEnabled(canAct && cur.currentStamina >= 3);
        btnAbility .SetEnabled(canAct && cur.currentStamina >= cur.AbilityStaminaCost);
        btnEndTurn .SetEnabled(canAct);

        if (gm.phase == GamePhase.SelectTarget)
            selectionPrompt.text = (gm.pendingAction == ActionType.ClassAbility && cur.AbilityTargetsAlly)
                ? "Click an ally to target" : "Click an enemy to target";
        else if (gm.phase == GamePhase.SelectBodyPart)
            selectionPrompt.text = "Click a body part on the target";
        else if (gm.phase == GamePhase.EnemyTurn)
            selectionPrompt.text = "Enemy is thinking...";
        else
            selectionPrompt.text = "";
    }

    void RefreshLog()
    {
        combatLogContainer.Clear();
        foreach (var entry in gm.logEntries)
        {
            var lbl = new Label(entry.msg);
            lbl.AddToClassList("log-entry");
            string typeClass = entry.type == CombatLogType.Ability ? "log-ability" :
                               entry.type == CombatLogType.Injury  ? "log-injury"  :
                               entry.type == CombatLogType.Kill    ? "log-kill"    : "";
            if (!string.IsNullOrEmpty(typeClass)) lbl.AddToClassList(typeClass);
            combatLogContainer.Add(lbl);
        }
    }

    void RefreshGameOver()
    {
        if (gm.gameOver)
        {
            gameOverPanel.RemoveFromClassList("panel-hidden");
            gameOverText.text = gm.playerWon ? "PLAYER VICTORY!" : "DEFEAT\nEnemies Victorious";
            gameOverText.style.color = gm.playerWon
                ? new StyleColor(new Color(1f, 0.9f, 0.1f))
                : new StyleColor(new Color(1f, 0.3f, 0.3f));
        }
        else
        {
            if (!gameOverPanel.ClassListContains("panel-hidden"))
                gameOverPanel.AddToClassList("panel-hidden");
        }
    }
}
