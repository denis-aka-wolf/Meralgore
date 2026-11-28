using System.Collections.Generic;
using UnityEngine;

public class MeralgoreEnemyController : MonoBehaviour
{
    private static readonly List<MeralgoreEnemyController> ActiveEnemies =
        new List<MeralgoreEnemyController>();
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackDamage = 8f;

    private FloorRunController _runController;
    private MeralgorePlayerState _target;
    private CharacterController _characterController;
    private Health _health;
    private GameObject _attackTelegraph;
    private Material _telegraphMaterial;
    private bool _isBoss;
    private bool _phaseTwoUnlocked;
    private bool _isWindingUp;
    private float _windupEndTime;
    private float _nextAttackTime;
    private float _verticalVelocity;
    private float _attackRadius;
    private float _attackDamage;
    private Vector3 _attackCenter;
    private float _formationAngle;
    private float _engagementRadius = 1.9f;

    public bool IsBoss => _isBoss;

    public void Configure(FloorRunController runController, bool isBoss, int normalEnemyHealth)
    {
        _runController = runController;
        _target = runController.PlayerState;
        _isBoss = isBoss;
        _formationAngle = Mathf.Abs(GetInstanceID() % 360) * Mathf.Deg2Rad;
        _engagementRadius = 1.7f + Mathf.Abs(GetInstanceID() % 3) * 0.35f;
        if (!ActiveEnemies.Contains(this))
            ActiveEnemies.Add(this);
        _characterController = GetComponent<CharacterController>();
        _health = GetComponent<Health>();

        if (_isBoss)
        {
            transform.localScale = Vector3.one * 1.8f;
            chaseSpeed = 3.2f;
            attackRange = 5.5f;
            attackCooldown = 1.55f;
            attackDamage = 24f;
            _health.SetHealth(60f);
        }
        else
        {
            _health.SetHealth(normalEnemyHealth);
        }

        _characterController.height = 2.2f;
        _characterController.radius = 0.48f;
        _characterController.center = new Vector3(0f, 1.1f, 0f);
        CreateAttackTelegraph();
    }

    private void Update()
    {
        if (_runController == null || !_runController.IsPlaying || _target == null || _health.IsDead)
            return;

        Vector3 toTarget = _target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        bool phaseTwo = _isBoss && _health.CurrentHealth <= 30f;
        if (phaseTwo && !_phaseTwoUnlocked)
        {
            _phaseTwoUnlocked = true;
            _runController.Log("Босс входит во вторую фазу: готовьтесь к ударной волне.");
        }

        if (_target.HasDevotion && distance <= 9f)
        {
            CancelAttack();
            return;
        }

        if (_isWindingUp)
        {
            if (Time.time >= _windupEndTime)
                ResolveAttack();
            return;
        }

        float effectiveAttackRange = phaseTwo ? 8.5f : (_isBoss ? attackRange : 2.8f);
        if (distance > effectiveAttackRange)
        {
            Vector3 desiredPoint = _target.transform.position;
            if (!_isBoss)
            {
                Vector3 orbitOffset = new Vector3(
                    Mathf.Cos(_formationAngle), 0f, Mathf.Sin(_formationAngle)) * _engagementRadius;
                desiredPoint += orbitOffset;
            }

            Vector3 toApproachPoint = desiredPoint - transform.position;
            toApproachPoint.y = 0f;
            Vector3 movementDirection = toApproachPoint.sqrMagnitude > 0.01f
                ? toApproachPoint.normalized
                : toTarget.normalized;

            if (!_isBoss)
            {
                Vector3 separation = GetSeparationVector();
                movementDirection = (movementDirection + separation * 1.8f).normalized;
            }

            if (movementDirection.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up);

            if (_characterController.isGrounded)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;

            Vector3 movement = movementDirection * chaseSpeed;
            movement.y = _verticalVelocity;
            _characterController.Move(movement * Time.deltaTime);
            return;
        }

        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + (phaseTwo ? attackCooldown * 0.65f : attackCooldown);
        if (!_isBoss)
        {
            BeginAttack(_target.transform.position, 2.2f, attackDamage, 0.35f, null);
        }
        else if (phaseTwo)
        {
            BeginAttack(transform.position, 8.5f, 32f, 1f,
                "Босс заряжает ударную волну. Уходите из красной зоны!");
        }
        else
        {
            BeginAttack(_target.transform.position, 4.5f, attackDamage, 0.65f,
                "Босс замахивается. Уходите из зоны удара!");
        }
    }

    private Vector3 GetSeparationVector()
    {
        const float separationRadius = 2.35f;
        Vector3 separation = Vector3.zero;

        for (int i = 0; i < ActiveEnemies.Count; i++)
        {
            MeralgoreEnemyController other = ActiveEnemies[i];
            if (other == null || other == this || other._isBoss ||
                other._runController != _runController || other._health == null || other._health.IsDead)
                continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float sqrDistance = away.sqrMagnitude;
            if (sqrDistance <= 0.001f || sqrDistance >= separationRadius * separationRadius)
                continue;

            float distance = Mathf.Sqrt(sqrDistance);
            separation += away / (distance * distance);
        }

        return separation;
    }

    private void BeginAttack(
        Vector3 center,
        float radius,
        float damage,
        float windup,
        string warning)
    {
        _attackCenter = center;
        _attackRadius = radius;
        _attackDamage = damage;
        _windupEndTime = Time.time + windup;
        _isWindingUp = true;
        SetTelegraphVisible(true);

        if (!string.IsNullOrEmpty(warning))
            _runController.Log(warning);
    }

    private void ResolveAttack()
    {
        _isWindingUp = false;
        SetTelegraphVisible(false);

        Vector3 targetPosition = _target.transform.position;
        targetPosition.y = _attackCenter.y;
        if ((targetPosition - _attackCenter).sqrMagnitude <= _attackRadius * _attackRadius)
            _target.Damage(_attackDamage);
    }

    private void CancelAttack()
    {
        _isWindingUp = false;
        SetTelegraphVisible(false);
    }

    private void CreateAttackTelegraph()
    {
        _attackTelegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _attackTelegraph.name = "AttackWarning";
        Collider collider = _attackTelegraph.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogError("No supported shader was found for enemy attack warnings.");
            _attackTelegraph.SetActive(false);
            return;
        }

        _telegraphMaterial = new Material(shader) { name = "Enemy_AttackWarning_Material" };
        if (_telegraphMaterial.HasProperty("_BaseColor"))
            _telegraphMaterial.SetColor("_BaseColor", MeralgorePalette.Boss);
        if (_telegraphMaterial.HasProperty("_Color"))
            _telegraphMaterial.SetColor("_Color", MeralgorePalette.Boss);

        Renderer renderer = _attackTelegraph.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = _telegraphMaterial;
        _attackTelegraph.SetActive(false);
    }

    private void SetTelegraphVisible(bool visible)
    {
        if (_attackTelegraph == null || _telegraphMaterial == null)
            return;

        if (visible)
        {
            _attackTelegraph.transform.position =
                new Vector3(_attackCenter.x, transform.position.y + 0.05f, _attackCenter.z);
            _attackTelegraph.transform.localScale =
                new Vector3(_attackRadius * 2f, 0.025f, _attackRadius * 2f);
        }
        _attackTelegraph.SetActive(visible);
    }

    private void OnDestroy()
    {
        ActiveEnemies.Remove(this);
        if (_attackTelegraph != null)
            Destroy(_attackTelegraph);
        if (_telegraphMaterial != null)
            Destroy(_telegraphMaterial);
    }
}
