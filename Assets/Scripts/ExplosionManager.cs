using com.borismez.ShockwavesHDRP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionManager : MonoBehaviour
{
    [SerializeField]
    GameObject ExplosionPrefabSmall;

    [SerializeField]
    GameObject ExplosionPrefabMedium;

    [SerializeField]
    GameObject ExplosionPrefabLarge;

    [SerializeField]
    ShockwaveManager shockwaveManager;

    private Dictionary<float, GameObject> explosions = new Dictionary<float, GameObject>();

    public GameObject AddExplosion(Vector3 worldPos, Camera camera, float size)
    {
        float scale = size + 0.5f;
        GameObject ExplosionPrefab = ExplosionPrefabSmall;

        if (size > 1)
        {
            ExplosionPrefab = ExplosionPrefabMedium;
            scale = size - 0.5f;
        }
        if (size > 2)
        { 
            ExplosionPrefab = ExplosionPrefabLarge;
            scale = size - 1.5f;
        }


        float speed = 0.8f * size;
        float maxTime = Time.time + 0.6f * size; //Bigger explosion lasts longer.
        float gauge = 0.3f / size; //Bigger explosion is thicker
        float intensity = 2.0f * size; //Bigger explosion is more intense
        float decaySpeed = 1.0f / size; //Bigger explosion decays slower

        shockwaveManager.AddShockwave(worldPos, camera, speed, maxTime, gauge, intensity, decaySpeed);
        GameObject newExplosion = Instantiate(ExplosionPrefab, worldPos, Quaternion.identity);
        newExplosion.transform.localScale = Vector3.one * scale;
        explosions[maxTime] = newExplosion;
        return newExplosion;
        //return null;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float curTime = Time.time;
        List<float> toRemove = new List<float>();
        foreach (KeyValuePair<float, GameObject> pair in explosions)
        {
            if (pair.Key < curTime)
            {
                toRemove.Add(pair.Key);
                Destroy(pair.Value);
            }
        }
        foreach (float key in toRemove)
        {
            explosions.Remove(key);
        }
            
    }
}
