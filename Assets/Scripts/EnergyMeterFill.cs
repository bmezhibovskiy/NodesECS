using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnergyMeterFill : MonoBehaviour
{

    void Start()
    {
        
    }

    void Update()
    {
        //TODO: Actual logic from player's energy
        Image image = GetComponent<Image>();
        image.fillAmount = (Time.time % 10) / 10.0f;
    }
}
