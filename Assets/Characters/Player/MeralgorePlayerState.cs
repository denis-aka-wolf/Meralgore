using System;
using System.Collections.Generic;
using UnityEngine;

public enum MeralgoreItemType
{
    FlowerSection,
    WingedKitten,
    Devotion,
    Meloni
}

public class MeralgorePlayerState : MonoBehaviour
{
    private readonly HashSet<MeralgoreItemType> _items = new HashSet<MeralgoreItemType>();
    private PlayerMovement _movement;
    private float _baseRunSpeed;
    private float _health = 100f;
    private int _currency;

    public float MaxHealth { get; private set; } = 100f;
    public float CurrentHealth => _health;
    public int Currency => _currency;
    public IEnumerable<MeralgoreItemType> Items => _items;
    public bool HasDevotion => _items.Contains(MeralgoreItemType.Devotion);
    public float CooldownMultiplier => _items.Contains(MeralgoreItemType.WingedKitten) ? 1.4f : 1f;
    public float SpreadMultiplier => _items.Contains(MeralgoreItemType.WingedKitten) ? 0.4f : 1f;

    public event Action Changed;

    private void OnEnable()
    {
        global::Health.DamageDealt += OnDamageDealt;
    }

    private void OnDisable()
    {
        global::Health.DamageDealt -= OnDamageDealt;
    }

    public void Initialize(PlayerMovement movement)
    {
        _movement = movement;
        if (_movement != null)
            _baseRunSpeed = _movement.runSpeed;
        _health = MaxHealth;
        _currency = 0;
        _items.Clear();
        Changed?.Invoke();
    }

    public bool AddItem(MeralgoreItemType item)
    {
        if (!_items.Add(item))
            return false;

        if (item == MeralgoreItemType.Meloni && _movement != null)
            _movement.SetRunSpeed(_baseRunSpeed * 2.5f);

        Changed?.Invoke();
        return true;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0)
            return;

        _currency += amount;
        Changed?.Invoke();
    }

    public bool TrySpendCurrency(int amount)
    {
        if (amount < 0 || _currency < amount)
            return false;

        _currency -= amount;
        Changed?.Invoke();
        return true;
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || _health <= 0f)
            return;

        _health = Mathf.Max(0f, _health - amount);
        Changed?.Invoke();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || _health <= 0f)
            return;

        _health = Mathf.Min(MaxHealth, _health + amount);
        Changed?.Invoke();
    }

    private void OnDamageDealt(float damage)
    {
        if (_items.Contains(MeralgoreItemType.FlowerSection) && UnityEngine.Random.value < 0.15f)
            Heal(4f);
    }
}
