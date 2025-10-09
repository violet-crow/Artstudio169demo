using UnityEngine;

public class NPCBehavior : MonoBehaviour
{
    public float speed;
    public float moveRate;
    public int dirX;
    public int dirY;

    private float moveCounter;

    private new Rigidbody2D rigidbody2D { get { return GetComponent<Rigidbody2D>() ?? default(Rigidbody2D); } }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (rigidbody2D)
        {
            if (moveCounter > moveRate)
            {
                ChangeDirection();
                moveCounter = 0f;
            }

            Vector2 vel = new Vector2(dirX * speed, dirY * speed);

            rigidbody2D.linearVelocity = Vector2.Lerp(rigidbody2D.linearVelocity, vel, Time.deltaTime * 10f);

            moveCounter += Time.deltaTime;
        }
    }

    private void ChangeDirection()
    {
        dirX = Random.Range(-1, 1); // -1 or 0 or 1
        dirY = Random.Range(-1, 1); // -1 or 0 or 1
    }
}
