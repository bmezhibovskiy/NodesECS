using com.borismez.ShockwavesHDRP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

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

    private class Explosion
    {
        public Camera camera;
        public float maxTime;
        public float size;
        public Vector3 pos;
        public GameObject lightObject;
        public GameObject explosionObject;
        public float startTime;
        public int phase;
        public Explosion(Camera camera, float maxTime, float size, Vector3 pos)
        {
            this.camera = camera;
            this.maxTime = maxTime;
            this.size = size;
            this.pos = pos;
            this.lightObject = null;
            this.explosionObject = null;
            this.startTime = Time.time;
            this.phase = 0;
        }
    }


    private List<Explosion> explosions = new List<Explosion>();

    public void AddExplosion(Vector3 worldPos, Camera camera, float size, float maxTime)
    {
        explosions.Add(new Explosion(camera, maxTime, size, worldPos));
    }

    private GameObject AddExplosionObject(Vector3 worldPos, float size)
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

        GameObject newExplosion = Instantiate(ExplosionPrefab, worldPos, Quaternion.identity);
        newExplosion.transform.localScale = Vector3.one * scale;
        return newExplosion;
    }

    private GameObject AddLightObject(Vector3 worldPos, float size)
    {
        GameObject newLight = new GameObject("ExplosionLightObject");
        newLight.transform.position = worldPos;

        Light light = newLight.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = size * 5000.0f;
        light.colorTemperature = 2700.0f;
        light.useColorTemperature = true;

        return newLight;
    }

    private void AddExplosionShockwave(Vector3 worldPos, Camera camera, float size, float maxTime)
    {
        float speed = 1.1f * size;
        float gauge = 0.2f / size; //Bigger explosion is thicker
        float intensity = 3.0f * size; //Bigger explosion is more intense
        float decaySpeed = 0.8f / size; //Bigger explosion decays slower

        shockwaveManager.AddShockwave(worldPos, camera, speed, maxTime, gauge, intensity, decaySpeed);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float curTime = Time.time;

        foreach(Explosion explosion in explosions)
        {
            if(curTime > explosion.maxTime)
            {
                Destroy(explosion.lightObject);
                Destroy(explosion.explosionObject);
                continue;
            }

            switch (explosion.phase)
            {
                case 0:
                    explosion.lightObject = AddLightObject(explosion.pos, explosion.size);
                    explosion.phase = 1;
                    break;
                case 1:
                    if(curTime > explosion.startTime + 0.1f)
                    {
                        AddExplosionShockwave(explosion.pos, explosion.camera, explosion.size, explosion.maxTime);
                        explosion.phase = 2;
                    }
                    break;
                case 2:
                    if (curTime > explosion.startTime + 0.24f)
                    {
                        explosion.explosionObject = AddExplosionObject(explosion.pos, explosion.size);
                        explosion.phase = 3;
                    }
                    break;
                default:
                    break;
            }
            explosion.lightObject.GetComponent<Light>().intensity *= 0.8f;
        }
        explosions.RemoveAll(explosion => curTime > explosion.maxTime);
            
    }
}
