using UnityEngine;

public class JiggleController : MonoBehaviour
{
    //the pinball to jiggle
    public Transform pinball;         
    public float jiggleAmount = 0.2f;
    public float jiggleSpeed = 10f;
    private Vector3 basePosition;
    
    void Start()
    {
        if (pinball == null){
            Debug.LogError("Pinball is not set");
        }
        basePosition = pinball.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (pinball == null){
            return;
        }
        float dt = Time.deltaTime;
        pinball.position = basePosition + new Vector3(0f, Mathf.Sin(Time.time * jiggleSpeed) * jiggleAmount, 0f);
    }
}
