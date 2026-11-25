using UnityEngine;

namespace Snog.SimpleWaveSystem.Demo
{
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }

    public class SimpleRaycastShooter : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera shootCamera;
        [SerializeField] private float range = 50f;
        [SerializeField] private float damage = 25f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("FX (Optional)")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private AudioSource shootAudio;

        private void Awake()
        {
            if (shootCamera == null)
            {
                shootCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Shoot();
            }
        }

        private void Shoot()
        {
            if (shootCamera == null)
                return;

            Ray ray = new Ray(shootCamera.transform.position, shootCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                // Damage
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    dmg.TakeDamage(damage);
                }

                // Hit FX
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                }
            }

            if (shootAudio != null)
            {
                shootAudio.Play();
            }
        }
    }
}
