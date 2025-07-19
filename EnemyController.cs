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
    [SerializeField] private float waypointWaitTime = 180f;
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
    private int currentComboType; // 0-3 for different combo types
    
    [Header("Combo Settings")]
    [SerializeField] private ComboData[] combos = new ComboData[4];
    
    [System.Serializable]
    public class ComboData
    {
        public string comboName;
        public float[] attackTimings; // Time between each attack in the combo
        public float[] attackDamages; // Damage for each attack in the combo
        public string[] animationTriggers; // Animation trigger names for each attack
        public float comboRange = 1.5f; // Range for this specific combo
    }

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
        
        InitializeCombos();
        ChangeState(EnemyState.Patrol);
    }
    
    private void InitializeCombos()
    {
        // Initialize default combo data if not set in inspector
        if (combos == null || combos.Length != 4)
        {
            combos = new ComboData[4];
        }
        
        // Combo 1: Quick Jabs
        if (combos[0] == null) combos[0] = new ComboData();
        if (string.IsNullOrEmpty(combos[0].comboName)) combos[0].comboName = "Quick Jabs";
        if (combos[0].attackTimings == null || combos[0].attackTimings.Length == 0)
            combos[0].attackTimings = new float[] { 0.3f, 0.3f, 0.4f };
        if (combos[0].attackDamages == null || combos[0].attackDamages.Length == 0)
            combos[0].attackDamages = new float[] { 8f, 8f, 12f };
        if (combos[0].animationTriggers == null || combos[0].animationTriggers.Length == 0)
            combos[0].animationTriggers = new string[] { "QuickJab1" };
        combos[0].comboRange = 1.2f;
        
        // Combo 2: Heavy Strikes
        if (combos[1] == null) combos[1] = new ComboData();
        if (string.IsNullOrEmpty(combos[1].comboName)) combos[1].comboName = "Heavy Strikes";
        if (combos[1].attackTimings == null || combos[1].attackTimings.Length == 0)
            combos[1].attackTimings = new float[] { 0.8f, 0.9f, 1.2f };
        if (combos[1].attackDamages == null || combos[1].attackDamages.Length == 0)
            combos[1].attackDamages = new float[] { 15f, 18f, 25f };
        if (combos[1].animationTriggers == null || combos[1].animationTriggers.Length == 0)
            combos[1].animationTriggers = new string[] { "HeavyStrike1" };
        combos[1].comboRange = 1.8f;
        
        // Combo 3: Spinning Attacks
        if (combos[2] == null) combos[2] = new ComboData();
        if (string.IsNullOrEmpty(combos[2].comboName)) combos[2].comboName = "Spinning Attacks";
        if (combos[2].attackTimings == null || combos[2].attackTimings.Length == 0)
            combos[2].attackTimings = new float[] { 0.6f, 0.5f, 0.7f, 0.8f };
        if (combos[2].attackDamages == null || combos[2].attackDamages.Length == 0)
            combos[2].attackDamages = new float[] { 10f, 12f, 14f, 20f };
        if (combos[2].animationTriggers == null || combos[2].animationTriggers.Length == 0)
            combos[2].animationTriggers = new string[] { "SpinAttack1"};
        combos[2].comboRange = 2.0f;
        
        // Combo 4: Uppercut Combo
        if (combos[3] == null) combos[3] = new ComboData();
        if (string.IsNullOrEmpty(combos[3].comboName)) combos[3].comboName = "Uppercut Combo";
        if (combos[3].attackTimings == null || combos[3].attackTimings.Length == 0)
            combos[3].attackTimings = new float[] { 0.5f, 0.4f, 1.0f };
        if (combos[3].attackDamages == null || combos[3].attackDamages.Length == 0)
            combos[3].attackDamages = new float[] { 12f, 10f, 30f };
        if (combos[3].animationTriggers == null || combos[3].animationTriggers.Length == 0)
            combos[3].animationTriggers = new string[] { "UppercutFinish" };
        combos[3].comboRange = 1.5f;
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
        if (distanceToTarget <= GetCurrentComboRange())
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
        
        // Reset attack state completely to prevent overlapping combos
        isAttacking = false;
        currentComboCount = 0;
        lastAttackTime = Time.time;
        
        // Clear any existing animation triggers to prevent conflicts
        animator.ResetTrigger("QuickJab1");
        animator.ResetTrigger("QuickJab2");
        animator.ResetTrigger("QuickJab3");
        animator.ResetTrigger("HeavyStrike1");
        animator.ResetTrigger("HeavyStrike2");
        animator.ResetTrigger("HeavyStrike3");
        animator.ResetTrigger("SpinAttack1");
        animator.ResetTrigger("SpinAttack2");
        animator.ResetTrigger("SpinAttack3");
        animator.ResetTrigger("SpinAttack4");
        animator.ResetTrigger("UppercutSetup");
        animator.ResetTrigger("UppercutHit");
        animator.ResetTrigger("UppercutFinish");
        
        // Select a random combo type or cycle through them
        SelectComboType();
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
        if (distanceToTarget > GetCurrentComboRange() * 1.2f)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Check if we can do another attack in the combo
        ComboData currentCombo = combos[currentComboType];
        if (!isAttacking && currentComboCount < currentCombo.attackTimings.Length)
        {
            float timeSinceLastAttack = Time.time - lastAttackTime;
            float requiredDelay = currentCombo.attackTimings[currentComboCount];
            
            if (timeSinceLastAttack >= requiredDelay)
            {
                TriggerAttack();
            }
        }
        // Reset combo if too much time has passed or combo is complete
        else if (Time.time >= lastAttackTime + comboResetTime || currentComboCount >= currentCombo.attackTimings.Length)
        {
            // Ensure we're not stuck in attacking state
            isAttacking = false;
            currentComboCount = 0;
            
            // Clear any lingering animation triggers
            ClearAllAnimationTriggers();
            
            ChangeState(EnemyState.Chase);
        }
    }

    private void SelectComboType()
    {
        // Always select a different combo type to ensure variety
        int newComboType;
        do
        {
            newComboType = Random.Range(0, combos.Length);
        } while (newComboType == currentComboType && combos.Length > 1);
        
        currentComboType = newComboType;
        Debug.Log($"Selected combo: {combos[currentComboType].comboName}");
    }

    private void TriggerAttack()
    {
        // Prevent triggering attack if already attacking to avoid overlaps
        if (isAttacking)
        {
            Debug.LogWarning("Attempted to trigger attack while already attacking - preventing overlap");
            return;
        }
        
        isAttacking = true;
        lastAttackTime = Time.time;
        
        ComboData currentCombo = combos[currentComboType];
        
        // Trigger the appropriate animation for this attack in the combo
        if (currentComboCount < currentCombo.animationTriggers.Length)
        {
            string triggerName = currentCombo.animationTriggers[currentComboCount];
            
            // Clear all other animation triggers before setting the new one
            ClearAllAnimationTriggers();
            
            animator.SetTrigger(triggerName);
            Debug.Log($"Triggered animation: {triggerName} (Attack {currentComboCount + 1} of {currentCombo.comboName})");
        }
        
        currentComboCount++;
    }

    private float GetCurrentComboRange()
    {
        if (combos != null && currentComboType < combos.Length && combos[currentComboType] != null)
        {
            return combos[currentComboType].comboRange;
        }
        return attackRange; // Fallback to default attack range
    }

    private float GetCurrentAttackDamage()
    {
        ComboData currentCombo = combos[currentComboType];
        int attackIndex = currentComboCount - 1; // -1 because we increment before calling this
        
        if (attackIndex >= 0 && attackIndex < currentCombo.attackDamages.Length)
        {
            return currentCombo.attackDamages[attackIndex];
        }
        return attackDamage; // Fallback to default damage
    }

    // Call this from animation events
    public void OnAttackHit()
    {
        if (currentTarget != null)
        {
            // Check if target is still in front and in range
            Vector3 directionToTarget = (currentTarget.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, directionToTarget) > 0.5f && 
                Vector3.Distance(transform.position, currentTarget.transform.position) <= GetCurrentComboRange() * 1.2f)
            {
                // Apply damage to the target
                float damage = GetCurrentAttackDamage();
                var healthComponent = currentTarget.GetComponent<IDamageable>();
                if (healthComponent != null)
                {
                    healthComponent.TakeDamage(damage);
                    Debug.Log($"Dealt {damage} damage to {currentTarget.name} with {combos[currentComboType].comboName}");
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
        
        ComboData currentCombo = combos[currentComboType];
        
        // Clear animation triggers to prevent lingering states
        ClearAllAnimationTriggers();
        
        // If we've reached the end of this combo, reset and chase
        if (currentComboCount >= currentCombo.attackTimings.Length)
        {
            currentComboCount = 0;
            ChangeState(EnemyState.Chase);
        }
    }

    private void ClearAllAnimationTriggers()
    {
        // Clear all possible animation triggers to prevent conflicts
        animator.ResetTrigger("QuickJab1");
        animator.ResetTrigger("QuickJab2");
        animator.ResetTrigger("QuickJab3");
        animator.ResetTrigger("HeavyStrike1");
        animator.ResetTrigger("HeavyStrike2");
        animator.ResetTrigger("HeavyStrike3");
        animator.ResetTrigger("SpinAttack1");
        animator.ResetTrigger("SpinAttack2");
        animator.ResetTrigger("SpinAttack3");
        animator.ResetTrigger("SpinAttack4");
        animator.ResetTrigger("UppercutSetup");
        animator.ResetTrigger("UppercutHit");
        animator.ResetTrigger("UppercutFinish");
    }

    private void ExitAttackState()
    {
        isAttacking = false;
        navMeshAgent.isStopped = false;
        
        // Clear all animation triggers when exiting attack state
        ClearAllAnimationTriggers();
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
