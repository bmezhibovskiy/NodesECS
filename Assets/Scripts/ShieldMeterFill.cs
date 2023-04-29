using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShieldMeterFill : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //TODO: Actual logic from player's shield
        Image image = GetComponent<Image>();
        image.fillAmount = (Time.time % 10) / 10.0f;
    }
}
