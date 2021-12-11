using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Google.XR.Cardboard;

public class CameraController : MonoBehaviour
{
    public GameObject reticle;
    public Camera camera;

    public Vector3 previousDirection { get; private set; }
    public Vector3 currentDirection { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        reticle.transform.position = transform.position + camera.transform.rotation * (Vector3.forward * 2);

        previousDirection = camera.transform.rotation * Vector3.forward;
        currentDirection = camera.transform.rotation * Vector3.forward;
    }

    // Update is called once per frame
    void Update()
    {
        previousDirection = currentDirection;
        currentDirection = camera.transform.rotation * Vector3.forward;

        reticle.transform.position = transform.position + camera.transform.rotation * (Vector3.forward * 2);

        //if (Input.GetMouseButtonUp(0))
        if (Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Ray ray = new Ray(transform.position, camera.transform.rotation * Vector3.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                hit.transform.gameObject.SendMessage("Interact");
            }
        }

        
        /*
        if (!Input.GetMouseButton(1))
        {
            float speed = 600f;
            Vector3 rot = new Vector3(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"), 0) * Time.deltaTime * speed;

            Vector3 e_angles = transform.rotation.eulerAngles + rot;
            transform.rotation = Quaternion.Euler(e_angles.x, e_angles.y, e_angles.z);
        }
        */
        
        

        /*
        if (Input.GetMouseButtonUp(0))
        {
            Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                hit.transform.gameObject.SendMessage("Interact");
            }
        }
        */

        /*
        if (Input.GetKeyDown("r"))
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        */
    }
}
