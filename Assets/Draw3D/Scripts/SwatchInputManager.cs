using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
//using metaio;

/// Implements a concept of a brush tip - different kinds of painterly modes are set here and draw different kinds of art

public class SwatchInputManager : MonoBehaviour {

	public Transform target;				// the moving feature which represents the origin of the scene
	public Transform markerBased;
	public Transform markerlessBased;
	public Transform brush;					// this brush "chases" the target
	public Transform brushShadowGround;
	public Transform choice1;
	public Transform choice2;
	public Light sunlight;
	
	// Shared state for drawable things - a piece of drawing material for reference
	public GameObject prefabSwatch;
	public Color color = Color.green;
	public Material material;
	public float DrawSizeDefault = 8.0f;
	public bool isCameraMoving = false;

	// current focus of drawing
	GameObject focus;
	bool ignoreFingerPosition = false;
	Swatch3d.STYLE style = Swatch3d.STYLE.RIBBON;
	Vector3 targetPosition;
	Quaternion targetOrientation;

	GameObject lastObject;
	Vector3 inputPosition;
	int downmode = 0;
		
	// ----------------------------------------------------------------------------
	
	void SetMaterial(Material m) {
		material = Object.Instantiate(m) as Material;
		material.color = color;		
		brush.GetComponent<Renderer>().material = material;
	}

	void SetColor(Color c) {
		color = c;
		material.color = color;
		brush.GetComponent<Renderer>().material = material;
	}

	float bounce = 0;
	Transform t;
	void HandleBounce(Transform _t) {
		t = _t;
		bounce = 10;
	}

	void HandleUpdate() {
		if(bounce < 1) return;
		bounce--;
		float size = 0.8f + (10.0f-bounce)/50.0f;
		t.localScale = new Vector3(size,size,size);
	}
	
	void SetChoice(GameObject g) {
		choice1.transform.position = g.transform.position;
	}
	
	void SetChoice2(GameObject g) {
		choice2.transform.position = g.transform.position;
	}
	
	bool HandleButtons(Vector3 input) {
		// look for button events
		Ray ray = Camera.main.ScreenPointToRay(input);
		RaycastHit hit;
		if( Physics.Raycast (ray,out hit) ) {
			HandleBounce(hit.transform);
			switch(hit.transform.name) {
				case "Palette1": SetColor(hit.transform.gameObject.GetComponent<Renderer>().material.color); SetChoice(hit.transform.gameObject); return true;
				case "Palette2": SetColor(hit.transform.gameObject.GetComponent<Renderer>().material.color); SetChoice(hit.transform.gameObject); return true;
				case "Palette3": SetColor(hit.transform.gameObject.GetComponent<Renderer>().material.color); SetChoice(hit.transform.gameObject); return true;
				case "Palette4": SetColor(hit.transform.gameObject.GetComponent<Renderer>().material.color); SetChoice(hit.transform.gameObject); return true;
				case "Palette5": SetColor(hit.transform.gameObject.GetComponent<Renderer>().material.color); SetChoice(hit.transform.gameObject); return true;
				case "Palette6": SetColor(hit.transform.gameObject.GetComponent<Renderer>().material.color); SetChoice(hit.transform.gameObject); return true;
				case "Swatch1":  SetMaterial(hit.transform.gameObject.GetComponent<Renderer>().material); return true;
				case "Swatch2":  SetMaterial(hit.transform.gameObject.GetComponent<Renderer>().material); return true;
				case "Track": 	 Track(); return true;
				case "SaveExit": SaveAndExit(); return true;
				case "Sun": 	 SetSunPosition(); SetChoice2(hit.transform.gameObject); return true;
				case "Undo":     Undo(); return true;
				case "Tube":     style = Swatch3d.STYLE.TUBE; SetChoice2(hit.transform.gameObject); return true;
				case "Ribbon":   style = Swatch3d.STYLE.RIBBON; SetChoice2(hit.transform.gameObject); return true;
				case "Swatch":   style = Swatch3d.STYLE.SWATCH; SetChoice2(hit.transform.gameObject); return true;
				default: break;
			}
		}
		return false;
	}

	void DrawingBegin() {

/*
		// Support a special mode for painting in 3d rather than with screen as an aid
		if( (input.x > Screen.width - 100) && (input.y < 100)) {
			ignoreFingerPosition = true;
		} else {
			ignoreFingerPosition = false;
		}
*/
		// start new art swatch
		focus = Instantiate(prefabSwatch) as GameObject;
		focus.transform.parent = gameObject.transform;
		Swatch3d art = focus.GetComponent<Swatch3d>() as Swatch3d;
		art.setup(color,style,material);
		lastObject = null;
	}


	void DrawingContinue() {
		if(focus==null) return;

		// Use these facts about the brush in 3d to help orient paint swatches
		Vector3 xyz = brush.transform.position;
		Vector3 right = brush.transform.right;
		Vector3 forward = brush.transform.forward;

/*		// A mode where the brush 3d position is ignored
		if(ignoreFingerPosition == true) {
			input.z = 400; 
			xyz = Camera.main.ScreenToWorldPoint(input);
			xyz = world.transform.InverseTransformPoint(xyz);
		}
*/
		Swatch3d art = focus.GetComponent<Swatch3d>() as Swatch3d;
		art.paintConsider(xyz,right,forward,DrawSizeDefault);

		//AddDots();
	}

	void DrawingEnd() {
		if(focus==null) return;
		Swatch3d art = focus.GetComponent<Swatch3d>() as Swatch3d;
		focus = null;
		if(art.paintFinish() == false) {
			Destroy(art);
		}
	}

	void AddDots() {
		float distance = 50;
		if(lastObject != null) {
			Vector3 p = brush.transform.position-lastObject.transform.position;
			distance = p.magnitude;
		}
		if(distance > 20) {
	        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
	        sphere.transform.parent = brush.parent;
    	    sphere.transform.position = brush.transform.position;
    	    sphere.transform.localScale = new Vector3(10,10,10);
    	    lastObject = sphere;
		}
	}

	// ----------------------------------------------------------------------------
	
	// shake detection
	const float accelerometerUpdateInterval = 1.0f / 60.0f;
	// The greater the value of LowPassKernelWidthInSeconds, the slower the filtered value will converge towards current input sample (and vice versa).
	const float lowPassKernelWidthInSeconds = 1.0f;
	// This next parameter is initialized to 2.0 per Apple's recommendation, or at least according to Brady! ;)
	float shakeDetectionThreshold = 2.0f;
	
	private float lowPassFilterFactor = accelerometerUpdateInterval / lowPassKernelWidthInSeconds;
	private Vector3 lowPassValue = Vector3.zero;
	private Vector3 acceleration = Vector3.zero;
	private Vector3 deltaAcceleration = Vector3.zero;
	int shakelatch = 0;

	void ShakeStart() {
		shakeDetectionThreshold *= shakeDetectionThreshold;
		lowPassValue = Input.acceleration;
	}

	void ShakeUpdate() {
		acceleration = Input.acceleration;
		lowPassValue = Vector3.Lerp(lowPassValue, acceleration, lowPassFilterFactor);
		deltaAcceleration = acceleration - lowPassValue;
		if(shakelatch>0) { shakelatch--; return; }
		if (deltaAcceleration.sqrMagnitude < shakeDetectionThreshold) return;
		if( transform.childCount < 1) return;
		Undo();
		shakelatch = 30;
	}

	void Undo() {
		if( transform.childCount < 1) return;
		Destroy(transform.GetChild ( transform.childCount - 1 ).gameObject );
	}

	// ----------------------------------------------------------------------------

	void Start() {
		material = Object.Instantiate(material) as Material;
		Screen.orientation = ScreenOrientation.LandscapeLeft;
		ShakeStart();

		//test();	
	}

	void test() {
	
		focus = Instantiate(prefabSwatch) as GameObject;
		focus.transform.parent = this.transform;
		
		Swatch3d art = focus.GetComponent<Swatch3d>() as Swatch3d;
		art.setup(color,Swatch3d.STYLE.TUBE,material);
		
		Vector3 xyz = brush.transform.position;
		Vector3 right = brush.transform.right;
		Vector3 forward = brush.transform.forward;

		art.test (right,forward,DrawSizeDefault);
		
		focus = null;
	
	}

	public static Quaternion RotationFromMatrix(Matrix4x4 matrix) {
        var qw = Mathf.Sqrt(1f + matrix.m00 + matrix.m11 + matrix.m22) / 2;
        var w = 4 * qw;
        var qx = (matrix.m21 - matrix.m12) / w;
        var qy = (matrix.m02 - matrix.m20) / w;
        var qz = (matrix.m10 - matrix.m01) / w;
        return new Quaternion(qx, qy, qz, qw);
    }
 
    public static Vector3 PositionFromMatrix(Matrix4x4 matrix) {
        var x = matrix.m03;
        var y = matrix.m13;
        var z = matrix.m23;
        return new Vector3(x, y, z);
    }

	public static void Apply1(Matrix4x4 matrix, Transform transform) {
		transform.localScale = new Vector3(1,1,1);
        transform.rotation = RotationFromMatrix(matrix);
        transform.position = PositionFromMatrix(matrix);
    }

	public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
	{
		// Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
		Quaternion q = new Quaternion();
        q.w =  Mathf.Sqrt(Mathf.Max(0,1+ m[0,0]+ m[1,1]+ m[2,2]))/2;
        q.x =  Mathf.Sqrt(Mathf.Max(0,1+ m[0,0]- m[1,1]- m[2,2]))/2;
        q.y =  Mathf.Sqrt(Mathf.Max(0,1- m[0,0]+ m[1,1]- m[2,2]))/2;
        q.z =  Mathf.Sqrt(Mathf.Max(0,1- m[0,0]- m[1,1]+ m[2,2]))/2;
        q.x *= Mathf.Sign(q.x *(m[2,1]- m[1,2]));
        q.y *= Mathf.Sign(q.y *(m[0,2]- m[2,0]));
        q.z *= Mathf.Sign(q.z *(m[1,0]- m[0,1]));
        return q;
	}
 
	void Apply2(Matrix4x4 matrix, Transform transform) {
		transform.localScale = new Vector3(1,1,1);
	    transform.rotation = QuaternionFromMatrix(matrix);
	    transform.position = PositionFromMatrix(matrix);
	}

	void Update () {

		// update
		ShakeUpdate();
		HandleUpdate();

		if(true) {
			// Rather than moving the 3d models I want to move the camera, so apply the inverse of the target to camera

			Apply2(target.transform.worldToLocalMatrix,Camera.main.transform);

			// Calculate where the brush is in 3d as well - it is forward of the camera a bit - and same orientation

			Vector3 xyz = new Vector3(0,0,400);
			xyz = Camera.main.transform.TransformPoint(xyz);
			Quaternion rot = Camera.main.transform.rotation;

			brush.transform.position = Vector3.Lerp(brush.transform.position, xyz, Time.deltaTime * 10.0f );
			brush.transform.rotation = Quaternion.Slerp(brush.transform.rotation, rot, Time.deltaTime * 10.0f );
			// shadow
			//brushShadowGround.transform.position = new Vector3(brush.transform.position.x,0,brush.transform.position.z);

		}

		// deal with platform specific mouse down and move and up events
		RuntimePlatform platform = Application.platform;
		if(platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer){
			if(Input.touchCount > 0) {
				inputPosition = Input.GetTouch (0).position;
				if(downmode == 0 && Input.GetTouch(0).phase == TouchPhase.Began) {
					if(HandleButtons(inputPosition)) {
						return;
					}
					downmode = 1;
				}
				else if(downmode == 2 && Input.GetTouch(0).phase == TouchPhase.Ended) {
					downmode = 3;
				}
			}
		} else if(platform == RuntimePlatform.OSXEditor || platform == RuntimePlatform.OSXPlayer){
			inputPosition = Input.mousePosition;
			if(downmode == 0 && Input.GetMouseButtonDown(0)) {
				if(HandleButtons(inputPosition)) {
					return;
				}
				downmode = 1;
			}
			else if(downmode == 2 && Input.GetMouseButtonUp(0)) {
				downmode = 3;
			}
		}

		// Handle current drawing mode
		if(downmode == 1) {
			// Show the brush only when actually painting
			// brush.gameObject.SetActive (true);
			DrawingBegin();
			DrawingContinue();
			downmode = 2;
		}
		else if(downmode == 2) {
			DrawingContinue();
		}
		else if(downmode == 3) {
			DrawingEnd();
			//brush.gameObject.SetActive(false);
			downmode = 0;
		}
	
	}
	
	void Track() {
		//MetaioSDKUnity.startInstantTracking("INSTANT_3D", "");
	}

	void SetSunPosition() {
		// do it
		sunlight.transform.rotation = Camera.main.transform.rotation;
	}
	
	void SaveAndExit() {
	}

}

