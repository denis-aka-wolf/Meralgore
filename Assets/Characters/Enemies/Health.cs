using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float health = 30f;

    private AudioSource _audioSource;
    private bool _isDead;

    public static event Action<float> DamageDealt;
    public event Action<Health> Died;
    public float CurrentHealth => health;
    public bool IsDead => _isDead;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void SetHealth(float value)
    {
        health = Mathf.Max(1f, value);
        _isDead = false;
    }

    public void Damage(float damage)
    {
        if (_isDead || damage <= 0f)
            return;

        health -= damage;
        DamageDealt?.Invoke(damage);

        if (health <= 0)
            Death();
    }

    private void Death()
    {
        _isDead = true;
        Died?.Invoke(this);

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        if (_audioSource != null && deathSound != null)
        {
            _audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(deathSound);
            Destroy(gameObject, deathSound.length);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
