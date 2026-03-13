using UnityEngine;
public class CameraBallTrack : MonoBehaviour
{
    public GameObject ball; // Reference to the ball GameObject

    void Start()
    {
        if (ball == null)
        {
            ball = GameObject.FindGameObjectWithTag("Ball");
        }
    }

    void Update()
    {
        if (ball != null)
        {
            Vector3 newPosition = transform.position;
            newPosition.x = Mathf.Clamp(ball.transform.position.x + ball.GetComponent<BallManager>().goingTo.x, -1f, 1f);
            transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * 1f);
        }
    }
}
