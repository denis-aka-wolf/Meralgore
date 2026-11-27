using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeralgoreRuntimeUI : MonoBehaviour
{
    private const int VisibleLogCount = 5;
    private const float LogLifetime = 8f;
    private const float LogFadeDuration = 3f;

    private readonly List<LogEntry> _logs = new List<LogEntry>();
    private readonly Dictionary<int, UnityEngine.UI.Image> _roomMarkers =
        new Dictionary<int, UnityEngine.UI.Image>();
    private readonly Dictionary<int, FloorMapNode> _nodesById = new Dictionary<int, FloorMapNode>();

    private FloorRunController _runController;
    private MeralgorePlayerState _playerState;
    private TMPro.TMP_FontAsset _font;
    private GameObject _hud;
    private GameObject _titlePanel;
    private GameObject _pausePanel;
    private GameObject _endPanel;
    private TMPro.TextMeshProUGUI _startPrompt;
    private CanvasGroup _transition;
    private Coroutine _transitionRoutine;
    private TMPro.TextMeshProUGUI _healthText;
    private TMPro.TextMeshProUGUI _weaponText;
    private TMPro.TextMeshProUGUI _currencyText;
    private TMPro.TextMeshProUGUI _roomText;
    private TMPro.TextMeshProUGUI _seedText;
    private TMPro.TextMeshProUGUI _itemText;
    private TMPro.TextMeshProUGUI _endText;
    private readonly TMPro.TextMeshProUGUI[] _logLines = new TMPro.TextMeshProUGUI[VisibleLogCount];
    private int _currentRoomId;
    private int _seed;
    private static Sprite _whiteSprite;

    public void Initialize(
        FloorRunController runController,
        IReadOnlyList<FloorMapNode> nodes,
        int seed)
    {
        _runController = runController;
        _playerState = runController.PlayerState;
        _seed = seed;
        foreach (FloorMapNode node in nodes)
            _nodesById[node.Id] = node;

        _font = TMPro.TMP_Settings.defaultFontAsset;
        if (_font == null)
            _font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_font == null)
            Debug.LogError("Meralgore UI could not find a TextMesh Pro font asset.");

        BuildCanvas(nodes);
        SetCurrentRoom(0);
    }

    private void Update()
    {
        if (_startPrompt != null)
        {
            Color color = _startPrompt.color;
            color.a = 0.75f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.25f) * 0.25f;
            _startPrompt.color = color;
        }
    }

    public void SetCurrentRoom(int roomId)
    {
        _currentRoomId = roomId;
        if (_roomText != null)
            _roomText.text = $"ПРОСТРАНСТВО / {roomId + 1:00}";
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
            _endText.text = "ПОБЕДА\nБОСС ПОВЕРЖЕН\nENTER — НОВЫЙ ЗАБЕГ";
        else if (state == MeralgoreRunState.Lost)
            _endText.text = "ЗАБЕГ ЗАВЕРШЁН\nENTER — ПОПРОБОВАТЬ СНОВА";
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
        if (_logs.Count > 20)
            _logs.RemoveAt(0);
        RefreshLogs();
    }

    public void Refresh()
    {
        if (_playerState == null || _healthText == null)
            return;

        _healthText.text = $"ЗДОРОВЬЕ  {Mathf.CeilToInt(_playerState.CurrentHealth):000}";
        _currencyText.text = $"ВАЛЮТА  {_playerState.Currency:000}";
        _roomText.text = _runController.GetRoomLabel();
        _seedText.text = $"SEED  {_seed}";

        WeaponSwitcher weaponSwitcher = _runController.WeaponSwitcher;
        Weapon weapon = weaponSwitcher != null ? weaponSwitcher.weapon : null;
        if (weaponSwitcher != null)
        {
            string ammo = weapon != null && weapon.CurrentStats is IAmmoStats
                ? $" / {weapon.currentAmmo:00}"
                : string.Empty;
            var slots = new string[2];
            for (int i = 0; i < slots.Length; i++)
            {
                Weapon slotWeapon = weaponSwitcher.GetWeaponForSlot(i);
                string slotName = slotWeapon == null
                    ? "--"
                    : slotWeapon.GetType().Name.ToUpperInvariant();
                bool active = weapon != null && weaponSwitcher.CurrentSlot == i;
                slots[i] = $"{(active ? "[" : string.Empty)}{i + 1}:{slotName}{(active ? "]" : string.Empty)}";
            }
            _weaponText.text = $"{string.Join("  /  ", slots)}{ammo}";
        }
        else
        {
            _weaponText.text = "ОРУЖИЕ  --";
        }

        var itemNames = new List<string>();
        foreach (MeralgoreItemType item in _playerState.Items)
            itemNames.Add(GetItemName(item));
        _itemText.text = itemNames.Count == 0
            ? "ПРЕДМЕТЫ  --"
            : "ПРЕДМЕТЫ  " + string.Join("  /  ", itemNames);

        RefreshMap();
        RefreshLogs();
    }

    private void BuildCanvas(IReadOnlyList<FloorMapNode> nodes)
    {
        var canvasObject = new GameObject("MeralgoreCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        UnityEngine.Canvas canvas = canvasObject.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        _hud = CreatePanel(canvasObject.transform, "HUD", new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(0f, 0f, 0f, 0f));

        _healthText = CreateText(_hud.transform, "Health", new Vector2(0f, 1f), new Vector2(38f, -36f),
            new Vector2(320f, 34f), 24f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Left);
        _currencyText = CreateText(_hud.transform, "Currency", new Vector2(0f, 1f), new Vector2(38f, -78f),
            new Vector2(320f, 34f), 21f, MeralgorePalette.Signal, TMPro.TextAlignmentOptions.Left);
        _roomText = CreateText(_hud.transform, "Room", new Vector2(0f, 0f), new Vector2(38f, 34f),
            new Vector2(380f, 32f), 18f, MeralgorePalette.MainRoute, TMPro.TextAlignmentOptions.Left);
        _seedText = CreateText(_hud.transform, "Seed", new Vector2(0f, 0f), new Vector2(38f, 8f),
            new Vector2(380f, 28f), 16f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Left);
        _weaponText = CreateText(_hud.transform, "Weapon", new Vector2(1f, 0f), new Vector2(-38f, 66f),
            new Vector2(430f, 34f), 20f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Right);
        _itemText = CreateText(_hud.transform, "Items", new Vector2(1f, 0f), new Vector2(-38f, 34f),
            new Vector2(650f, 28f), 15f, MeralgorePalette.Signal, TMPro.TextAlignmentOptions.Right);

        CreateMapPanel(_hud.transform, nodes);
        for (int i = 0; i < _logLines.Length; i++)
        {
            _logLines[i] = CreateText(_hud.transform, $"Log_{i}", new Vector2(0f, 0f),
                new Vector2(38f, 130f + i * 27f), new Vector2(760f, 25f),
                16f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Left);
            _logLines[i].alpha = 0f;
        }

        _titlePanel = CreatePanel(canvasObject.transform, "TitlePanel",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(16f / 255f, 18f / 255f, 18f / 255f, 0.78f));
        CreateText(_titlePanel.transform, "Title", new Vector2(0.5f, 0.6f), Vector2.zero,
            new Vector2(900f, 120f), 64f, MeralgorePalette.Signal, TMPro.TextAlignmentOptions.Center)
            .text = "METALGORE";
        CreateText(_titlePanel.transform, "TitleSubhead", new Vector2(0.5f, 0.48f), Vector2.zero,
            new Vector2(820f, 54f), 20f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center)
            .text = "20 ОТКРЫТЫХ ПРОСТРАНСТВ / НАЙДИ ПУТЬ К БОССУ";
        CreateText(_titlePanel.transform, "Controls", new Vector2(0.5f, 0.34f), Vector2.zero,
            new Vector2(900f, 146f), 18f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center)
            .text = "WASD — ДВИЖЕНИЕ    SPACE — ПРЫЖОК    CTRL — ПРИСЕСТЬ / СЛАЙД\nЛКМ — ОГОНЬ    ПКМ — ОСОБАЯ АТАКА    R — ПЕРЕЗАРЯДКА\nE — ВЗАИМОДЕЙСТВИЕ    F — УДАР    1–2 — СЛОТЫ ОРУЖИЯ\nESC — ПАУЗА";
        _startPrompt = CreateText(_titlePanel.transform, "StartPrompt", new Vector2(0.5f, 0.2f), Vector2.zero,
            new Vector2(700f, 40f), 19f, MeralgorePalette.Signal, TMPro.TextAlignmentOptions.Center);
        _startPrompt.text = "Нажмите ENTER, чтобы начать";
        CreateText(_titlePanel.transform, "TitleSeed", new Vector2(0.5f, 0.13f), Vector2.zero,
            new Vector2(500f, 32f), 16f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center)
            .text = $"SEED  {_seed}";

        _pausePanel = CreatePanel(canvasObject.transform, "PausePanel",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(16f / 255f, 18f / 255f, 18f / 255f, 0.82f));
        CreateText(_pausePanel.transform, "PauseTitle", new Vector2(0.5f, 0.55f), Vector2.zero,
            new Vector2(700f, 90f), 44f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center)
            .text = "ПАУЗА\nESC — ПРОДОЛЖИТЬ";

        _endPanel = CreatePanel(canvasObject.transform, "EndPanel",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(16f / 255f, 18f / 255f, 18f / 255f, 0.88f));
        _endText = CreateText(_endPanel.transform, "EndText", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(800f, 180f), 36f, MeralgorePalette.Text, TMPro.TextAlignmentOptions.Center);

        Color transitionColor = MeralgorePalette.Shadow;
        transitionColor.a = 1f;
        GameObject transitionObject = CreatePanel(canvasObject.transform, "Transition",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            transitionColor);
        _transition = transitionObject.AddComponent<CanvasGroup>();
        _transition.interactable = false;
        _transition.blocksRaycasts = false;
    }

    private IEnumerator FadeTransition()
    {
        const float totalDuration = 0.3f;
        const float halfDuration = totalDuration * 0.5f;
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

    private void CreateMapPanel(Transform parent, IReadOnlyList<FloorMapNode> nodes)
    {
        GameObject panel = CreatePanel(parent, "MapPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-25f, -25f), new Vector2(218f, 448f), new Color(16f / 255f, 18f / 255f, 18f / 255f, 0.76f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.pivot = new Vector2(1f, 1f);
        CreateText(panel.transform, "MapTitle", new Vector2(0f, 1f), new Vector2(16f, -15f),
            new Vector2(190f, 30f), 16f, MeralgorePalette.Signal, TMPro.TextAlignmentOptions.Left)
            .text = "МАРШРУТ  /  БОСС ↑";

        var nodePositions = new Dictionary<int, Vector2>();
        foreach (FloorMapNode node in nodes)
        {
            nodePositions[node.Id] = new Vector2(42f + (node.Lane + 1) * 62f, 58f + node.Row * 48f);
        }

        foreach (FloorMapNode node in nodes)
        {
            foreach (int connectedId in node.Connections)
            {
                if (connectedId <= node.Id)
                    continue;

                Vector2 from = nodePositions[node.Id];
                Vector2 to = nodePositions[connectedId];
                Vector2 delta = to - from;
                Color pathColor = MeralgorePalette.MainRoute;
                pathColor.a = 0.55f;
                GameObject line = CreatePanel(panel.transform, $"Path_{node.Id}_{connectedId}",
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    (from + to) * 0.5f, new Vector2(delta.magnitude, 2f), pathColor);
                line.GetComponent<RectTransform>().rotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        foreach (FloorMapNode node in nodes)
        {
            GameObject marker = CreatePanel(panel.transform, $"Node_{node.Id}",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                nodePositions[node.Id], new Vector2(14f, 14f), GetRoomColor(node.Type));
            _roomMarkers[node.Id] = marker.GetComponent<UnityEngine.UI.Image>();
        }
    }

    private GameObject CreatePanel(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        var panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private TMPro.TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TMPro.TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TMPro.TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMPro.TextMeshProUGUI text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.characterSpacing = 1.5f;
        text.outlineColor = MeralgorePalette.Shadow;
        text.outlineWidth = 0.16f;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private void RefreshMap()
    {
        if (_runController == null)
            return;

        foreach (KeyValuePair<int, UnityEngine.UI.Image> marker in _roomMarkers)
        {
            FloorMapNode node = FindNode(marker.Key);
            Color color = node != null ? GetRoomColor(node.Type) : MeralgorePalette.Structure;
            if (marker.Key == _currentRoomId)
                color = MeralgorePalette.Signal;
            else if (_runController.IsRoomAvailable(marker.Key))
                color = MeralgorePalette.Text;
            else if (!_runController.IsRoomVisited(marker.Key))
                color.a = 0.42f;
            marker.Value.color = color;
        }
    }

    private FloorMapNode FindNode(int id)
    {
        return _nodesById.TryGetValue(id, out FloorMapNode node) ? node : null;
    }

    private void RefreshLogs()
    {
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
            case MeralgoreItemType.FlowerSection: return "ЦВЕТОЧНОЕ СЕЧЕНИЕ";
            case MeralgoreItemType.WingedKitten: return "КОТЁНОК С КРЫЛЬЯМИ";
            case MeralgoreItemType.Devotion: return "ПРЕДАННОСТЬ";
            case MeralgoreItemType.Meloni: return "МЭЛОНИ";
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
