using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Background : MonoBehaviour
{
    [SerializeField]
    Camera mainCamera;

    [SerializeField, Range(0, 1)]
    float cameraPosMultiplier;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 scaledCameraPos = mainCamera.transform.position * cameraPosMultiplier;
        transform.position = new Vector3(scaledCameraPos.x, scaledCameraPos.y, transform.position.z);
    }
}
