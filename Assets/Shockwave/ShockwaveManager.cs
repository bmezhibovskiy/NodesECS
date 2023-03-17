using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.borismez.ShockwavesHDRP
{

    public class ShockwaveManager : MonoBehaviour
    {
        [SerializeField]
        bool DebugMouseClickShockwaves = false;

        private struct ShockwaveMetadata { public float speed; public float maxTime; }

        private List<Vector4> shockwaveGeometry = new List<Vector4>();
        private List<Vector4> shockwaveParams = new List<Vector4>();
        private List<ShockwaveMetadata> shockwaveMetadata = new List<ShockwaveMetadata>();
        private int numShockwaves = 0;

        private Shockwave shockWave = null;

        public bool AddShockwave(Vector3 worldPos, Camera camera, float speed, float maxTime, float gauge = 1f, float intensity = 1f, float decaySpeed = 1f)
        {
            if (numShockwaves >= Shockwave.MAX_SHOCKWAVES) { return false; }

            Vector3 screenPos = camera.WorldToScreenPoint(worldPos);

            shockwaveGeometry[numShockwaves] = new Vector4(screenPos.x / Screen.width, screenPos.y / Screen.height, 0, 0);
            shockwaveParams[numShockwaves] = new Vector4(gauge, intensity, decaySpeed, 0);
            shockwaveMetadata[numShockwaves] = new ShockwaveMetadata { speed = speed, maxTime = maxTime };
            ++numShockwaves;

            return true;
        }

        void Start()
        {
            Shockwave tempShockwave;
            if (GetComponent<Volume>().profile.TryGet(out tempShockwave))
            {
                shockWave = tempShockwave;
                for (int i = 0; i < Shockwave.MAX_SHOCKWAVES; ++i)
                {
                    shockwaveGeometry.Add(Vector4.zero);
                    shockwaveParams.Add(Vector4.one);
                    shockwaveMetadata.Add(new ShockwaveMetadata { speed = 0, maxTime = 0 });
                }
            }
        }

        void Update()
        {
            if (DebugMouseClickShockwaves && Input.GetMouseButtonDown(0))
            {
                AddDebugShockwave();
            }

            UpdateShockwaveData();

            OverrideShaderData();
        }

        private void UpdateShockwaveData()
        {
            for (int i = 0; i < numShockwaves; ++i)
            {
                if (shockwaveMetadata[i].maxTime < Time.time)
                {
                    //Order doesn't matter, so we can delete a shockwave by moving the last one to its index, and decrementing count
                    --numShockwaves;
                    shockwaveGeometry[i] = shockwaveGeometry[numShockwaves];
                    shockwaveParams[i] = shockwaveParams[numShockwaves];
                    shockwaveMetadata[i] = shockwaveMetadata[numShockwaves];
                }

                //This could have been modified in the if statement above, but still needs to run.
                shockwaveGeometry[i] = new Vector4(shockwaveGeometry[i].x, shockwaveGeometry[i].y, 0, shockwaveGeometry[i].w + shockwaveMetadata[i].speed * Time.deltaTime);
            }
        }

        private void OverrideShaderData()
        {
            if (shockWave == null) { return; }
            shockWave.numShockwaves.Override(numShockwaves);
            shockWave.shockwaveGeometry.Override(shockwaveGeometry);
            shockWave.shockwaveParams.Override(shockwaveParams);
        }

        private void AddDebugShockwave()
        {
            Vector4 posTime = new Vector4(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0, 0);
            shockwaveGeometry[numShockwaves] = posTime;
            shockwaveParams[numShockwaves] = new Vector4(0.75f, 1.5f, 0.5f, 0);
            shockwaveMetadata[numShockwaves] = new ShockwaveMetadata { maxTime = Time.time + 0.5f, speed = 0.5f };
            ++numShockwaves;
        }
    }
}