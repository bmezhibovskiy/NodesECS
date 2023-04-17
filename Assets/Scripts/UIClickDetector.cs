using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIClickDetector : MonoBehaviour
{
    GraphicRaycaster raycaster;

    void Start()
    {
        raycaster = GetComponent<GraphicRaycaster>();
    }

    void Update()
    {
        Globals.sharedInputState.Data.MinimapClicked = false;
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            List<RaycastResult> results = new List<RaycastResult>();

            pointerData.position = Input.mousePosition;
            raycaster.Raycast(pointerData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject.name == "Minimap")
                {
                    Globals.sharedInputState.Data.MinimapClicked = true;
                }
            }
        }
    }
}