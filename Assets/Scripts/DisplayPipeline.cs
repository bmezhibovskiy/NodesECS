using System.Collections;
using System.Collections.Generic;
using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class PartRenderInfo
{
    public Mesh mesh;
    public Material material;
    public Transform transform;
    public PartRenderInfo(Mesh mesh, Material material, Transform transform)
    {
        this.mesh = mesh;
        this.material = material;
        this.transform = transform;
    }
}
public class PartsRenderInfo
{
    public static PartsRenderInfo CreatePartsRenderInfo(DisplayInfo displayInfo)
    {
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        foreach (PartDisplayInfo partInfo in displayInfo.parts)
        {
            string materialPath = displayInfo.path + "/" + partInfo.material;
            materials[partInfo.mesh] = Resources.Load<Material>(materialPath);
        }
        PartsRenderInfo pri = new PartsRenderInfo();
        string meshBundlePath = displayInfo.path + "/" + displayInfo.meshBundle;
        GameObject containerObject = Resources.Load(meshBundlePath) as GameObject;
        for (int i = 0; i < containerObject.transform.childCount; ++i)
        {
            Transform childTransform = containerObject.transform.GetChild(i);
            GameObject child = childTransform.gameObject;
            Mesh mesh = child.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            Material mat = materials[child.name];
            Assert.IsNotNull(mat);
            pri.AddPart(child.name, new PartRenderInfo(mesh, mat, childTransform));

        }
        return pri;
    }

    public Dictionary<string, PartRenderInfo> parts = new Dictionary<string, PartRenderInfo>();
    public void AddPart(string name, PartRenderInfo info)
    {
        parts[name] = info;
    }

    public void AddRenderComponents(EntityManager em, Entity parent, float3 anchor, float3 rotation, float scale)
    {
        foreach (KeyValuePair<string, PartRenderInfo> pair in parts)
        {
            Entity child = em.CreateEntity();
            em.AddComponentData(child, new Parent { Value = parent });
            float4x4 anchorTransform = float4x4.Translate(anchor);
            float4x4 rotationTransform = math.mul(float4x4.RotateX(math.radians(rotation.x)), math.mul(float4x4.RotateY(math.radians(rotation.y)), float4x4.RotateZ(math.radians(rotation.z))));
            float4x4 scaleTransform = float4x4.Scale(scale);
            float4x4 transform = math.mul(anchorTransform, math.mul(rotationTransform, math.mul(scaleTransform, pair.Value.transform.localToWorldMatrix)));
            em.AddComponentData(child, new LocalToWorld { Value = transform });
            em.AddComponentData(child, new RelativeTransform { Value = transform, lastParentValue = em.GetComponentData<LocalToWorld>(parent).Value });
            em.AddComponentData(child, new DestroyOnLevelUnload());

            RenderMeshDescription rmd = new RenderMeshDescription(ShadowCastingMode.On, true);
            RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { pair.Value.material }, new Mesh[] { pair.Value.mesh });
            RenderMeshUtility.AddComponents(child, em, rmd, renderMeshArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }
    }
}