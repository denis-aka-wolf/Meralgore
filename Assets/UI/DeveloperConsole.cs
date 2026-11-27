using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class DeveloperConsole : MonoBehaviour
{
    [SerializeField] private int maxLogLines = 30;

    private readonly List<string> _log = new List<string>();
    private readonly Dictionary<string, Func<string[], string>> _commands =
        new Dictionary<string, Func<string[], string>>(StringComparer.OrdinalIgnoreCase);

    private bool _isOpen;
    private string _input = string.Empty;
    private Vector2 _scrollPosition;
    private bool _noclip;
    private float _resumeTimeScale = 1f;

    private void Awake()
    {
        RegisterCommands();
        Log("Developer console. Type 'help'.");
    }

    private void OnDisable()
    {
        if (_isOpen)
        {
            Time.timeScale = _resumeTimeScale;
            _isOpen = false;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame && _isOpen)
            SetOpen(false);

        if (_noclip)
            UpdateNoclip(keyboard);
    }

    private void UpdateNoclip(Keyboard keyboard)
    {
        PlayerMovement player = FindPlayer();
        if (player == null)
            return;

        CinemachineCamera playerCamera = player.GetComponent<PlayerMovement>() != null
            ? GetPlayerCamera(player)
            : null;

        Vector3 forward = playerCamera != null
            ? playerCamera.transform.forward
            : player.transform.forward;
        Vector3 right = playerCamera != null
            ? playerCamera.transform.right
            : player.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += forward;
        if (keyboard.sKey.isPressed) move -= forward;
        if (keyboard.dKey.isPressed) move += right;
        if (keyboard.aKey.isPressed) move -= right;
        if (keyboard.eKey.isPressed) move += Vector3.up;
        if (keyboard.qKey.isPressed) move -= Vector3.up;

        float speed = keyboard.shiftKey.isPressed ? 30f : 10f;
        player.transform.position += move.normalized * speed * Time.unscaledDeltaTime;
    }

    private static CinemachineCamera GetPlayerCamera(PlayerMovement player)
    {
        FieldInfo field = typeof(PlayerMovement).GetField("playerCamera",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(player) as CinemachineCamera;
    }

    private void OnGUI()
    {
        Event eventValue = Event.current;
        if (eventValue.type == EventType.KeyDown)
        {
            if (eventValue.keyCode == KeyCode.BackQuote ||
                eventValue.character == '`')
            {
                SetOpen(!_isOpen);
                eventValue.Use();
                return;
            }

            if (_isOpen && eventValue.keyCode == KeyCode.Escape)
            {
                SetOpen(false);
                eventValue.Use();
                return;
            }

            if (_isOpen)
            {
                if (eventValue.keyCode == KeyCode.Return ||
                    eventValue.keyCode == KeyCode.KeypadEnter)
                {
                    Execute(_input);
                    _input = string.Empty;
                    eventValue.Use();
                    return;
                }

                if (eventValue.keyCode == KeyCode.Backspace)
                {
                    if (_input.Length > 0)
                        _input = _input.Substring(0, _input.Length - 1);
                    eventValue.Use();
                    return;
                }

                if (!char.IsControl(eventValue.character) && eventValue.character != '\0')
                {
                    _input += eventValue.character;
                    eventValue.Use();
                    return;
                }
            }
        }

        if (!_isOpen)
            return;

        const float width = 760f;
        const float height = 420f;
        float left = 20f;
        float top = 20f;

        GUI.Box(new Rect(left, top, width, height), "Developer Console");

        float logHeight = height - 82f;
        _scrollPosition = GUI.BeginScrollView(
            new Rect(left + 10f, top + 28f, width - 20f, logHeight),
            _scrollPosition,
            new Rect(0f, 0f, width - 40f, _log.Count * 22f + 4f));

        for (int i = 0; i < _log.Count; i++)
        {
            GUI.Label(new Rect(4f, i * 22f, width - 48f, 20f), _log[i]);
        }
        GUI.EndScrollView();

        GUI.Box(new Rect(left + 10f, top + height - 42f, width - 20f, 28f), string.Empty);
        GUI.Label(new Rect(left + 18f, top + height - 39f, width - 36f, 22f), "> " + _input + "_");
    }

    private void SetOpen(bool open)
    {
        if (_isOpen == open)
            return;

        if (open)
        {
            _resumeTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = _resumeTimeScale;
        }

        _isOpen = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (open)
        {
            ShowHelp();
            _scrollPosition.y = float.MaxValue;
        }
    }

    private void RegisterCommands()
    {
        _commands["help"] = args =>
        {
            ShowHelp();
            return string.Empty;
        };
        _commands["clear"] = args =>
        {
            _log.Clear();
            return "Console cleared.";
        };
        _commands["status"] = args =>
        {
            PlayerMovement player = FindPlayer();
            if (player == null)
                return "Player not found.";

            return $"Position={player.transform.position:F1}, Jump={GetJumpHeight(player):F1}, Walk={player.walkSpeed:F1}, Run={player.runSpeed:F1}, TimeScale={Time.timeScale:F2}";
        };
        _commands["set"] = SetParameter;
        _commands["tp"] = Teleport;
        _commands["noclip"] = args =>
        {
            _noclip = !_noclip;
            PlayerMovement player = FindPlayer();
            if (player == null)
                return "Player not found.";

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = !_noclip;

            return _noclip ? "Noclip enabled." : "Noclip disabled.";
        };
    }

    private void ShowHelp()
    {
        string[] helpLines =
        {
            "METALGORE Developer Console - command reference",
            "help - Show this command reference. Example: help",
            "clear - Clear console output. Example: clear",
            "status - Show player position, jump, speeds and time scale. Example: status",
            "set jump <n> - Set jump height. Example: set jump 5.2",
            "set walk <n> - Set walking speed. Example: set walk 8",
            "set run <n> - Set running speed. Example: set run 13",
            "set gravity <n> - Set gravity acceleration (usually negative). Example: set gravity -9.81",
            "set timescale <n> - Set resume speed from 0 to 10; applied after closing. Example: set timescale 0.5",
            "tp start - Teleport to the floor spawn. Example: tp start",
            "tp boss - Teleport to the boss arena. Example: tp boss",
            "noclip - Toggle free flight through geometry. Example: noclip",
            "Noclip controls: WASD move, E/Q move vertically, hold Shift to move faster.",
            "The game pauses while this console is open. Press ` to toggle or Esc to close."
        };

        foreach (string line in helpLines)
            Log(line);
    }

    private string SetParameter(string[] args)
    {
        if (args.Length != 2)
            return "Usage: set <jump|walk|run|gravity|timescale> <number>";

        if (!float.TryParse(args[1], out float value))
            return "Invalid number.";

        if (args[0].Equals("timescale", StringComparison.OrdinalIgnoreCase))
        {
            _resumeTimeScale = Mathf.Clamp(value, 0f, 10f);
            return $"TimeScale set to {_resumeTimeScale:F2} (applied when console closes).";
        }

        PlayerMovement player = FindPlayer();
        if (player == null)
            return "Player not found.";

        if (args[0].Equals("jump", StringComparison.OrdinalIgnoreCase))
        {
            SetJumpHeight(player, value);
            return $"Jump set to {value:F1}.";
        }
        if (args[0].Equals("walk", StringComparison.OrdinalIgnoreCase))
        {
            player.walkSpeed = value;
            return $"Walk set to {value:F1}.";
        }
        if (args[0].Equals("run", StringComparison.OrdinalIgnoreCase))
        {
            player.runSpeed = value;
            return $"Run set to {value:F1}.";
        }
        if (args[0].Equals("gravity", StringComparison.OrdinalIgnoreCase))
        {
            FieldInfo gravityField = typeof(PlayerMovement).GetField("gravity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (gravityField == null)
                return "Gravity field was not found.";
            gravityField.SetValue(player, value);
            return $"Gravity set to {value:F1}.";
        }

        return "Unknown parameter. Use jump, walk, run, gravity or timescale.";
    }

    private string Teleport(string[] args)
    {
        if (args.Length != 1)
            return "Usage: tp <start|boss>";

        PlayerMovement player = FindPlayer();
        if (player == null)
            return "Player not found.";

        Vector3 position;
        if (args[0].Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            Transform spawn = FindTransform("PlayerSpawn");
            position = spawn != null ? spawn.position + Vector3.up * 1.5f : Vector3.zero;
        }
        else if (args[0].Equals("boss", StringComparison.OrdinalIgnoreCase))
        {
            position = new Vector3(0f, 2.5f, 648f);
        }
        else
        {
            return "Unknown location. Use start or boss.";
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.forward));

        if (controller != null)
            controller.enabled = !_noclip;

        return $"Teleported to {args[0].ToLowerInvariant()}.";
    }

    private void Execute(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return;

        string normalizedCommandLine = NormalizeKeyboardLayout(commandLine.Trim());
        Log("> " + normalizedCommandLine);
        string[] parts = normalizedCommandLine.Split((char[])null,
            StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0];
        string[] args = parts.Length > 1
            ? parts[1..]
            : Array.Empty<string>();

        if (_commands.TryGetValue(command, out Func<string[], string> handler))
        {
            string response = handler(args);
            if (!string.IsNullOrEmpty(response))
                Log(response);
        }
        else
            Log("Unknown command: " + command);

        _scrollPosition.y = float.MaxValue;
    }

    private static string NormalizeKeyboardLayout(string value)
    {
        const string russianKeys = "ёйцукенгшщзхъфывапролджэячсмитьбю";
        const string englishKeys = "`qwertyuiop[]asdfghjkl;'zxcvbnm,.";
        StringBuilder normalized = new StringBuilder(value.Length);

        foreach (char character in value)
        {
            char lowerCharacter = char.ToLowerInvariant(character);
            int keyIndex = russianKeys.IndexOf(lowerCharacter);
            if (keyIndex < 0)
            {
                normalized.Append(character);
                continue;
            }

            char englishCharacter = englishKeys[keyIndex];
            normalized.Append(char.IsUpper(character)
                ? char.ToUpperInvariant(englishCharacter)
                : englishCharacter);
        }

        return normalized.ToString();
    }

    private void Log(string message)
    {
        _log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (_log.Count > maxLogLines)
            _log.RemoveAt(0);
    }

    private static PlayerMovement FindPlayer()
    {
        return FindObjectOfType<PlayerMovement>();
    }

    private static Transform FindTransform(string name)
    {
        foreach (Transform transform in FindObjectsOfType<Transform>(true))
        {
            if (transform.name == name)
                return transform;
        }
        return null;
    }

    private static float GetJumpHeight(PlayerMovement player)
    {
        FieldInfo field = typeof(PlayerMovement).GetField("jumpHeight",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (float)field.GetValue(player) : 0f;
    }

    private static void SetJumpHeight(PlayerMovement player, float value)
    {
        FieldInfo field = typeof(PlayerMovement).GetField("jumpHeight",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(player, value);
    }
}
