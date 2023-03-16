using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;

[Serializable]
public class Blah: VolumeParameter<List<Vector4>>
{

}

[Serializable, VolumeComponentMenu("Post-processing/Custom/Shockwave")]
public sealed class Shockwave : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    public static readonly int MAX_SHOCKWAVES = 32;

    [Tooltip("The list of shockwaves, in normalized screen space. xy is the center, w is the radius")]
    public VolumeParameter<List<Vector4>> shockwaveData = new VolumeParameter<List<Vector4>>();

    [Tooltip("The number of shockwaves present")]
    public IntParameter numShockwaves = new IntParameter(0);

    Material m_Material;

    public bool IsActive() => m_Material != null && numShockwaves.value > 0 && numShockwaves.value <= MAX_SHOCKWAVES;

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

        m_Material.SetInt("_PositionsCount", numShockwaves.value);
        m_Material.SetVectorArray("_PositionsTimes", shockwaveData.value);
        m_Material.SetFloat("_AspectRatio", ((float)Screen.width)/((float)Screen.height));
        m_Material.SetTexture("_MainTex", source);
        HDUtils.DrawFullScreen(cmd, m_Material, destination, shaderPassId: 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}
