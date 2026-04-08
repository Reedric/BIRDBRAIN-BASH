using UnityEngine;

using System.Collections.Generic;

public class FresnelManager : MonoBehaviour
{
    [Header("Fresnel Materials Per Player")]
    public List<Material> playerFresnelMaterials; // Assign unique fresnel materials for each player in Inspector
    public Material defaultBallMaterial; // Assign the default ball material in Inspector

    private Renderer[] ballRenderers;
    private int currentHighlightPlayer = -1;
    private BallInteract[] allPlayers;
    private AIBehavior[] allAIs;
    private Rigidbody ballRb;
    private bool fresnelActive = false;

    void Start()
    {
        // Find the ball's renderers
        var ballObj = BallManager.Instance.gameObject;
        ballRenderers = ballObj.GetComponentsInChildren<Renderer>();
        ballRb = ballObj.GetComponent<Rigidbody>();
        // Find all BallInteract scripts (players)
        allPlayers = FindObjectsOfType<BallInteract>();
        // Find all AIBehavior scripts (AIs)
        allAIs = FindObjectsOfType<AIBehavior>();
    }

    void Update()
    {
        if (ballRb == null || ballRenderers == null || ballRenderers.Length == 0) return;

        int highlightIndex = -1;
        float closestDist = float.MaxValue;
        bool isAI = false;

        // Check all players
        for (int i = 0; i < allPlayers.Length; i++)
        {
            var player = allPlayers[i];
            if (player.IsPlayerNearBall())
            {
                float dist = Vector3.Distance(player.transform.position, ballRb.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    highlightIndex = i;
                    isAI = false;
                }
            }
        }

        // Check all AIs
        for (int i = 0; i < allAIs.Length; i++)
        {
            var ai = allAIs[i];
            if (ai != null && IsAINearBall(ai))
            {
                float dist = Vector3.Distance(ai.transform.position, ballRb.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    highlightIndex = i;
                    isAI = true;
                }
            }
        }

        // Ball is in someone's radius
        if (highlightIndex != -1)
        {
            int matIndex = isAI ? allPlayers.Length + highlightIndex : highlightIndex;
            if (!fresnelActive || matIndex != currentHighlightPlayer)
            {
                ApplyFresnelMaterial(matIndex);
            }
        }
        else if (fresnelActive)
        {
            RestoreDefaultMaterial();
        }

        // Remove fresnel if ball is hit or touches ground
        if (fresnelActive && BallIsInactive())
        {
            RestoreDefaultMaterial();
        }
    }

    private void ApplyFresnelMaterial(int matIndex)
    {
        if (matIndex < 0 || matIndex >= playerFresnelMaterials.Count) return;
        Material fresnelMat = playerFresnelMaterials[matIndex];
        foreach (var rend in ballRenderers)
        {
            if (rend != null && fresnelMat != null)
                rend.material = fresnelMat;
        }
        fresnelActive = true;
        currentHighlightPlayer = matIndex;
    }

    // Helper for AI proximity
    private bool IsAINearBall(AIBehavior ai)
    {
        if (ai == null) return false;
        var contactPointField = ai.GetType().GetField("contactPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var interactionRadiusField = ai.GetType().GetField("interactionRadius", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Transform contactPoint = contactPointField != null ? (Transform)contactPointField.GetValue(ai) : ai.transform;
        float radius = interactionRadiusField != null ? (float)interactionRadiusField.GetValue(ai) : 2f;
        if (contactPoint == null) return false;
        float distance = Vector3.Distance(contactPoint.position, ballRb.transform.position);
        return distance <= radius;
    }

    private void RestoreDefaultMaterial()
    {
        foreach (var rend in ballRenderers)
        {
            if (rend != null && defaultBallMaterial != null)
                rend.material = defaultBallMaterial;
        }
        fresnelActive = false;
        currentHighlightPlayer = -1;
    }

    // Ball is hit or touches ground (expand as needed)
    private bool BallIsInactive()
    {
        var gm = GameManager.Instance;
        // Not in play if point ended or ball is at/under ground
        return gm.gameState == GameManager.GameState.PointEnd || ballRb.transform.position.y <= 0.1f;
    }
}
