using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MeralgoreRuntimeUI : MonoBehaviour
{
    private const int VisibleLogCount = 4;
    private const float LogLifetime = 10f;
    private const float LogFadeDuration = 2.5f;

    private readonly List<LogEntry> _logs = new List<LogEntry>();
    private readonly Dictionary<int, Image> _roomMarkers = new Dictionary<int, Image>();
    private readonly Dictionary<int, FloorMapNode> _nodesById = new Dictionary<int, FloorMapNode>();

    private FloorRunController _runController;
    private MeralgorePlayerState _playerState;
    private TMPro.TMP_FontAsset _font;
    private GameObject _hud;
    private GameObject _titlePanel;
    private GameObject _pausePanel;
    private GameObject _endPanel;
    private TMPro.TextMeshProUGUI _startPrompt;
    private TMPro.TextMeshProUGUI _endText;
    private CanvasGroup _transition;
    private Coroutine _transitionRoutine;

    private TMPro.TextMeshProUGUI _healthValue;
    private TMPro.TextMeshProUGUI _healthState;
    private TMPro.TextMeshProUGUI _currencyValue;
    private TMPro.TextMeshProUGUI _roomValue;
    private TMPro.TextMeshProUGUI _sectorValue;
    private TMPro.TextMeshProUGUI _weaponName;
    private TMPro.TextMeshProUGUI _ammoValue;
    private TMPro.TextMeshProUGUI _weaponSlots;
    private TMPro.TextMeshProUGUI _itemValue;
    private TMPro.TextMeshProUGUI _companionState;
    private TMPro.TextMeshProUGUI _companionDetail;
    private TMPro.TextMeshProUGUI[] _logLines = new TMPro.TextMeshProUGUI[VisibleLogCount];
    private Image _healthFill;
    private int _currentRoomId;
    private int _seed;
    private float _nextRefreshTime;
    private static Sprite _whiteSprite;

    private static readonly Color PanelColor = new Color32(16, 18, 18, 224);
    private static readonly Color PanelSoft = new Color32(25, 29, 28, 205);
    private static readonly Color LineColor = new Color32(69, 74, 71, 220);
    private static readonly Color MutedText = new Color32(139, 151, 145, 255);
    private static readonly Color Teal = new Color32(77, 122, 120, 255);
    private static readonly Color Amber = new Color32(179, 115, 20, 255);

    public void Initialize(FloorRunController runController, IReadOnlyList<FloorMapNode> nodes, int seed)
    {
        _runController = runController;
        _playerState = runController.PlayerState;
        _seed = seed;

        foreach (FloorMapNode node in nodes)
            _nodesById[node.Id] = node;

        _font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/TMP_AnkaCoder-C87-r");
        if (_font == null)
            _font = TMPro.TMP_Settings.defaultFontAsset;
        if (_font == null)
            _font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_font == null)
            Debug.LogError("Meralgore UI could not find a TextMesh Pro font asset.");

        BuildCanvas(nodes);
        SetCurrentRoom(0);

        if (_playerState != null)
            _playerState.Changed += OnPlayerStateChanged;

        AddLog("OPERATOR LINK ESTABLISHED");
        AddLog("NAVIGATION GRAPH SYNCHRONIZED");
        AddLog("PRIMARY SYSTEMS NOMINAL");
        Refresh();
    }

    private void OnDestroy()
    {
        if (_playerState != null)
            _playerState.Changed -= OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged()
    {
        Refresh();
    }

    private void Update()
    {
        if (_startPrompt != null)
        {
            Color color = _startPrompt.color;
            color.a = 0.72f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.22f;
            _startPrompt.color = color;
        }

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + 0.12f;
            Refresh();
        }
    }

    public void SetCurrentRoom(int roomId)
    {
        _currentRoomId = roomId;
        if (_roomValue != null)
            _roomValue.text = $"SPACE {roomId + 1:00}";
        RefreshMap();
    }

    public void SetState(MeralgoreRunState state)
    {
        if (_titlePanel == null)
            return;

        _titlePanel.SetActive(state == MeralgoreRunState.Title);
        _pausePanel.SetActive(state == MeralgoreRunState.Paused);
        _endPanel.SetActive(state == MeralgoreRunState.Won || state == MeralgoreRunState.Lost);
        _hud.SetActive(state == MeralgoreRunState.Playing || state == MeralgoreRunState.Paused);

        if (state == MeralgoreRunState.Won)
            _endText.text = "RUN COMPLETE\nBOSS NEUTRALIZED\n\nENTER  /  NEW RUN";
        else if (state == MeralgoreRunState.Lost)
            _endText.text = "OPERATOR TERMINATED\nRUN DATA SEALED\n\nENTER  /  RETRY";

        if (_transition != null)
        {
            if (_transitionRoutine != null)
                StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(FadeTransition());
        }

        Refresh();
    }

    public void AddLog(string message)
    {
        _logs.Add(new LogEntry($"// {message}", Time.unscaledTime));
        if (_logs.Count > 24)
            _logs.RemoveAt(0);
        RefreshLogs();
    }

    public void Refresh()
    {
        if (_playerState == null || _healthValue == null || _runController == null)
            return;

        float health = Mathf.Clamp(_playerState.CurrentHealth, 0f, _playerState.MaxHealth);
        float healthRatio = _playerState.MaxHealth > 0f ? health / _playerState.MaxHealth : 0f;
        _healthValue.text = Mathf.CeilToInt(health).ToString("000");
        _healthState.text = healthRatio <= 0.25f ? "CRITICAL" : healthRatio <= 0.55f ? "DEGRADED" : "NOMINAL";
        _healthState.color = healthRatio <= 0.25f ? MeralgorePalette.Boss : healthRatio <= 0.55f ? Amber : Teal;
        if (_healthFill != null)
            _healthFill.fillAmount = healthRatio;

        _currencyValue.text = _playerState.Currency.ToString("000");
        _roomValue.text = _runController.GetRoomLabel();
        _sectorValue.text = $"CHAPTER 01  /  FLOOR 01     //     SEED {_seed}";

        WeaponSwitcher weaponSwitcher = _runController.WeaponSwitcher;
        Weapon weapon = weaponSwitcher != null ? weaponSwitcher.weapon : null;
        if (weaponSwitcher != null)
        {
            _weaponName.text = weapon != null
                ? weapon.GetType().Name.Replace("Weapon", "").Replace("_", " ").ToUpperInvariant()
                : "NO WEAPON EQUIPPED";

            IAmmoStats ammoStats = weapon != null ? weapon.CurrentStats as IAmmoStats : null;
            _ammoValue.text = ammoStats != null
                ? $"{weapon.currentAmmo:00}  <color=#8B9791>/</color>  RESERVE --"
                : "--  <color=#8B9791>/</color>  NO AMMO DATA";

            var slots = new string[2];
            for (int i = 0; i < slots.Length; i++)
            {
                Weapon slotWeapon = weaponSwitcher.GetWeaponForSlot(i);
                string slotName = slotWeapon == null ? "EMPTY" :
                    slotWeapon.GetType().Name.Replace("Weapon", "").ToUpperInvariant();
                bool active = weapon != null && weaponSwitcher.CurrentSlot == i;
                slots[i] = active ? $"<color=#B37314>[ {i + 1}  {slotName} ]</color>" : $"{i + 1}  {slotName}";
            }
            _weaponSlots.text = string.Join("     ", slots);
        }
        else
        {
            _weaponName.text = "WEAPON SYSTEM OFFLINE";
            _ammoValue.text = "--";
            _weaponSlots.text = "01  EMPTY     02  EMPTY";
        }

        var itemNames = new List<string>();
        foreach (MeralgoreItemType item in _playerState.Items)
            itemNames.Add(GetItemName(item));
        _itemValue.text = itemNames.Count == 0 ? "NO ACTIVE MODIFIERS" : string.Join("   /   ", itemNames);

        RefreshMap();
        RefreshLogs();
    }

    private void BuildCanvas(IReadOnlyList<FloorMapNode> nodes)
    {
        GameObject canvasObject = new GameObject("MeralgoreCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        canvasObject.AddComponent<GraphicRaycaster>();

        _hud = CreatePanel(canvasObject.transform, "GameplayHUD", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));

        BuildHeader(_hud.transform);
        BuildOperatorPanel(_hud.transform);
        BuildTacticalReticle(_hud.transform);
        BuildArmamentPanel(_hud.transform);
        BuildNavigationPanel(_hud.transform, nodes);
        BuildCompanionPanel(_hud.transform);
        BuildEventLog(_hud.transform);
        BuildModifierStrip(_hud.transform);

        _titlePanel = CreatePanel(canvasObject.transform, "TitlePanel", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(8f / 255f, 10f / 255f, 10f / 255f, 0.94f));
        CreateText(_titlePanel.transform, "TitleEyebrow", new Vector2(0.5f, 0.68f), Vector2.zero,
            new Vector2(900f, 28f), 14f, Teal, TMPro.TextAlignmentOptions.Center).text =
            "M E G A S T R U C T U R E   /   COMBAT OPERATING SYSTEM";
        CreateText(_titlePanel.transform, "Title", new Vector2(0.5f, 0.56f), Vector2.zero,
            new Vector2(1000f, 120f), 76f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center).text = "METALGORE";
        CreateText(_titlePanel.transform, "TitleSubhead", new Vector2(0.5f, 0.46f), Vector2.zero,
            new Vector2(900f, 40f), 18f, Amber, TMPro.TextAlignmentOptions.Center).text =
            "CHAPTER 01  //  FLOOR 01  //  DESCENT PROTOCOL";
        CreateText(_titlePanel.transform, "Controls", new Vector2(0.5f, 0.32f), Vector2.zero,
            new Vector2(1000f, 112f), 16f, MutedText, TMPro.TextAlignmentOptions.Center).text =
            "WASD  MOVE     SPACE  JUMP     CTRL  CROUCH / SLIDE\n" +
            "LMB  FIRE     RMB  ALT FIRE     R  RELOAD     1-2  WEAPON SLOT\n" +
            "E  INTERACT     F  MELEE     ESC  SYSTEM MENU";
        _startPrompt = CreateText(_titlePanel.transform, "StartPrompt", new Vector2(0.5f, 0.19f), Vector2.zero,
            new Vector2(700f, 36f), 18f, Amber, TMPro.TextAlignmentOptions.Center);
        _startPrompt.text = "PRESS ENTER TO INITIALIZE";
        CreateText(_titlePanel.transform, "TitleFooter", new Vector2(0.5f, 0.09f), Vector2.zero,
            new Vector2(800f, 24f), 12f, MutedText, TMPro.TextAlignmentOptions.Center).text =
            "UNAUTHORIZED ACCESS  //  OPERATOR PROFILE NOT FOUND";

        _pausePanel = CreatePanel(canvasObject.transform, "PausePanel", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(8f / 255f, 10f / 255f, 10f / 255f, 0.92f));
        CreateText(_pausePanel.transform, "PauseEyebrow", new Vector2(0.5f, 0.59f), Vector2.zero,
            new Vector2(700f, 28f), 14f, Amber, TMPro.TextAlignmentOptions.Center).text = "SYSTEM SUSPENDED";
        CreateText(_pausePanel.transform, "PauseTitle", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(700f, 90f), 48f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center).text = "PAUSED";
        CreateText(_pausePanel.transform, "PauseHint", new Vector2(0.5f, 0.41f), Vector2.zero,
            new Vector2(700f, 36f), 16f, MutedText, TMPro.TextAlignmentOptions.Center).text = "ESC  /  RESUME OPERATION";

        _endPanel = CreatePanel(canvasObject.transform, "EndPanel", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(8f / 255f, 10f / 255f, 10f / 255f, 0.95f));
        _endText = CreateText(_endPanel.transform, "EndText", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(900f, 240f), 34f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center);

        GameObject transitionObject = CreatePanel(canvasObject.transform, "Transition",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(8f / 255f, 10f / 255f, 10f / 255f, 1f));
        _transition = transitionObject.AddComponent<CanvasGroup>();
        _transition.interactable = false;
        _transition.blocksRaycasts = false;
    }

    private void BuildHeader(Transform parent)
    {
        GameObject frame = CreateFrame(parent, "StructureHeader", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(1852f, 58f), false);
        CreateText(frame.transform, "Brand", new Vector2(0f, 0.5f), new Vector2(18f, 0f),
            new Vector2(360f, 28f), 17f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Left).text =
            "METALGORE  <color=#4D7A78>//</color>  COS";
        _sectorValue = CreateText(frame.transform, "Sector", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(780f, 24f), 13f, MutedText, TMPro.TextAlignmentOptions.Center);
        _sectorValue.text = "CHAPTER 01  /  FLOOR 01";
        CreateText(frame.transform, "SystemStatus", new Vector2(1f, 0.5f), new Vector2(-18f, 0f),
            new Vector2(320f, 24f), 13f, Teal, TMPro.TextAlignmentOptions.Right).text =
            "●  SYSTEM LINK  /  STABLE";
        CreateAccent(frame.transform, "HeaderAccent", new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(84f, 2f), Amber);
    }

    private void BuildOperatorPanel(Transform parent)
    {
        GameObject frame = CreateFrame(parent, "OperatorPanel", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(34f, -112f), new Vector2(300f, 166f), true);
        AddSectionTitle(frame.transform, "OPERATOR STATUS", "01 / VITALS");
        _healthValue = CreateText(frame.transform, "HealthValue", new Vector2(0f, 1f),
            new Vector2(18f, -54f), new Vector2(90f, 48f), 34f, MeralgorePalette.Text,
            TMPro.TextAlignmentOptions.Left);
        _healthValue.text = "100";
        CreateText(frame.transform, "HealthUnit", new Vector2(0f, 1f),
            new Vector2(105f, -62f), new Vector2(80f, 20f), 11f, MutedText,
            TMPro.TextAlignmentOptions.Left).text = " / 100 HP";
        _healthState = CreateText(frame.transform, "HealthState", new Vector2(1f, 1f),
            new Vector2(-16f, -64f), new Vector2(105f, 20f), 11f, Teal,
            TMPro.TextAlignmentOptions.Right);
        _healthState.text = "NOMINAL";

        GameObject barBack = CreatePanel(frame.transform, "HealthBarBack", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(18f, -91f), new Vector2(264f, 7f), LineColor);
        _healthFill = CreatePanel(barBack.transform, "HealthFill", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, Teal).GetComponent<Image>();
        _healthFill.type = Image.Type.Filled;
        _healthFill.fillMethod = Image.FillMethod.Horizontal;
        _healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _healthFill.fillAmount = 1f;

        CreateText(frame.transform, "CurrencyLabel", new Vector2(0f, 0f),
            new Vector2(18f, 22f), new Vector2(110f, 20f), 11f, MutedText,
            TMPro.TextAlignmentOptions.Left).text = "CREDITS";
        _currencyValue = CreateText(frame.transform, "CurrencyValue", new Vector2(1f, 0f),
            new Vector2(-18f, 22f), new Vector2(130f, 22f), 17f, Amber,
            TMPro.TextAlignmentOptions.Right);
        _currencyValue.text = "000";
    }

    private void BuildTacticalReticle(Transform parent)
    {
        GameObject reticleObject = new GameObject("TacticalReticle", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(MeralgoreHUDReticleGraphic));
        reticleObject.transform.SetParent(parent, false);

        RectTransform rect = reticleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(92f, 92f);

        MeralgoreHUDReticleGraphic graphic = reticleObject.GetComponent<MeralgoreHUDReticleGraphic>();
        graphic.color = new Color32(211, 221, 215, 210);
        graphic.raycastTarget = false;

        CreateText(parent, "TacticalMode", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -58f), new Vector2(300f, 18f), 10f, MutedText,
            TMPro.TextAlignmentOptions.Center).text = "TARGET ACQUISITION  /  STANDBY";
    }

    private void BuildArmamentPanel(Transform parent)
    {
        GameObject frame = CreateFrame(parent, "ArmamentPanel", new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-34f, 32f), new Vector2(390f, 154f), true);
        AddSectionTitle(frame.transform, "ARMAMENT", "02 / ACTIVE LOADOUT");
        _weaponName = CreateText(frame.transform, "WeaponName", new Vector2(0f, 1f),
            new Vector2(18f, -56f), new Vector2(350f, 26f), 17f, MeralgorePalette.Text,
            TMPro.TextAlignmentOptions.Left);
        _weaponName.text = "WEAPON SYSTEM";
        _ammoValue = CreateText(frame.transform, "AmmoValue", new Vector2(0f, 1f),
            new Vector2(18f, -91f), new Vector2(350f, 34f), 23f, Amber,
            TMPro.TextAlignmentOptions.Left);
        _ammoValue.text = "-- / --";
        _weaponSlots = CreateText(frame.transform, "WeaponSlots", new Vector2(0f, 0f),
            new Vector2(18f, 18f), new Vector2(350f, 20f), 11f, MutedText,
            TMPro.TextAlignmentOptions.Left);
        _weaponSlots.text = "01 EMPTY     02 EMPTY";
    }

    private void BuildNavigationPanel(Transform parent, IReadOnlyList<FloorMapNode> nodes)
    {
        GameObject frame = CreateFrame(parent, "NavigationPanel", new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-34f, -112f), new Vector2(292f, 356f), true);
        AddSectionTitle(frame.transform, "NAVIGATION", "03 / FLOOR GRAPH");
        CreateText(frame.transform, "MapLegend", new Vector2(0f, 1f),
            new Vector2(18f, -43f), new Vector2(250f, 18f), 9f, MutedText,
            TMPro.TextAlignmentOptions.Left).text = "● CURRENT   ○ AVAILABLE   · UNKNOWN";

        const float mapWidth = 270f;
        const float mapHeight = 270f;
        float mapTop = -76f;
        var positions = new Dictionary<int, Vector2>();
        int maxRow = 1;
        foreach (FloorMapNode node in nodes)
            maxRow = Mathf.Max(maxRow, node.Row);

        foreach (FloorMapNode node in nodes)
        {
            float x = 18f + (node.Lane + 1) * (mapWidth * 0.5f);
            float y = mapTop - 8f - node.Row * (mapHeight / maxRow);
            positions[node.Id] = new Vector2(x, y);
        }

        foreach (FloorMapNode node in nodes)
        {
            foreach (int connectedId in node.Connections)
            {
                if (connectedId <= node.Id || !positions.ContainsKey(connectedId))
                    continue;

                Vector2 from = positions[node.Id];
                Vector2 to = positions[connectedId];
                Vector2 delta = to - from;
                GameObject line = CreatePanel(frame.transform, $"Route_{node.Id}_{connectedId}",
                    new Vector2(0f, 1f), new Vector2(0f, 1f), (from + to) * 0.5f,
                    new Vector2(delta.magnitude, 2f), new Color(Teal.r, Teal.g, Teal.b, 0.42f));
                line.GetComponent<RectTransform>().rotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        foreach (FloorMapNode node in nodes)
        {
            GameObject marker = CreatePanel(frame.transform, $"Node_{node.Id}",
                new Vector2(0f, 1f), new Vector2(0f, 1f), positions[node.Id], new Vector2(13f, 13f), GetRoomColor(node.Type));
            marker.GetComponent<RectTransform>().sizeDelta = node.Type == FloorRoomType.Boss
                ? new Vector2(19f, 19f) : new Vector2(12f, 12f);
            _roomMarkers[node.Id] = marker.GetComponent<Image>();
        }

        _roomValue = CreateText(frame.transform, "RoomValue", new Vector2(0f, 0f),
            new Vector2(18f, 17f), new Vector2(250f, 20f), 12f, Amber,
            TMPro.TextAlignmentOptions.Left);
        _roomValue.text = "SPACE 01 / START";
    }

    private void BuildCompanionPanel(Transform parent)
    {
        GameObject frame = CreateFrame(parent, "CompanionPanel", new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(34f, 32f), new Vector2(300f, 88f), false);
        AddSectionTitle(frame.transform, "COMPANION", "OPTIONAL MODULE");
        _companionState = CreateText(frame.transform, "CompanionState", new Vector2(0f, 0f),
            new Vector2(18f, 15f), new Vector2(140f, 20f), 12f, MutedText,
            TMPro.TextAlignmentOptions.Left);
        _companionState.text = "NOT INSTALLED";
        _companionDetail = CreateText(frame.transform, "CompanionDetail", new Vector2(1f, 0f),
            new Vector2(-18f, 15f), new Vector2(110f, 20f), 10f, MutedText,
            TMPro.TextAlignmentOptions.Right);
        _companionDetail.text = "ANALYSIS N/A";
    }

    private void BuildEventLog(Transform parent)
    {
        GameObject frame = CreateFrame(parent, "EventLogPanel", new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(354f, 148f), new Vector2(610f, 128f), false);
        AddSectionTitle(frame.transform, "SYSTEM EVENT LOG", "LIVE TELEMETRY");
        for (int i = 0; i < _logLines.Length; i++)
        {
            _logLines[i] = CreateText(frame.transform, $"Log_{i}", new Vector2(0f, 1f),
                new Vector2(18f, -42f - i * 19f), new Vector2(570f, 18f), 10f,
                i == _logLines.Length - 1 ? MeralgorePalette.Text : MutedText,
                TMPro.TextAlignmentOptions.Left);
            _logLines[i].alpha = 0f;
        }
    }

    private void BuildModifierStrip(Transform parent)
    {
        GameObject frame = CreateFrame(parent, "ModifierStrip", new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-34f, 210f), new Vector2(390f, 46f), false);
        CreateText(frame.transform, "ModifierLabel", new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(90f, 18f), 9f, MutedText,
            TMPro.TextAlignmentOptions.Left).text = "MODIFIERS";
        _itemValue = CreateText(frame.transform, "ModifierValue", new Vector2(1f, 0.5f),
            new Vector2(-14f, 0f), new Vector2(310f, 24f), 10f, MeralgorePalette.Text,
            TMPro.TextAlignmentOptions.Right);
        _itemValue.text = "NO ACTIVE MODIFIERS";
    }

    private GameObject CreateFrame(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, bool showAccent)
    {
        GameObject frame = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(MeralgoreHUDFrameGraphic));
        frame.transform.SetParent(parent, false);

        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        MeralgoreHUDFrameGraphic graphic = frame.GetComponent<MeralgoreHUDFrameGraphic>();
        graphic.SetPalette(PanelColor, LineColor, showAccent ? Teal : new Color32(92, 103, 98, 150));
        graphic.raycastTarget = false;
        return frame;
    }

    private void AddSectionTitle(Transform parent, string title, string code)
    {
        CreateText(parent, title.Replace(" ", "") + "Label", new Vector2(0f, 1f),
            new Vector2(16f, -15f), new Vector2(190f, 20f), 11f, MeralgorePalette.Text,
            TMPro.TextAlignmentOptions.Left).text = title;
        CreateText(parent, title.Replace(" ", "") + "Code", new Vector2(1f, 1f),
            new Vector2(-16f, -15f), new Vector2(130f, 18f), 9f, MutedText,
            TMPro.TextAlignmentOptions.Right).text = code;
        CreateAccent(parent, title.Replace(" ", "") + "Divider", new Vector2(0f, 1f),
            new Vector2(1f, 1f), new Vector2(0f, -31f), new Vector2(0f, 1f), LineColor);
    }

    private GameObject CreatePanel(Transform parent, string objectName, Vector2 anchorMin,
        Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private void CreateAccent(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject line = CreatePanel(parent, name, anchorMin, anchorMax, position, size, color);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.pivot = new Vector2(anchorMin.x == 1f ? 1f : 0f, anchorMin.y == 1f ? 1f : 0f);
    }

    private TMPro.TextMeshProUGUI CreateText(Transform parent, string objectName, Vector2 anchor,
        Vector2 position, Vector2 size, float fontSize, Color color,
        TMPro.TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMPro.TextMeshProUGUI text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = Mathf.Max(fontSize * 1.08f, 11f);
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = fontSize >= 20f ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
        text.characterSpacing = fontSize <= 12f ? 1.05f : fontSize <= 17f ? 0.55f : 0.15f;
        text.outlineColor = MeralgorePalette.Shadow;
        text.outlineWidth = 0.12f;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        return text;
    }

    private void RefreshMap()
    {
        if (_runController == null)
            return;

        foreach (KeyValuePair<int, Image> marker in _roomMarkers)
        {
            FloorMapNode node = FindNode(marker.Key);
            Color color = node != null ? GetRoomColor(node.Type) : MeralgorePalette.Structure;
            if (marker.Key == _currentRoomId)
                color = Amber;
            else if (_runController.IsRoomAvailable(marker.Key))
                color = MeralgorePalette.Text;
            else if (!_runController.IsRoomVisited(marker.Key))
                color.a = 0.24f;
            else
                color.a = 0.62f;

            marker.Value.color = color;
        }
    }

    private FloorMapNode FindNode(int id)
    {
        return _nodesById.TryGetValue(id, out FloorMapNode node) ? node : null;
    }

    private void RefreshLogs()
    {
        if (_logLines == null)
            return;

        float now = Time.unscaledTime;
        int visible = Mathf.Min(VisibleLogCount, _logs.Count);
        for (int i = 0; i < _logLines.Length; i++)
        {
            int logIndex = _logs.Count - visible + i;
            if (i >= visible || logIndex < 0)
            {
                _logLines[i].text = string.Empty;
                _logLines[i].alpha = 0f;
                continue;
            }

            LogEntry entry = _logs[logIndex];
            float age = now - entry.Time;
            _logLines[i].text = entry.Text;
            _logLines[i].alpha = age <= LogLifetime - LogFadeDuration
                ? 1f
                : Mathf.Clamp01((LogLifetime - age) / LogFadeDuration);
        }
    }

    private IEnumerator FadeTransition()
    {
        const float halfDuration = 0.15f;
        float startAlpha = _transition.alpha;
        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
        {
            _transition.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / halfDuration);
            yield return null;
        }

        _transition.alpha = 1f;
        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
        {
            _transition.alpha = Mathf.Lerp(1f, 0f, elapsed / halfDuration);
            yield return null;
        }

        _transition.alpha = 0f;
        _transitionRoutine = null;
    }

    private static Color GetRoomColor(FloorRoomType type)
    {
        switch (type)
        {
            case FloorRoomType.Start:
            case FloorRoomType.Combat:
                return MeralgorePalette.MainRoute;
            case FloorRoomType.Shop:
            case FloorRoomType.Golden:
                return MeralgorePalette.Signal;
            case FloorRoomType.Cursed:
            case FloorRoomType.Boss:
                return MeralgorePalette.Boss;
            default:
                return MeralgorePalette.Structure;
        }
    }

    private static string GetItemName(MeralgoreItemType item)
    {
        switch (item)
        {
            case MeralgoreItemType.FlowerSection: return "FLOWER SECTION";
            case MeralgoreItemType.WingedKitten: return "WINGED KITTEN";
            case MeralgoreItemType.Devotion: return "DEVOTION";
            case MeralgoreItemType.Meloni: return "MELONI";
            default: return item.ToString().ToUpperInvariant();
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite == null)
            _whiteSprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }

    private readonly struct LogEntry
    {
        public readonly string Text;
        public readonly float Time;

        public LogEntry(string text, float time)
        {
            Text = text;
            Time = time;
        }
    }
}
