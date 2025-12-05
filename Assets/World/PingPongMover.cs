using UnityEngine;

public class PingPongMover : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 1f;

    void Update()
    {
        if (pointA == null || pointB == null)
            return;

        SinPingPong();
    }

    void PingPongMove()
    {
        // t goes 0 → 1 → 0 → 1... based on time
        float t = Mathf.PingPong(Time.time * speed, 1f);

        // Move between A and B
        transform.position = Vector3.Lerp(pointA.position, pointB.position, t);
    }

    float t = 0;
    int direction = 1;

    void PingPongManual()
    {
        if (!pointA || !pointB)
            return;

        // Move t forward or backward
        t += direction * speed * Time.deltaTime;

        // Clamp and flip direction
        if (t >= 1f)
        {
            t = 1f;
            direction = -1;
        }
        else if (t <= 0f)
        {
            t = 0f;
            direction = 1;
        }

        // Apply position
        transform.position = Vector3.Lerp(pointA.position, pointB.position, t);
    }

    void SinPingPong()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        transform.position = Vector3.Lerp(pointA.position, pointB.position, t);
    }
}
