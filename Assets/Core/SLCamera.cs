using UnityEngine;
using System;
using System.Collections;

public class SLCamera : MonoBehaviour {

    [NonSerialized]
    public Transform target;
    [NonSerialized]
    public Vector3 focalPoint;
    
    float smoothFactor = 8f; 
    bool dragging = false;
    int dragX, dragY;
	GameObject targetEmpty;
	
	void Start () {
		focalPoint = transform.position + transform.rotation * (Vector3.forward * 5);
		targetEmpty = (GameObject)new GameObject("camera target");
		target = targetEmpty.transform;
		target.position = transform.position;
		target.rotation = transform.rotation;
	}

	void Update () {
		if (Input.GetMouseButtonDown(0)) // Left mouse button pressed
		{
            dragging = true;
            dragX = (int)Input.mousePosition.x;
            dragY = (int)Input.mousePosition.y;
			
			if (Input.GetKey(KeyCode.LeftAlt))
			{
				// Starting to alt-zoom
				var ray = camera.ScreenPointToRay(Input.mousePosition);
				RaycastHit hit;
				if (Physics.Raycast(ray, out hit, Mathf.Infinity))
				{
					focalPoint = hit.point;
					target.LookAt(focalPoint);
				}
			}
		}
		else if (Input.GetMouseButtonUp(0))
		{
			dragging = false;
		}
		
		if (Input.GetMouseButtonDown(2)) // Middle mouse button pressed
		{
            dragging = true;
            dragX = (int)Input.mousePosition.x;
            dragY = (int)Input.mousePosition.y;
		}
		else if (Input.GetMouseButtonUp(2))
		{
			dragging = false;
		}
		
		if (dragging)
		{
            int deltaX = (int)Input.mousePosition.x - dragX;
            int deltaY = (int)Input.mousePosition.y - dragY;
			if (Input.GetMouseButton(0))
			{
				// Orbit
				if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt))
				{
					var direction = focalPoint - target.position;
					var axis = Vector3.Cross(Vector3.up, direction);
					target.LookAt(focalPoint);
					target.RotateAround(focalPoint, axis, -deltaY * smoothFactor * Time.deltaTime * 4f);
				}
				// Pan
				else if (Input.GetKey(KeyCode.LeftControl))
				{
					target.Translate(Vector3.down * deltaY * Time.deltaTime * smoothFactor / 4);
					target.Translate(Vector3.left * deltaX * Time.deltaTime * smoothFactor / 4);
				}
				// Alt-zoom (up down move camera closer to target, left right rotate around target)
	            else if (Input.GetKey(KeyCode.LeftAlt))
	            {
					target.Translate(Vector3.forward *  deltaY * smoothFactor * Time.deltaTime * 12);
					target.RotateAround(focalPoint, Vector3.up, deltaX * smoothFactor * Time.deltaTime * 4);
	            }
			}
			else if (Input.GetMouseButton(2))
			{
				// Pan
				target.Translate(Vector3.down * deltaY * Time.deltaTime * smoothFactor / 4);
				target.Translate(Vector3.left * deltaX * Time.deltaTime * smoothFactor / 4);
			}


            dragX = (int)Input.mousePosition.x;
            dragY = (int)Input.mousePosition.y;
		}
		
		//SmoothPos();
	}
	
	void LateUpdate()
	{
		SmoothPos();
	}
	
	void SmoothPos()
    {
		transform.position = Vector3.Lerp(transform.position, target.position, smoothFactor * Time.deltaTime);
		transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, smoothFactor * Time.deltaTime);
    }
}
