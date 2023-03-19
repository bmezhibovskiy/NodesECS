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
        GameObject ExplosionPrefab = ExplosionPrefabSmall;
        if(size > 1) { ExplosionPrefab = ExplosionPrefabMedium; }
        if (size > 2) { ExplosionPrefab = ExplosionPrefabLarge; }

        float maxTime = Time.time + size * 2.0f;
        shockwaveManager.AddShockwave(worldPos, camera, 1.0f, maxTime, 0.5f, 1.5f, 0.2f);
        GameObject newExplosion = Instantiate(ExplosionPrefab, worldPos, Quaternion.identity);
        explosions[maxTime] = newExplosion;
        return newExplosion;
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
