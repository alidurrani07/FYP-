using UnityEngine;
using Invector;

public class PlayerBulletDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    public int damageAmount = 20;
    public float destroyDelay = 0.05f;

    private bool hasHit;

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Check if hit object or its parent has Invector health
        vHealthController health = other.GetComponentInParent<vHealthController>();

        if (health != null)
        {
            hasHit = true;

            vDamage damage = new vDamage()
            {
                damageValue = damageAmount,
                sender = transform,
                hitPosition = transform.position,
                receiver = health.transform
            };

            health.TakeDamage(damage);

            Debug.Log("Bullet hit Invector AI: " + health.gameObject.name);

            Destroy(gameObject, destroyDelay);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        vHealthController health = collision.collider.GetComponentInParent<vHealthController>();

        if (health != null)
        {
            hasHit = true;

            ContactPoint contact = collision.contacts[0];

            vDamage damage = new vDamage()
            {
                damageValue = damageAmount,
                sender = transform,
                hitPosition = contact.point,
                receiver = health.transform
            };

            health.TakeDamage(damage);

            Debug.Log("Bullet collided with Invector AI: " + health.gameObject.name);

            Destroy(gameObject, destroyDelay);
        }
    }
}