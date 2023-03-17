using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;

namespace com.borismez.ShockwavesHDRP
{
    using Vector4ListParameter = VolumeParameter<List<Vector4>>;

    [Serializable, VolumeComponentMenu("Post-processing/Custom/Shockwave")]
    public sealed class Shockwave : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public static readonly int MAX_SHOCKWAVES = 32;

        [Tooltip("List of shockwave geometries. In normalized screen space, xy is the center, w is the radius")]
        public Vector4ListParameter shockwaveGeometry = new Vector4ListParameter();

        [Tooltip("Parameters for each shockwave. x is gauge (thinness), y is intensity, z is decay speed")]
        public Vector4ListParameter shockwaveParams = new Vector4ListParameter();

        [Tooltip("The number of shockwaves present")]
        public IntParameter numShockwaves = new IntParameter(0);

        Material m_Material;

        public bool IsActive() => m_Material != null && numShockwaves.value > 0 && shockwaveGeometry.value.Count == MAX_SHOCKWAVES && shockwaveParams.value.Count == MAX_SHOCKWAVES;

        // Do not forget to add this post process in the Custom Post Process Orders list (Project Settings > Graphics > HDRP Global Settings).
        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        const string kShaderName = "Hidden/Shader/Shockwave";

        public override void Setup()
        {
            if (Shader.Find(kShaderName) != null)
            {
                m_Material = new Material(Shader.Find(kShaderName));
            }
            else
                Debug.LogError($"Unable to find shader '{kShaderName}'. Post Process Volume Shockwave is unable to load.");
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null)
                return;

            m_Material.SetInt("_ShockwavesCount", Math.Min(MAX_SHOCKWAVES, numShockwaves.value));
            m_Material.SetVectorArray("_ShockwavesGeometry", shockwaveGeometry.value);
            m_Material.SetVectorArray("_ShockwavesParams", shockwaveParams.value);
            m_Material.SetFloat("_AspectRatio", ((float)Screen.width) / ((float)Screen.height));
            m_Material.SetTexture("_MainTex", source);
            HDUtils.DrawFullScreen(cmd, m_Material, destination, shaderPassId: 0);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(m_Material);
        }
    }
}