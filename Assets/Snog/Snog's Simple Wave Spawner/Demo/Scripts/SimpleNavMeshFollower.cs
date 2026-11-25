using UnityEngine;
using UnityEngine.AI;

namespace Snog.SimpleWaveSystem.Demo
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class SimpleNavMeshFollower : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow Settings")]
        [SerializeField] private float updateRate = 0.25f;
        [SerializeField] private float stopDistance = 1.5f;
        [SerializeField] private bool rotateToTarget = true;

        private NavMeshAgent agent;
        private float nextUpdateTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            if (target == null)
            {
                var interactor = FindAnyObjectByType<SimplePlayerController>();
                if (interactor != null)
                    target = interactor.transform;
            }

            agent.stoppingDistance = stopDistance;
        }

        private void Update()
        {
            if (target == null || !agent.isOnNavMesh)
                return;

            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + updateRate;
                agent.SetDestination(target.position);
            }

            if (rotateToTarget)
            {
                RotateTowardsTarget();
            }
        }

        private void RotateTowardsTarget()
        {
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.001f)
                return;

            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 8f);
        }
    }
}
