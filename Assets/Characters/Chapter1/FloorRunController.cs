using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum MeralgoreRunState
{
    Title,
    Playing,
    Paused,
    Won,
    Lost
}

public class FloorRunController : MonoBehaviour
{
    private readonly HashSet<int> _visitedRooms = new HashSet<int>();
    private readonly HashSet<int> _usedSpecialRooms = new HashSet<int>();
    private readonly HashSet<Health> _activeEnemies = new HashSet<Health>();

    private Chapter1Floor1Generator _generator;
    private MeralgoreRuntimeUI _ui;
    private PlayerMovement _movement;
    private PlayerInputController _inputController;
    private WeaponSwitcher _weaponSwitcher;
    private FloorMapRoom _currentRoom;
    private System.Random _random;
    private MeralgoreRunState _state = MeralgoreRunState.Title;

    public MeralgorePlayerState PlayerState { get; private set; }
    public bool IsPlaying => _state == MeralgoreRunState.Playing;
    public MeralgoreRunState State => _state;
    public int CurrentSeed => _generator != null ? _generator.CurrentSeed : 0;
    public FloorMapRoom CurrentRoom => _currentRoom;
    public WeaponSwitcher WeaponSwitcher => _weaponSwitcher;
    public Transform PlayerTransform => _movement != null ? _movement.transform : null;

    public void Initialize(Chapter1Floor1Generator generator)
    {
        _generator = generator;
        _random = new System.Random(generator.CurrentSeed ^ 0x4D455441);

        foreach (FloorMapRoom room in generator.Rooms)
            room.SetRunController(this);

        CreateUpperPickups();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null)
        {
            Debug.LogError("Meralgore run could not start: GameObject 'Player' was not found.");
            enabled = false;
            return;
        }

        _movement = playerObject.GetComponent<PlayerMovement>();
        _inputController = playerObject.GetComponent<PlayerInputController>();
        _weaponSwitcher = playerObject.GetComponentInChildren<WeaponSwitcher>(true);
        PlayerState = playerObject.GetComponent<MeralgorePlayerState>();
        if (PlayerState == null)
            PlayerState = playerObject.AddComponent<MeralgorePlayerState>();
        PlayerState.Initialize(_movement);

        DebugUI debugUI = UnityEngine.Object.FindAnyObjectByType<DebugUI>();
        if (debugUI != null)
            debugUI.SetDebugVisible(false);

        if (_generator.PlayerSpawn != null)
            TeleportPlayer(_generator.PlayerSpawn.position + Vector3.up * 1.2f);

        _currentRoom = _generator.Rooms[0];
        _visitedRooms.Add(_currentRoom.Node.Id);

        GameObject uiObject = new GameObject("MeralgoreRuntimeUI");
        _ui = uiObject.AddComponent<MeralgoreRuntimeUI>();
        _ui.Initialize(this, _generator.Nodes, _generator.CurrentSeed);
        _ui?.SetCurrentRoom(_currentRoom.Node.Id);
        SetState(MeralgoreRunState.Title);
        Log($"Seed забега: {CurrentSeed}");
    }

    private void Update()
    {
        if (PlayerState == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (_state == MeralgoreRunState.Playing && PlayerState.CurrentHealth <= 0f)
        {
            EndRun(false);
            return;
        }

        if (_state == MeralgoreRunState.Title && keyboard.enterKey.wasPressedThisFrame)
        {
            SetState(MeralgoreRunState.Playing);
            Log("Забег начался. Найдите путь к боссу.");
        }
        else if (_state == MeralgoreRunState.Playing)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                SetState(MeralgoreRunState.Paused);
            else if (keyboard.eKey.wasPressedThisFrame)
                Interact();
        }
        else if (_state == MeralgoreRunState.Paused && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetState(MeralgoreRunState.Playing);
        }
        else if ((_state == MeralgoreRunState.Won || _state == MeralgoreRunState.Lost) &&
                 keyboard.enterKey.wasPressedThisFrame)
        {
            RestartRun();
        }

    }

    public void EnterRoom(FloorMapRoom room)
    {
        if (!IsPlaying || room == null || room.Node == null || _currentRoom == room)
            return;

        _currentRoom = room;
        _visitedRooms.Add(room.Node.Id);
        _ui?.SetCurrentRoom(room.Node.Id);

        if (room.IsCleared || _usedSpecialRooms.Contains(room.Node.Id))
            return;

        switch (room.Node.Type)
        {
            case FloorRoomType.Combat:
                StartEncounter(room, _random.Next(2, 5), false);
                break;
            case FloorRoomType.Boss:
                Log("Арена босса. Победите его, чтобы завершить забег.");
                StartEncounter(room, 1, true);
                break;
            case FloorRoomType.Shop:
                Log("Магазин: E — купить предмет за 15 единиц валюты.");
                break;
            case FloorRoomType.Golden:
                _usedSpecialRooms.Add(room.Node.Id);
                CreatePickup(room.transform.position + Vector3.up * 1.3f, RandomItem());
                Log("В этом пространстве вас ждёт предмет.");
                break;
            case FloorRoomType.Cursed:
                Log("Проклятое пространство: E — заплатить 10 за неизвестный исход.");
                break;
        }
    }

    public void Log(string message)
    {
        _ui?.AddLog(message);
    }

    public string GetWeaponLabel(int slot)
    {
        Weapon weapon = _weaponSwitcher != null ? _weaponSwitcher.GetWeaponForSlot(slot) : null;
        return weapon == null ? "EMPTY" : weapon.GetType().Name.ToUpperInvariant();
    }

    public string GetRoomLabel()
    {
        return _currentRoom != null && _currentRoom.Node != null
            ? $"{_currentRoom.Node.Id + 1:00} / {_currentRoom.Node.Type.ToString().ToUpperInvariant()}"
            : "--";
    }

    public bool IsRoomVisited(int id) => _visitedRooms.Contains(id);

    public bool IsRoomAvailable(int id)
    {
        if (_currentRoom == null || _currentRoom.Node == null)
            return false;
        return _currentRoom.Node.Connections.Contains(id);
    }

    public void OnItemPicked(MeralgoreItemType item)
    {
        Log($"Получен предмет: {GetItemName(item)} — {GetItemDescription(item)}.");
    }

    private void StartEncounter(FloorMapRoom room, int enemyCount, bool boss)
    {
        room.SetLocked(true);
        _activeEnemies.Clear();

        for (int i = 0; i < enemyCount; i++)
        {
            float x = (float)(_random.NextDouble() * 28f - 14f);
            float z = (float)(_random.NextDouble() * 22f - 11f);
            Vector3 position = room.transform.position + new Vector3(x, 0f, z);
            SpawnEnemy(position, room, boss, i + 1);
        }

        if (!boss)
            Log($"Началась схватка. Противников: {enemyCount}.");
    }

    private void SpawnEnemy(Vector3 position, FloorMapRoom room, bool boss, int ordinal)
    {
        var enemyObject = new GameObject(boss ? "BOSS" : $"HOSTILE_{ordinal:00}");
        enemyObject.transform.position = position;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            enemyObject.layer = enemyLayer;

        try
        {
            enemyObject.tag = "Enemy";
        }
        catch (UnityException exception)
        {
            Debug.LogError($"Enemy tag is not configured: {exception.Message}");
        }

        var controller = enemyObject.AddComponent<CharacterController>();
        controller.stepOffset = 0.3f;
        var health = enemyObject.AddComponent<Health>();
        var enemy = enemyObject.AddComponent<MeralgoreEnemyController>();
        CreateEnemyVisual(enemyObject.transform, boss);
        enemy.Configure(this, boss, _random.Next(7, 12));

        health.Damaged += OnEnemyDamaged;
        health.Died += OnEnemyDied;
        _activeEnemies.Add(health);
        if (boss)
            Log("Босс готовит атаку.");
    }

    private void OnEnemyDamaged(Health enemyHealth, float damage, float remainingHealth)
    {
        string targetName = enemyHealth.gameObject.name.Replace('_', ' ');
        Log($"HIT // {targetName} // -{Mathf.CeilToInt(damage)} DMG // " +
            $"{Mathf.CeilToInt(remainingHealth)}/{Mathf.CeilToInt(enemyHealth.MaxHealth)} HP");
    }

    private void OnEnemyDied(Health enemyHealth)
    {
        enemyHealth.Damaged -= OnEnemyDamaged;
        enemyHealth.Died -= OnEnemyDied;
        _activeEnemies.Remove(enemyHealth);

        MeralgoreEnemyController enemy = enemyHealth.GetComponent<MeralgoreEnemyController>();
        if (enemy != null && !enemy.IsBoss && _random.NextDouble() < 0.2)
            CreatePickup(enemy.transform.position + Vector3.up, RandomItem());

        if (enemy != null && enemy.IsBoss)
        {
            if (_currentRoom != null)
                _currentRoom.MarkCleared();
            PlayerState.AddCurrency(50);
            Log("Босс повержен. Забег завершён.");
            EndRun(true);
            return;
        }

        if (_activeEnemies.Count == 0 && _currentRoom != null)
        {
            _currentRoom.MarkCleared();
            PlayerState.AddCurrency(10);
            Log("Пространство очищено: +10 валюты. Выходы открыты.");
        }
    }

    private void Interact()
    {
        if (_currentRoom == null || _currentRoom.Node == null)
            return;

        if (_currentRoom.Node.Type == FloorRoomType.Shop)
        {
            if (!_usedSpecialRooms.Add(_currentRoom.Node.Id))
            {
                Log("Этот магазин уже использован.");
                return;
            }

            if (!PlayerState.TrySpendCurrency(15))
            {
                _usedSpecialRooms.Remove(_currentRoom.Node.Id);
                Log("Недостаточно валюты. Предмет стоит 15.");
                return;
            }

            GiveRandomItem();
            Log("Предмет куплен за 15 валюты.");
        }
        else if (_currentRoom.Node.Type == FloorRoomType.Cursed)
        {
            if (!_usedSpecialRooms.Add(_currentRoom.Node.Id))
                return;
            if (!PlayerState.TrySpendCurrency(10))
            {
                _usedSpecialRooms.Remove(_currentRoom.Node.Id);
                Log("Недостаточно валюты. Вход стоит 10.");
                return;
            }

            int outcome = _random.Next(100);
            if (outcome < 55)
            {
                GiveRandomItem();
                _currentRoom.MarkCleared();
                Log("Проклятие принесло предмет.");
            }
            else if (outcome < 80)
            {
                Log("В пространстве оказалась ловушка.");
                StartEncounter(_currentRoom, 1, false);
            }
            else
            {
                _currentRoom.MarkCleared();
                Log("Пространство оказалось пустым.");
            }
        }
    }

    private void GiveRandomItem()
    {
        MeralgoreItemType item = RandomItem();
        if (PlayerState.AddItem(item))
            OnItemPicked(item);
        else
            Log($"Предмет уже есть: {GetItemName(item)}.");
    }

    private MeralgoreItemType RandomItem()
    {
        MeralgoreItemType[] items =
        {
            MeralgoreItemType.FlowerSection,
            MeralgoreItemType.WingedKitten,
            MeralgoreItemType.Devotion,
            MeralgoreItemType.Meloni
        };
        return items[_random.Next(items.Length)];
    }

    private void CreateUpperPickups()
    {
        if (_generator.Rooms.Count < 8)
            return;

        var candidateRooms = new List<FloorMapRoom>();
        foreach (FloorMapRoom room in _generator.Rooms)
        {
            if (room.Node.Type == FloorRoomType.Combat)
                candidateRooms.Add(room);
        }

        for (int i = 0; i < 3 && candidateRooms.Count > 0; i++)
        {
            int roomIndex = _random.Next(candidateRooms.Count);
            FloorMapRoom room = candidateRooms[roomIndex];
            candidateRooms.RemoveAt(roomIndex);
            int side = _random.Next(0, 2) == 0 ? -1 : 1;
            float z = (float)(_random.NextDouble() * 24f - 12f);
            Vector3 position = room.transform.position + new Vector3(side * 27f, 9.5f, z);
            CreatePickup(position, RandomItem());
        }
    }

    private void CreatePickup(Vector3 position, MeralgoreItemType item)
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickup.name = $"Item_{item}";
        pickup.transform.position = position;
        pickup.transform.localScale = Vector3.one * 0.75f;

        SphereCollider collider = pickup.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1.25f;

        Renderer renderer = pickup.GetComponent<Renderer>();
        Material material = CreateItemMaterial(item);
        if (renderer != null)
            renderer.sharedMaterial = material;

        pickup.AddComponent<MeralgoreItemPickup>().Configure(item);
    }

    private static Material CreateItemMaterial(MeralgoreItemType item)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError($"No supported shader was found for item '{item}'.");
            return null;
        }

        Color color = item == MeralgoreItemType.Meloni
            ? MeralgorePalette.Signal
            : item == MeralgoreItemType.Devotion
                ? MeralgorePalette.MainRoute
                : item == MeralgoreItemType.WingedKitten
                    ? MeralgorePalette.Text
                    : MeralgorePalette.Boss;
        var material = new Material(shader) { name = $"Item_{item}_Material" };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    private static void CreateEnemyVisual(Transform parent, bool boss)
    {
        Material bodyMaterial = CreateEnemyMaterial(boss ? MeralgorePalette.Boss : MeralgorePalette.Structure);
        Material signalMaterial = CreateEnemyMaterial(MeralgorePalette.Signal);
        float size = boss ? 1.7f : 1f;

        CreateEnemyPart(parent, "Torso", PrimitiveType.Capsule,
            new Vector3(0f, 1f * size, 0f), new Vector3(0.7f, 0.8f, 0.55f) * size,
            Quaternion.Euler(0f, 0f, -4f), bodyMaterial);
        CreateEnemyPart(parent, "Head", PrimitiveType.Cube,
            new Vector3(0f, 2f * size, 0.25f * size), new Vector3(0.6f, 0.45f, 0.6f) * size,
            Quaternion.Euler(-8f, 0f, 3f), signalMaterial);
        CreateEnemyPart(parent, "LeftShoulder", PrimitiveType.Cube,
            new Vector3(-0.52f * size, 1.55f * size, 0f), new Vector3(0.34f, 0.5f, 0.4f) * size,
            Quaternion.Euler(0f, 0f, 14f), bodyMaterial);
        CreateEnemyPart(parent, "RightShoulder", PrimitiveType.Cube,
            new Vector3(0.52f * size, 1.55f * size, 0f), new Vector3(0.34f, 0.5f, 0.4f) * size,
            Quaternion.Euler(0f, 0f, -14f), bodyMaterial);
    }

    private static GameObject CreateEnemyPart(
        Transform parent,
        string objectName,
        PrimitiveType primitive,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            part.layer = enemyLayer;
        return part;
    }

    private static Material CreateEnemyMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("No supported shader was found for generated enemy visuals.");
            return null;
        }

        var material = new Material(shader) { name = "Meralgore_Enemy_Material" };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    private void SetState(MeralgoreRunState state)
    {
        _state = state;
        bool controlsEnabled = state == MeralgoreRunState.Playing;
        if (_movement != null)
            _movement.enabled = controlsEnabled;
        if (_inputController != null)
            _inputController.enabled = controlsEnabled;

        if (state == MeralgoreRunState.Paused)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }

        Cursor.lockState = controlsEnabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !controlsEnabled;
        _ui?.SetState(state);
    }

    private void EndRun(bool won)
    {
        if (_state == MeralgoreRunState.Won || _state == MeralgoreRunState.Lost)
            return;
        SetState(won ? MeralgoreRunState.Won : MeralgoreRunState.Lost);
        Log(won ? "ПОБЕДА — ENTER: новый забег." : "ПОРАЖЕНИЕ — ENTER: попробовать снова.");
    }

    private void RestartRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void TeleportPlayer(Vector3 position)
    {
        if (_movement == null)
            return;

        CharacterController controller = _movement.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
        _movement.transform.position = position;
        if (controller != null)
            controller.enabled = true;
    }

    private static string GetItemName(MeralgoreItemType item)
    {
        switch (item)
        {
            case MeralgoreItemType.FlowerSection: return "Цветочное сечение";
            case MeralgoreItemType.WingedKitten: return "Котёнок с крыльями";
            case MeralgoreItemType.Devotion: return "Преданность";
            case MeralgoreItemType.Meloni: return "Мэлони";
            default: return item.ToString();
        }
    }

    private static string GetItemDescription(MeralgoreItemType item)
    {
        switch (item)
        {
            case MeralgoreItemType.FlowerSection: return "15% шанс восстановить 4 HP при нанесении урона";
            case MeralgoreItemType.WingedKitten: return "меньше разброс, но ниже скорострельность";
            case MeralgoreItemType.Devotion: return "усмиряет ближайших противников";
            case MeralgoreItemType.Meloni: return "скорость бега ×2,5";
            default: return string.Empty;
        }
    }

}
