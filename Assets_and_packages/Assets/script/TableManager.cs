using UnityEngine;
using System.Collections.Generic;

public class TableManager : MonoBehaviour
{
    //the playable areaof the table
    public Vector2 minXZ;
    public Vector2 maxXZ;
    //the pinball prefab for spawning
    public Transform pinballPrefab;
    public float ballRadius = 2f;
    //the cylinder to create
    public Transform cylinderPrefab;
    public int cylinderCount = 3;
    public float cylinderRadius = 2f;
    public float cylinderHeight = 5f;
    //the triangle to create
    public Transform trianglePrefab;
    public int triangleCount = 4;
    public float triangleHeight = 5f;
    public float triangleOffset = 0.5f;
    //padding between obstacles
    public float padding =5f;
    // number of balls allowed concurrently
    public int maxSimultaneousBalls = 2;
    //list of cylinders
    public List<Transform> cylinders;
    //list of triangles
    public List<Transform> triangles;
    
    //ball physics
    public Vector2 tiltAccelXZ = new Vector2(0f, -2f); 
    public float maxBallSpeed = 100f;
	// paddles on the table
	public PaddleController[] paddles;
	// scales the additional velocity imparted by moving paddles
	public float movingPaddleBoost = 100;
    
    //ball data structure
    public struct Ball
    {
        public Transform t;
        public Vector3 v;
    }
    public List<Ball> balls = new List<Ball>();
    // restitution for ball-ball collisions
    public float ballBallRestitution = 0.9f;

    

    void Start()
    {
        //place cylinders and triangles on start
        placeCylinders();
        placeTriangles();
		// discover paddles in scene if not assigned
		if (paddles == null || paddles.Length == 0) paddles = FindObjectsOfType<PaddleController>();
        
    }

    //place cylinders and check if they are too close to each other
    void placeCylinders(){
        cylinders = new List<Transform>();
        for (int i = 0; i < cylinderCount; i++){
            //init a cylinder
            Transform cylinder = Instantiate(cylinderPrefab);
            Vector3 tempPosition;
            bool tooClose;
            //do while loop to generate a random position for the cylinder
            do {
                tempPosition = new Vector3(Random.Range(minXZ.x, maxXZ.x), 0f, Random.Range(minXZ.y, maxXZ.y));
                tooClose = false;
                
                // Check if cylinder is too close to boundaries (leave margin for ball to pass)
                float cylMargin = ballRadius * 1.5f;
                if (tempPosition.x - cylMargin < minXZ.x || tempPosition.x + cylMargin > maxXZ.x ||
                    tempPosition.z - cylMargin < minXZ.y || tempPosition.z + cylMargin > maxXZ.y) {
                    tooClose = true;
                }
                
                // Check if too close to existing cylinders (XZ plane only)
                if (!tooClose) {
                    foreach (Transform existingCylinder in cylinders){
                    Vector3 existingPos = existingCylinder.position;
                    // Check horizontal distance (XZ plane)
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(tempPosition.x, tempPosition.z),
                        new Vector2(existingPos.x, existingPos.z)
                    );
                    //check if the cylinder is too close to the existing cylinders
                    if (horizontalDistance < cylinderRadius * 2 + padding){
                        tooClose = true;
                        break;
                    }
                }
                }
            } while (tooClose);
            
            cylinder.position = tempPosition;
            cylinder.localScale = new Vector3(cylinderRadius * 2, cylinderHeight, cylinderRadius * 2);
            cylinders.Add(cylinder);
        }
    }

    //place triangles function that is similar to the cylinders function
    void placeTriangles(){
        triangles = new List<Transform>();
        for (int i = 0; i < triangleCount; i++){
            Transform triangle = Instantiate(trianglePrefab);
            Vector3 tempPosition;
            bool tooClose;
            
            do {
                tempPosition = new Vector3(Random.Range(minXZ.x, maxXZ.x), 0f, Random.Range(minXZ.y, maxXZ.y));
                tooClose = false;
                
                // Check if triangle is too close to boundaries (leave margin for ball to pass)
                float triMargin = ballRadius * 1.5f;
                if (tempPosition.x - triMargin < minXZ.x || tempPosition.x + triMargin > maxXZ.x ||
                    tempPosition.z - triMargin < minXZ.y || tempPosition.z + triMargin > maxXZ.y) {
                    tooClose = true;
                }
                
                // Check if too close to existing triangles (XZ plane only)
                if (!tooClose) {
                    foreach (Transform existingTriangle in triangles){
                    Vector3 existingPos = existingTriangle.position;
                    // Check horizontal distance (XZ plane)
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(tempPosition.x, tempPosition.z),
                        new Vector2(existingPos.x, existingPos.z)
                    );
                    
                    if (horizontalDistance < padding + ballRadius + triangleOffset){
                        tooClose = true;
                        break;
                    }
                }
                
                // Check if too close to cylinders (XZ plane only)
                if (!tooClose && cylinders != null){
                    foreach (Transform existingCylinder in cylinders){
                        Vector3 existingPos = existingCylinder.position;
                        // Check horizontal distance (XZ plane)
                        float horizontalDistance = Vector2.Distance(
                            new Vector2(tempPosition.x, tempPosition.z),
                            new Vector2(existingPos.x, existingPos.z)
                        );
                        
                        if (horizontalDistance < cylinderRadius + padding + triangleOffset){
                            tooClose = true;
                            break;
                        }
                    }
                }
                }
            } while (tooClose);
            
            triangle.position = tempPosition;
            
            triangle.rotation = Quaternion.Euler(90f, 0f, Random.Range(45f, 335f)); // Random Z rotation to avoid parallel edges
            
            triangle.localScale = new Vector3(1f, 1f, triangleHeight);
            triangles.Add(triangle);
        }
    }



////////////////////////////////////////////////////////////
///      UPDATE FUNCTION
/////////////////////////////////////////////////////////////
    void Update()
    {
        //space key spawns a ball
        if (Input.GetKeyDown(KeyCode.Space)) TrySpawnBallRandom();
        
        // Z key jiggles table (once per press)
        if (Input.GetKey(KeyCode.Z)) {
            JiggleBalls();
        }
        //update the ball per frame
        StepBalls(Time.deltaTime);
    }

    void TrySpawnBallRandom()
    {
        if (balls.Count >= maxSimultaneousBalls) { return; }
        if (pinballPrefab == null) { Debug.LogWarning("no assignment of pinballPrefab"); return; }
        
        Vector3 tempPosition;
        bool isOverlapping;
        int attempts = 0;
        const int maxAttempts = 100;
        
        do {
            tempPosition = new Vector3(Random.Range(minXZ.x, maxXZ.x), 0f, Random.Range(minXZ.y, maxXZ.y));
            isOverlapping = false;
            attempts++;
            
            // Keep a margin from boundaries so the spawn doesn't instantly collide
            if (!isOverlapping){
                float spawnMargin = ballRadius + padding;
                if (tempPosition.x - spawnMargin < minXZ.x || tempPosition.x + spawnMargin > maxXZ.x ||
                    tempPosition.z - spawnMargin < minXZ.y || tempPosition.z + spawnMargin > maxXZ.y){
                    isOverlapping = true;
                }
            }

            // Check if overlapping with existing balls
            if (!isOverlapping && balls != null){
                for (int i = 0; i < balls.Count; i++){
                    Vector3 bp = balls[i].t.position;
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(tempPosition.x, tempPosition.z),
                        new Vector2(bp.x, bp.z)
                    );
                    if (horizontalDistance < ballRadius * 2f + 0.01f){
                        isOverlapping = true;
                        break;
                    }
                }
            }

            // Check if overlapping with cylinders
            if (cylinders != null){
                foreach (Transform existingCylinder in cylinders){
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(tempPosition.x, tempPosition.z),
                        new Vector2(existingCylinder.position.x, existingCylinder.position.z)
                    );
                    //check if the ball is overlapping with the cylinder
                    if (horizontalDistance < cylinderRadius + ballRadius){
                        isOverlapping = true;
                        //if we find any overlapping cylinder, we break the loop
                        break;
                    }
                }
            }
            
            // Check if overlapping with triangles
            if (!isOverlapping && triangles != null){
                foreach (Transform existingTriangle in triangles){
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(tempPosition.x, tempPosition.z),
                        new Vector2(existingTriangle.position.x, existingTriangle.position.z)
                    );
                    if (horizontalDistance < ballRadius + triangleOffset){
                        isOverlapping = true;
                        break;
                    }
                }
            }
            
            
        } while (isOverlapping && attempts < maxAttempts);
        
        if (attempts >= maxAttempts) {
            Debug.LogWarning("No room to spawn ball after " + maxAttempts + " attempts.");
            return;
        }

        var t = Instantiate(pinballPrefab);
        t.position = tempPosition;
        t.localScale = Vector3.one * ballRadius;
        
        balls.Add(new Ball { t = t, v = Vector3.zero }); // Ball starts with no velocity
    }
    

    void JiggleBalls()
    {
        if (balls.Count == 0) return;
        
        // Generate jiggle directions for all balls
        float jiggleDirectionX = Random.Range(-1f, 1f) > 0f ? 1f : -1f; // Left or right
        float jiggleDirectionZ = Random.Range(-1f, 1f) > 0f ? 1f : -1f; // Forward or backward
        float jiggleForce = 1f; // How strong the jiggle is
        
        for (int i = 0; i < balls.Count; i++)
        {
            var ball = balls[i];
            // Add velocity in both X and Z directions
            ball.v += new Vector3(jiggleDirectionX * jiggleForce, 0f, jiggleDirectionZ * jiggleForce);
            balls[i] = ball;
        }
    }

    void StepBalls(float dt)
    {
        if (dt <= 0f) return;

        for (int i = balls.Count - 1; i >= 0; --i)
        {
            var b = balls[i];
            
            // constant downward acceleration (using fixed frame rate)
            b.v += new Vector3(0f, 0f, -20f) * dt;

            
            // Speed cap - limit ball velocity to maxBallSpeed
            if (b.v.magnitude > maxBallSpeed)
            {
                b.v = b.v.normalized * maxBallSpeed;
            }

            // integrate
            Vector3 p = b.t.position + b.v * dt;

            //energy loss

            // Check collisions with objects and get corrected position
            p = CheckCollisions(ref b, p);

            // Update the ball data in the list
            balls[i] = b;



            b.t.position = new Vector3(p.x, 0f, p.z);

            //destroy the ball when it reaches the bottom
            if (p.z <= -27.5f)
            {
                Destroy(b.t.gameObject);
                balls.RemoveAt(i);
            }
        }

		// Resolve ball-ball collisions after positions are updated
		if (balls.Count >= 2)
		{
			ResolveBallBallCollisions(ballBallRestitution, dt);
		}
    }













////////////////////////////////////////////////////////////
///      CHECK COLLISIONS FUNCTION
/////////////////////////////////////////////////////////////

    Vector3 CheckCollisions(ref Ball ball, Vector3 position)
    {
        //check collision with the boundaries
        Vector3 correctedPosition = position;
        const float restitution = 0.9f;

        // Left wall at -15
        if (correctedPosition.x - ballRadius < -15f && ball.v.x < 0f)
        {
            correctedPosition.x = -15f + ballRadius;     // snap outside
            ball.v.x = -ball.v.x * restitution; // reflect with bounce
        }

        // Right wall at +15
        if (correctedPosition.x + ballRadius > 15f && ball.v.x > 0f)
        {
            correctedPosition.x = 15f - ballRadius;
            ball.v.x = -ball.v.x * restitution;
        }

        // top boundary only (let ball fall through bottom)
        if (correctedPosition.z + ballRadius > maxXZ.y && ball.v.z > 0f) { 
            correctedPosition.z = maxXZ.y - ballRadius; 
            ball.v.z = -ball.v.z * restitution; 
        }

		//check collision with the inclined boundaries
		CollideWithSegment(ref ball, ref correctedPosition, new Vector2(-14f, -14f), new Vector2(-7f, -20f), restitution);
		CollideWithSegment(ref ball, ref correctedPosition, new Vector2(7f, -20f), new Vector2(14f, -14f), restitution);

		// check collision with the paddles
		if (paddles != null)
		{
			for (int iP = 0; iP < paddles.Length; ++iP)
			{
				var paddle = paddles[iP];
				Vector2 pa, pb;
				paddle.GetSegmentEndpointsXZ(out pa, out pb);
				CollideWithMovingSegment(ref ball, ref correctedPosition, paddle, pa, pb, restitution);
			}
		}
        
        // Check collision with cylinders (add energy)
        if (cylinders != null)
        {
            foreach (Transform cylinder in cylinders)
            {
                Vector2 ballPos2D = new Vector2(correctedPosition.x, correctedPosition.z);
                Vector2 cylinderPos2D = new Vector2(cylinder.position.x, cylinder.position.z);
                float distance = Vector2.Distance(ballPos2D, cylinderPos2D);
                
                if (distance < cylinderRadius + ballRadius)
                {
                    // Calculate collision normal and separation
                    Vector2 collisionNormal = (ballPos2D - cylinderPos2D).normalized;
                    if (distance == 0f) collisionNormal = Vector2.right; // Handle exact center collision
                    
                    // Separate ball from cylinder
                    float overlap = (cylinderRadius + ballRadius) - distance;
                    Vector2 separation = collisionNormal * overlap;
                    correctedPosition.x += separation.x;
                    correctedPosition.z += separation.y;
                    
                    // Bounce with energy gain - more realistic physics
                    Vector3 direction = new Vector3(collisionNormal.x, 0f, collisionNormal.y);
                    float currentSpeed = ball.v.magnitude;
                    float newSpeed = currentSpeed * 1.3f; // 30% energy gain (more reasonable)
                    ball.v = direction * newSpeed;
                }
            }
        }

        // Check collision with triangles (remove energy)
        if (triangles != null)
        {
            foreach (Transform triangle in triangles)
            {
                Vector2 ballPos2D = new Vector2(correctedPosition.x, correctedPosition.z);
                Vector2 trianglePos2D = new Vector2(triangle.position.x, triangle.position.z);
                float distance = Vector2.Distance(ballPos2D, trianglePos2D);
                
                if (distance < ballRadius + triangleOffset)
                {
                    // Calculate collision normal and separation
                    Vector2 collisionNormal = (ballPos2D - trianglePos2D).normalized;
                    if (distance == 0f) collisionNormal = Vector2.right; // Handle exact center collision
                    
                    // Separate ball from triangle
                    float overlap = (ballRadius + triangleOffset) - distance;
                    Vector2 separation = collisionNormal * overlap;
                    correctedPosition.x += separation.x;
                    correctedPosition.z += separation.y;
                    
                    // Bounce with energy loss 
                    Vector3 direction = new Vector3(collisionNormal.x, 0f, collisionNormal.y);
                    float currentSpeed = ball.v.magnitude;
                    float newSpeed = currentSpeed * 0.7f ; 
                    ball.v = direction * newSpeed;
                }
            }
        }
        
        return correctedPosition;
    }

	
	//collision resolution for the ball inclined boundary
	void CollideWithSegment(ref Ball ball, ref Vector3 correctedPosition, Vector2 segA, Vector2 segB, float restitution)
	{
        // the math behind is that we have ball center p and segment endpoints a and b
        // we want to find the closest point on the segment to the ball center
        // we do that by projecting the ball center onto the segment and clamping the result to [0,1]
        // and then time with the segment length and add the starting pointto get the closest point
		Vector2 p = new Vector2(correctedPosition.x, correctedPosition.z);
		Vector2 ab = segB - segA;
        //ab length squared
		float abLenSq = ab.sqrMagnitude;
		if (abLenSq <= 1e-6f) return; 

		// if projection is less than 0 or greater than 1, we are beyond the segment and closest is the starting or ending point
		float t = Vector2.Dot(p - segA, ab) / abLenSq;
		t = Mathf.Clamp01(t);
		Vector2 closest = segA + t * ab;
		Vector2 separationVec = p - closest;
		float dist = separationVec.magnitude;

		if (dist < ballRadius)
		{
			// Determine collision normal
			Vector2 n;
			if (dist > 1e-6f)
				n = separationVec / dist;
			else
			{
				// Use a stable normal perpendicular to the segment
                // math behind is that for x y we have -y x for dot product to be zero
				Vector2 tangent = ab.normalized;
				n = new Vector2(-tangent.y, tangent.x); 
			}

			// Only reflect if moving into the surface
			Vector2 v2 = new Vector2(ball.v.x, ball.v.z);
			float vn = Vector2.Dot(v2, n);
            // if dot product is negative, we know it is moving opposite to the normal which is inwards of the segment
			if (vn < 0f)
			{
				// Separate out of the wall by overlap along normal
				float overlap = ballRadius - dist;
				Vector2 correction = n * overlap;
				correctedPosition.x += correction.x;
				correctedPosition.z += correction.y;

				// Reflect velocity across the normal with restitution
                // this is the standard reflection formula
                // if perfect bounce we should have vReflected2 = v2 - 2 * vn * n
				Vector2 vReflected2 = v2 - (1f + restitution) * vn * n;
				ball.v = new Vector3(vReflected2.x, 0f, vReflected2.y);
			}
			else
			{
				// If moving away but intersecting due to numerical drift, just separate without reflecting
				float overlap = ballRadius - dist;
				Vector2 correction = n * overlap;
				correctedPosition.x += correction.x;
				correctedPosition.z += correction.y;
			}
		}
	}

	// Moving segment collision with relative-velocity reflection
	void CollideWithMovingSegment(ref Ball ball, ref Vector3 correctedPosition, PaddleController paddle, Vector2 segA, Vector2 segB, float restitution)
	{
		Vector2 p = new Vector2(correctedPosition.x, correctedPosition.z);
		Vector2 ab = segB - segA;
		float abLenSq = ab.sqrMagnitude;
		if (abLenSq <= 1e-6f) return;

		float t = Vector2.Dot(p - segA, ab) / abLenSq;
		t = Mathf.Clamp01(t);
		Vector2 closest = segA + t * ab;
		Vector2 separationVec = p - closest;
		float dist = separationVec.magnitude;
		if (dist >= ballRadius) return;

		Vector2 n;
		if (dist > 1e-6f)
			n = separationVec / dist;
		else
		{
			Vector2 tangent = ab.normalized;
			n = new Vector2(-tangent.y, tangent.x);
		}

		// get paddle point velocity at contact point
		Vector3 closest3 = new Vector3(closest.x, 0f, closest.y);
		Vector3 paddleVel3 = paddle.GetPointVelocity(closest3);
		Vector2 paddleVel2 = new Vector2(paddleVel3.x, paddleVel3.z);

		// Separate first
		float overlap = ballRadius - dist;
		Vector2 correction = n * overlap;
		correctedPosition.x += correction.x;
		correctedPosition.z += correction.y;

		
        // we first find the relative velocity of the ball with respect to the paddle
		Vector2 v2 = new Vector2(ball.v.x, ball.v.z);
		float vnRel = Vector2.Dot(v2 - paddleVel2, n);
		if (vnRel < 0f)
		{
			// Reflect relative velocity
			Vector2 vRelReflected = (v2 - paddleVel2) - (1f + restitution) * vnRel * n;
			// If paddle is moving toward the ball along the normal, boost 
			float vPaddleAlongNormal = Vector2.Dot(paddleVel2, n);
			float boost = vPaddleAlongNormal > 0f ? movingPaddleBoost : 1f;
            // change back to world space
			Vector2 vOut = vRelReflected + paddleVel2 * boost;
			ball.v = new Vector3(vOut.x, 0f, vOut.y);
		}
	}

	// Pairwise collision resolution for balls 
	void ResolveBallBallCollisions(float restitution, float dt)
	{
		for (int i = 0; i < balls.Count; i++)
		{
			for (int j = i + 1; j < balls.Count; j++)
			{
				var bi = balls[i];
				var bj = balls[j];
				Vector2 pi = new Vector2(bi.t.position.x, bi.t.position.z);
				Vector2 pj = new Vector2(bj.t.position.x, bj.t.position.z);
				Vector2 diff = pi - pj;
				float dist = diff.magnitude;
				float minDist = ballRadius * 2f;
				if (dist < minDist && dist > 1e-6f)
				{
                    // for circle circle collision we use the normal vector is exactly the normalized difference vector
					Vector2 n = diff / dist;
					float overlap = minDist - dist;
					// separate positions equally
					Vector2 correction = n * (overlap * 0.5f);
					bi.t.position += new Vector3(correction.x, 0f, correction.y);
					bj.t.position -= new Vector3(correction.x, 0f, correction.y);

					// compute relative normal velocity
					Vector2 vi = new Vector2(bi.v.x, bi.v.z);
					Vector2 vj = new Vector2(bj.v.x, bj.v.z);
					float vnRel = Vector2.Dot(vi - vj, n);
					if (vnRel < 0f)
					{
						// impulse for equal masses
						float jImpulse = -(1f + restitution) * vnRel * 0.5f;
						Vector2 impulse = jImpulse * n;
						vi += impulse;
						vj -= impulse;
						bi.v = new Vector3(vi.x, 0f, vi.y);
						bj.v = new Vector3(vj.x, 0f, vj.y);
					}

					// apply speed cap
					if (bi.v.magnitude > maxBallSpeed) bi.v = bi.v.normalized * maxBallSpeed;
					if (bj.v.magnitude > maxBallSpeed) bj.v = bj.v.normalized * maxBallSpeed;

					// write back
					balls[i] = bi;
					balls[j] = bj;
				}
			}
		}
	}
}

