using System;
using UnityEngine;

/// <summary>
/// Script that lets you launch a ball from a specified position, velocity and angle.
/// Meant for testing bird abilities that require a ball.
/// HOW TO USE:
/// 1. Attach the script to an empty GameObject and position it where you want the ball to be launched from.
/// 2. Apply the ball in the inspector and set the launch parameters.
/// </summary>
[ExecuteAlways] // Let's script run in edit mode so you can see trajectory in the editor.
public class BallLauncher : MonoBehaviour
{
    public enum BallLaunchType
    {
        None,
        Serve,
        Spike,
        Block
    }

    [SerializeField] private Rigidbody ballPrefab;

    [Header("Launch Parameters")]
    [SerializeField] private BallLaunchType ballType = BallLaunchType.None;
    [SerializeField] private Vector3 launchVelocity;
    private Vector3 launchPosition => transform.position;

    [Header("Trajectory Settings")]
    [SerializeField] private int trajectoryPoints = 50;
    [SerializeField] private float trajectoryTime = 2f;

    private void Start()
    {
        if (Application.isPlaying) LaunchBall();
    }

    private void LaunchBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("Ball prefab is not assigned.");
            return;
        }

        ballPrefab.linearVelocity = Vector3.zero;
        ballPrefab.angularVelocity = Vector3.zero;
        ballPrefab.transform.position = launchPosition;

        ApplyLaunchTypeToGameState();
        ballPrefab.linearVelocity = launchVelocity;
    }

    private void OnDrawGizmos()
    {
        CalculateTrajectory();
    }

    private void ApplyLaunchTypeToGameState()
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null) return;


        switch (ballType)
        {
            case BallLaunchType.Serve:
                gameManager.gameState = GameManager.GameState.Served;
                break;
            case BallLaunchType.Spike:
                gameManager.gameState = GameManager.GameState.Spiked;
                break;
            case BallLaunchType.Block:
                gameManager.gameState = GameManager.GameState.Blocked;
                break;
            case BallLaunchType.None:
            default:
                break;
        }
    }

    private void CalculateTrajectory()
    {
        Vector3[] points = new Vector3[trajectoryPoints];
        for (int i = 0; i < trajectoryPoints; i++)
        {
            float t = i / (float)trajectoryPoints * trajectoryTime;
            points[i] = launchPosition + launchVelocity * t + 0.5f * Physics.gravity * t * t;
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < trajectoryPoints - 1; i++)
        {
            Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }
}
