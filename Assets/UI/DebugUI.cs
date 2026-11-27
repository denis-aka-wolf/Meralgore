using UnityEngine;
using UnityEngine.UI;

public class DebugUI : MonoBehaviour
{
    [SerializeField] GameObject player;
    PlayerMovement playerMovement;
    CharacterController playerController;

    Weapon weapon;
    [SerializeField] Text speedDebug;
    [SerializeField] Text stateDebug;
    [SerializeField] Text attackDebug;
    [SerializeField] bool showConsoleHint;

    void Start()
    {
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (player == null)
            return;

        playerMovement = player.GetComponent<PlayerMovement>();
        playerController = player.GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (playerMovement == null || playerController == null)
            return;

        if (stateDebug != null)
            stateDebug.text = $"{playerMovement.IsGrounded()}, {playerMovement.isCrouching}, {playerMovement.isSliding}, {playerMovement.isRunning}";
        if (speedDebug != null)
        {
            string speed = $"{Mathf.Round(playerController.velocity.magnitude)}";
            speedDebug.text = showConsoleHint
                ? $"<size=32>~ - console</size>\n{speed}"
                : speed;
        }
        //attackDebug.text = $"attack ({weapon.attack}, 0)";
    }
}
