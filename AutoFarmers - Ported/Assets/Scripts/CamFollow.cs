using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CamFollow : MonoBehaviour {

	public Vector2 viewAngles;
	public float viewDist;
	public float mouseSensitivity;

	void Start () {
		transform.rotation = Quaternion.Euler(viewAngles.y,viewAngles.x,0f);
	}
	
	void LateUpdate () {
        // view angles: 45, 30
        // view dist 10
        // mouse sens 4000
        EntityManager entityManager = World.All[0].EntityManager;
        Translation trans = entityManager.GetComponentData<Translation>(GridDataInitialization.firstFarmer);
        Vector3 pos = new Vector3(trans.Value.x, trans.Value.y+2, trans.Value.z);
		viewAngles.x += Input.GetAxis("Mouse X") * mouseSensitivity/Screen.height;
		viewAngles.y -= Input.GetAxis("Mouse Y") * mouseSensitivity/Screen.height;
		viewAngles.y = Mathf.Clamp(viewAngles.y,7f,80f);
		viewAngles.x -= Mathf.Floor(viewAngles.x / 360f) * 360f;
		transform.rotation = Quaternion.Euler(viewAngles.y,viewAngles.x,0f);
		transform.position = pos - transform.forward * viewDist;
	}
}
