using UnityEngine;

public class PaddleController : MonoBehaviour
{

    // A key for left paddle
    public KeyCode key = KeyCode.A;
    //basic field for rotation
    public float restAngle = 20f;
    public float flipAngle = -25f;
    public float flipSpeed = 720f;
    // use target angle to decide if the paddle is dropping or flipping
    private float currentAngle;
    private float targetAngle;
    private Quaternion baseRotation;
    private float previousAngle;
    private float angularVelocityRadPerSec;
    // half length of the paddle in world units along local X (used for collisions)
    public float halfLengthXZ = 4f;

    void Start()
    {
        //init
        baseRotation = transform.localRotation;
        currentAngle = restAngle;
        targetAngle = currentAngle;
        previousAngle = currentAngle;
        ApplyRotation();
    }

    void Update()
    {
        targetAngle = Input.GetKey(key) ? flipAngle : restAngle;
        float dt = Time.deltaTime;
        previousAngle = currentAngle;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, flipSpeed * dt);
        // compute signed angular velocity around Y axis (radians/sec)
        float angularVelocityDegPerSec = dt > 0f ? (currentAngle - previousAngle) / dt : 0f;
        angularVelocityRadPerSec = angularVelocityDegPerSec * Mathf.Deg2Rad;
        ApplyRotation();
    }

    private void ApplyRotation(){
        if (key == KeyCode.A){
            transform.localRotation = baseRotation * Quaternion.Euler(0f, currentAngle,0f);
        }else{
            transform.localRotation = baseRotation * Quaternion.Euler(0f, -currentAngle,0f);
        }
    }

    // Returns current angular velocity around world Y axis in radians/sec (signed)
    public float GetAngularVelocityRadPerSec(){
        return angularVelocityRadPerSec;
    }

    // Get paddle segment endpoints projected in XZ plane
    public void GetSegmentEndpointsXZ(out Vector2 a, out Vector2 b){
        Vector3 center = transform.position;
        Vector3 dir = transform.right; // local X axis is paddle length direction
        Vector3 pa = center - dir * halfLengthXZ;
        Vector3 pb = center + dir * halfLengthXZ;
        a = new Vector2(pa.x, pa.z);
        b = new Vector2(pb.x, pb.z);
    }

    // Linear velocity at a world-space point due to paddle rotation (ignores any translational motion)
    public Vector3 GetPointVelocity(Vector3 worldPoint){
        // v = omega x r, omega along +Y with magnitude angularVelocityRadPerSec
        Vector3 omega = angularVelocityRadPerSec * Vector3.up;
        Vector3 r = worldPoint - transform.position;
        return Vector3.Cross(omega, r);
    }
}
