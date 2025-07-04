using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public Transform cameraTrans;
    public PlayerController player;
    public float lockOnDetectionRadius;

    // Object Targeting Parameters
    public LayerMask enemyLayer;

    // Get SkinnedMeshHighlightercomponent
    public SkinnedMeshHighlighter MeshHighlighter { get; private set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MeshHighlighter = GetComponent<SkinnedMeshHighlighter>();
    }

    // Update is called once per frame
    void Update()
    {
        // Handle lock-on when Tab is pressed and in combat mode
        if (player.combatMode)
        {
            var closestEnemy = GetClosestEnemyToPlayer();
            if (closestEnemy != null)
            {
                // If we already have a target, remove its highlight
                player.targetEnemy?.MeshHighlighter?.HighlightMesh(false);
                
                // Set new target and highlight it
                player.targetEnemy = closestEnemy;
                player.targetEnemy.MeshHighlighter.HighlightMesh(true);
            }
        }
        // Clear target when exiting combat mode
        else if (Input.GetKeyDown(KeyCode.Tab) && !player.combatMode && player.targetEnemy != null)
        {
            player.targetEnemy.MeshHighlighter.HighlightMesh(false);
            player.targetEnemy = null;
        }
    }
    
    public Vector3 GetTargetingDirection()
    {
        var vectorFromCamera = player.transform.position - cameraTrans.position;
        vectorFromCamera.y = 0f;

        Debug.DrawLine(vectorFromCamera, transform.forward);
        return vectorFromCamera.normalized;
    }

    public EnemyController GetClosestEnemyToPlayer()
    {
        var targetingDirection = GetTargetingDirection(); // Target direction from camera to player

        float minDistance = Mathf.Infinity;
        EnemyController closestEnemy = null;

        var enemiesInRange = Physics.OverlapSphere(player.transform.position, lockOnDetectionRadius, enemyLayer);
        
        foreach (var enemy in enemiesInRange)
        {
            var vectorToEnemy = enemy.transform.position - player.transform.position;
            vectorToEnemy.y = 0f;

            // Get Angle from targeting direction to vectorToObject
            float angle = Vector3.Angle(targetingDirection, vectorToEnemy);
            float distance = vectorToEnemy.magnitude * Mathf.Sin(angle * Mathf.Deg2Rad);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = enemy.GetComponent<EnemyController>();
            }
        }

        if (closestEnemy != null)
        {
            // Debug.Log("Closest Object: " + closestEnemy.name);
        }
        
        return closestEnemy;
    }
}
