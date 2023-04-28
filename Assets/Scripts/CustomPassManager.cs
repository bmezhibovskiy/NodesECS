using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class CustomPassManager : MonoBehaviour
{
    FullScreenCustomPass fullScreenCustomPass;
    // Start is called before the first frame update
    void Start()
    {
        CustomPassVolume cpv = GetComponent<CustomPassVolume>(); 
        foreach(CustomPass cp in cpv.customPasses)
        {
            if(cp is FullScreenCustomPass fscp)
            {
                fullScreenCustomPass = fscp;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        //if (Input.GetMouseButtonDown(0))
        //{
            Vector3 mousePos = Input.mousePosition;
            Vector4 normalizedMousePos = new Vector4(mousePos.x / Screen.width, mousePos.y / Screen.height, 1, 1);
            fullScreenCustomPass.fullscreenPassMaterial.SetVector("_NormalizedMousePos", normalizedMousePos);
        //}
    }
}
