using UnityEngine;
using Invector;
using Invector.vEventSystems;

public class PlayerBulletDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    public int damageAmount = 20;
    public float destroyDelay = 0.05f;

    private bool hasHit;

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        TryDamage(other, transform.position, "Bullet hit Invector AI: ");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        ContactPoint contact = collision.contactCount > 0 ? collision.contacts[0] : default;
        Vector3 hitPoint = collision.contactCount > 0 ? contact.point : transform.position;
        TryDamage(collision.collider, hitPoint, "Bullet collided with Invector AI: ");
    }

    private void TryDamage(Collider hitCollider, Vector3 hitPoint, string logPrefix)
    {
        if (hitCollider == null)
        {
            return;
        }

        vDamage damage = new vDamage()
        {
            damageValue = damageAmount,
            sender = transform,
            hitPosition = hitPoint
        };

        vIDamageReceiver receiver = hitCollider.GetComponent<vIDamageReceiver>();
        if (receiver == null)
        {
            receiver = hitCollider.GetComponentInParent<vIDamageReceiver>();
        }

        if (receiver != null)
        {
            hasHit = true;
            receiver.TakeDamage(damage);
            Debug.Log(logPrefix + hitCollider.gameObject.name);
            Destroy(gameObject, destroyDelay);
            return;
        }

        vHealthController health = hitCollider.GetComponentInParent<vHealthController>();
        if (health == null)
        {
            health = hitCollider.GetComponentInChildren<vHealthController>();
        }

        if (health == null)
        {
            return;
        }

        hasHit = true;
        damage.receiver = health.transform;
        health.TakeDamage(damage);
        Debug.Log(logPrefix + health.gameObject.name);
        Destroy(gameObject, destroyDelay);
    }
}
