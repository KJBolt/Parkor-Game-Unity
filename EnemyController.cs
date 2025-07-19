using UnityEngine.AI;
using UnityEngine;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{
    [Header("References")]
    public GameObject player;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;
    private NavMeshAgent navMeshAgent;
    private Animator animator;
    private GameObject currentTarget;
    private Vector3 startPosition;
    private Quaternion startRotation;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float detectionAngle = 110f;
    [SerializeField] private float maxChaseDistance = 20f;
    private float nextFoVCheck;

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waypointWaitTime = 2f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolTurnSpeed = 120f;
    private int currentWaypointIndex = 0;
    private float waypointReachedTime;
    private bool isWaitingAtWaypoint;

    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float chaseAcceleration = 8f;
    [SerializeField] private float chaseStoppingDistance = 1.5f;
    [SerializeField] private float chaseRotationSpeed = 360f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackRate = 1f;
    [SerializeField] private int maxComboHits = 4;
    [SerializeField] private float comboResetTime = 2f;
    [SerializeField] private float attackDamage = 10f;
    private float nextAttackTime;
    private int currentComboCount;
    private float lastAttackTime;
    private bool isAttacking;

    // States
    private enum EnemyState { Idle, Patrol, Chase, Attack, Return }
    [Header("States")]
    [SerializeField] private EnemyState currentState;
    [SerializeField] private EnemyState previousState;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            // Create default waypoints if none are assigned
            GameObject waypoint1 = new GameObject("DefaultWaypoint1");
            GameObject waypoint2 = new GameObject("DefaultWaypoint2");
            
            waypoint1.transform.position = startPosition;
            waypoint2.transform.position = startPosition + transform.forward * 5f; // 5 units forward
            
            waypoints = new Transform[] { waypoint1.transform, waypoint2.transform };
            Debug.Log("Created default waypoints for patrol");
        }
        
        ChangeState(EnemyState.Patrol);
    }

    private void Update()
    {
        UpdateState();
        UpdateAnimator();
    }

    private void UpdateState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
            case EnemyState.Return:
                UpdateReturnState();
                break;
        }
    }

    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;

        // Exit current state
        switch (currentState)
        {
            case EnemyState.Idle:
                ExitIdleState();
                break;
            case EnemyState.Patrol:
                ExitPatrolState();
                break;
            case EnemyState.Chase:
                ExitChaseState();
                break;
            case EnemyState.Attack:
                ExitAttackState();
                break;
            case EnemyState.Return:
                ExitReturnState();
                break;
        }

        // Change state
        previousState = currentState;
        currentState = newState;

        // Enter new state
        switch (newState)
        {
            case EnemyState.Idle:
                EnterIdleState();
                break;
            case EnemyState.Patrol:
                EnterPatrolState();
                break;
            case EnemyState.Chase:
                EnterChaseState();
                break;
            case EnemyState.Attack:
                EnterAttackState();
                break;
            case EnemyState.Return:
                EnterReturnState();
                break;
        }
    }

    #region Idle State
    private void EnterIdleState()
    {
        navMeshAgent.isStopped = true;
    }

    private void UpdateIdleState()
    {
        // Check for player detection
        if (CanSeePlayer())
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // After some idle time, go back to patrolling
        ChangeState(EnemyState.Patrol);
    }

    private void ExitIdleState()
    {
        // Clean up idle state
    }
    #endregion

    #region Patrol State
    private void EnterPatrolState()
    {
        navMeshAgent.speed = patrolSpeed;
        navMeshAgent.acceleration = 4f;
        navMeshAgent.stoppingDistance = 0.1f;
        navMeshAgent.isStopped = false;
        isWaitingAtWaypoint = false;
        MoveToCurrentWaypoint();
    }

    private void UpdatePatrolState()
    {
        // Check for player detection
        if (CanSeePlayer())
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Handle waypoint movement
        if (!isWaitingAtWaypoint && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            if (!navMeshAgent.pathPending)
            {
                // Reached waypoint
                Debug.Log($"Reached waypoint {currentWaypointIndex}, starting wait timer");
                isWaitingAtWaypoint = true;
                waypointReachedTime = Time.time;
            }
        }

        // Wait at waypoint before moving to next
        if (isWaitingAtWaypoint && Time.time >= waypointReachedTime + waypointWaitTime)
        {
            Debug.Log($"Wait time finished, moving to next waypoint");
            isWaitingAtWaypoint = false;
            MoveToNextWaypoint();
        }

        // Debug info
        if (Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
        {
            Debug.Log($"Patrol State - Current waypoint: {currentWaypointIndex}, Remaining distance: {navMeshAgent.remainingDistance}, Is waiting: {isWaitingAtWaypoint}, NavMesh velocity: {navMeshAgent.velocity.magnitude}");
        }
    }

    private void MoveToCurrentWaypoint()
    {
        if (waypoints.Length == 0) return;
        
        navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
        Debug.Log($"Moving to waypoint {currentWaypointIndex} at position {waypoints[currentWaypointIndex].position}");
    }

    private void MoveToNextWaypoint()
    {
        if (waypoints.Length == 0) return;
        
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
        Debug.Log($"Moving to next waypoint {currentWaypointIndex} at position {waypoints[currentWaypointIndex].position}");
    }

    private void ExitPatrolState()
    {
        // Clean up patrol state
    }
    #endregion

    #region Chase State
    private void EnterChaseState()
    {
        navMeshAgent.speed = chaseSpeed;
        navMeshAgent.acceleration = chaseAcceleration;
        navMeshAgent.stoppingDistance = chaseStoppingDistance;
        navMeshAgent.isStopped = false;
    }

    private void UpdateChaseState()
    {
        if (currentTarget == null)
        {
            ChangeState(EnemyState.Return);
            return;
        }

        // Check if player is in attack range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distanceToTarget <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        // Check if player is too far away
        if (distanceToTarget > maxChaseDistance)
        {
            ChangeState(EnemyState.Return);
            return;
        }

        // Continue chasing
        navMeshAgent.SetDestination(currentTarget.transform.position);
    }

    private void ExitChaseState()
    {
        // Clean up chase state
    }
    #endregion

    #region Attack State
    private void EnterAttackState()
    {
        navMeshAgent.isStopped = true;
        isAttacking = true;
        currentComboCount = 0;
        lastAttackTime = Time.time;
        TriggerAttack();
    }

    private void UpdateAttackState()
    {
        if (currentTarget == null)
        {
            ChangeState(EnemyState.Return);
            return;
        }

        // Rotate to face the target
        Vector3 directionToTarget = (currentTarget.transform.position - transform.position).normalized;
        directionToTarget.y = 0;
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, chaseRotationSpeed * Time.deltaTime);
        }

        // Check if target moved out of attack range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distanceToTarget > attackRange * 1.2f)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Check if we can do another attack in the combo
        if (!isAttacking && Time.time >= lastAttackTime + attackRate && currentComboCount < maxComboHits)
        {
            TriggerAttack();
        }
        // Reset combo if too much time has passed
        else if (Time.time >= lastAttackTime + comboResetTime)
        {
            currentComboCount = 0;
            ChangeState(EnemyState.Chase);
        }
    }

    private void TriggerAttack()
    {
        isAttacking = true;
        currentComboCount++;
        lastAttackTime = Time.time;
        
        // Trigger attack animation (make sure you have these triggers in your Animator)
        animator.SetTrigger("Attack" + Mathf.Clamp(currentComboCount, 1, maxComboHits));
        
        // Deal damage (this would be called from an animation event)
        // You'll need to set up animation events in your attack animations
    }

    // Call this from animation events
    public void OnAttackHit()
    {
        if (currentTarget != null)
        {
            // Check if target is still in front and in range
            Vector3 directionToTarget = (currentTarget.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, directionToTarget) > 0.5f && 
                Vector3.Distance(transform.position, currentTarget.transform.position) <= attackRange * 1.2f)
            {
                // Apply damage to the target
                var healthComponent = currentTarget.GetComponent<IDamageable>();
                if (healthComponent != null)
                {
                    healthComponent.TakeDamage(attackDamage);
                    Debug.Log($"Dealt {attackDamage} damage to {currentTarget.name}");
                }
                else
                {
                    Debug.LogWarning($"No IDamageable component found on {currentTarget.name}");
                }
            }
        }
    }

    // Call this from animation events when attack animation ends
    public void OnAttackEnd()
    {
        isAttacking = false;
        
        // If we've reached max combo, reset and chase
        if (currentComboCount >= maxComboHits)
        {
            currentComboCount = 0;
            ChangeState(EnemyState.Chase);
        }
    }

    private void ExitAttackState()
    {
        isAttacking = false;
        navMeshAgent.isStopped = false;
    }
    #endregion

    #region Return State
    private void EnterReturnState()
    {
        navMeshAgent.speed = patrolSpeed;
        navMeshAgent.acceleration = 4f;
        navMeshAgent.stoppingDistance = 0.1f;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(startPosition);
    }

    private void UpdateReturnState()
    {
        // Check if we've reached the start position
        if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending)
        {
            // Reset rotation to original
            transform.rotation = Quaternion.RotateTowards(transform.rotation, startRotation, patrolTurnSpeed * Time.deltaTime);
            
            if (Quaternion.Angle(transform.rotation, startRotation) < 1f)
            {
                ChangeState(EnemyState.Patrol);
            }
        }
        
        // Check if we can see the player again
        if (CanSeePlayer())
        {
            ChangeState(EnemyState.Chase);
        }
    }

    private void ExitReturnState()
    {
        // Clean up return state
    }
    #endregion

    #region Detection
    private bool CanSeePlayer()
    {
        if (player == null)
        {
            Debug.LogWarning("Player reference is not set in the inspector!");
            return false;
        }
        
        Vector3 directionToPlayer = player.transform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;
        
        // Debug draw line to player
        Debug.DrawLine(transform.position + Vector3.up * 1.5f, player.transform.position, 
            Color.yellow, 0.1f);
        
        // Check if player is within detection radius
        if (distanceToPlayer > detectionRadius)
        {
            Debug.Log($"Player is too far: {distanceToPlayer} > {detectionRadius}");
            return false;
        }
        
        // Check if player is within field of view
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        
        // Debug visualization
        Vector3 eyeLevel = transform.position + Vector3.up * 1.5f;
        Debug.DrawRay(eyeLevel, transform.forward * detectionRadius, Color.blue, 0.1f); // Forward direction
        Debug.DrawRay(eyeLevel, directionToPlayer.normalized * detectionRadius, Color.green, 0.1f); // Direction to player
        
        // Draw the field of view cone
        Vector3 leftBoundary = Quaternion.Euler(0, -detectionAngle * 0.5f, 0) * transform.forward * detectionRadius;
        Vector3 rightBoundary = Quaternion.Euler(0, detectionAngle * 0.5f, 0) * transform.forward * detectionRadius;
        Debug.DrawRay(eyeLevel, leftBoundary, Color.white, 0.1f);
        Debug.DrawRay(eyeLevel, rightBoundary, Color.white, 0.1f);
        
        if (angleToPlayer > detectionAngle * 0.5f)
        {
            // Debug.Log($"Player is outside FOV: {angleToPlayer}° > {detectionAngle * 0.5f}°");
            return false;
        }
        
        // If we get here, player is within FOV and detection radius
        Debug.Log("Player detected in FOV!");
        currentTarget = player;
        return true;
    }
    #endregion

    #region Animation
    private void UpdateAnimator()
    {
        bool isMoving = navMeshAgent.velocity.magnitude > 0.1f;
        
        // Special case for patrol state - stop moving animation when very close to waypoint
        if (currentState == EnemyState.Patrol && waypoints != null && waypoints.Length > 0)
        {
            // Check if we're close to the next waypoint
            float distanceToWaypoint = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
            if (distanceToWaypoint <= 0.1f)
            {
                isMoving = false;
            }
        }
        
        // Update animator parameters based on current state
        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsChasing", currentState == EnemyState.Chase);
        animator.SetBool("IsAttacking", currentState == EnemyState.Attack);
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, detectionRadius);
        
        // Draw field of view
        Vector3 leftBoundary = Quaternion.Euler(0, -detectionAngle * 0.5f, 0) * transform.forward * detectionRadius;
        Vector3 rightBoundary = Quaternion.Euler(0, detectionAngle * 0.5f, 0) * transform.forward * detectionRadius;
        
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        Gizmos.DrawLine(transform.position + leftBoundary, transform.position + rightBoundary);
        
        // Draw attack range
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    #endregion
}
