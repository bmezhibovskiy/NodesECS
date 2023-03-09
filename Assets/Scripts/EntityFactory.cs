using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public struct EntityFactory
{
    public enum EntityType
    {
        Node, Rocket1, Thrust1
    }

    private struct Prototypes
    {
        public Entity nodePrototype;
        public Entity rocket1Prototype;
        public Entity thrust1Prototype;
    }
    private Prototypes prototypes;

    public void SetUpPrototypes(EntityManager em, Dictionary<EntityType, Mesh> meshes, Dictionary<EntityType, Material> materials)
    {
        EntityArchetype ea = em.CreateArchetype();

        RenderMeshDescription rmd = new RenderMeshDescription(ShadowCastingMode.Off, false);
        MaterialMeshInfo mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

        prototypes.nodePrototype = em.CreateEntity(ea);
        RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { materials[EntityType.Node] }, new Mesh[] { meshes[EntityType.Node] });
        RenderMeshUtility.AddComponents(prototypes.nodePrototype, em, rmd, renderMeshArray, mmi);


        prototypes.rocket1Prototype = em.CreateEntity(ea);
        RenderMeshArray renderMeshArray2 = new RenderMeshArray(new Material[] { materials[EntityType.Rocket1] }, new Mesh[] { meshes[EntityType.Rocket1] });
        RenderMeshUtility.AddComponents(prototypes.rocket1Prototype, em, rmd, renderMeshArray2, mmi);


        prototypes.thrust1Prototype = em.CreateEntity(ea);
        RenderMeshArray renderMeshArray3 = new RenderMeshArray(new Material[] { materials[EntityType.Thrust1] }, new Mesh[] { meshes[EntityType.Thrust1] });
        RenderMeshUtility.AddComponents(prototypes.thrust1Prototype, em, rmd, renderMeshArray3, mmi);
    }

    public Entity CreateRocket1Async(int sortKey, EntityCommandBuffer.ParallelWriter ecb, float3 pos, float3 facing, double elapsedTime)
    {
        Entity newRocket = ecb.Instantiate(sortKey, prototypes.rocket1Prototype);
        float4x4 scale = float4x4.Scale(0.1f);
        float4x4 rotation = float4x4.RotateZ(math.radians(270));
        float4x4 initialTransform = math.mul(rotation, scale);
        ecb.AddComponent(sortKey, newRocket, new LocalToWorld { Value = math.mul(float4x4.Translate(pos), initialTransform) });
        ecb.AddComponent(sortKey, newRocket, new Accelerating { prevPos = pos, accel = float3.zero, nodeOffset = float3.zero, vel = float3.zero });
        ecb.AddComponent(sortKey, newRocket, new InitialTransform { Value = initialTransform });
        ecb.AddComponent(sortKey, newRocket, new NextTransform { nextPos = pos, scale = 1.0f, facing = facing });
        ecb.AddComponent(sortKey, newRocket, new ConstantThrust { thrust = facing * 10.1f });

        ecb.AddComponent(sortKey, newRocket, ThrustHaver.One(new float3(0, -5.0f, 0), float4x4.RotateX(math.radians(270)), float4x4.Scale(new float3(80, 80, 240)), true));

        ecb.AddComponent(sortKey, newRocket, new NeedsDestroy { destroyTime = elapsedTime + 1.5 });
        ecb.AddComponent(sortKey, newRocket, new DestroyOnLevelUnload());
        return newRocket;
    }

    public Entity CreateNodeAsync(int sortKey, EntityCommandBuffer.ParallelWriter ecb, float3 pos, Entity connectionEntity)
    {
        Entity newNode = ecb.Instantiate(sortKey, prototypes.nodePrototype);

        float scale = Globals.sharedLevelInfo.Data.nodeSize;
        float4x4 localToWorldData = math.mul(float4x4.Translate(pos), float4x4.Scale(scale));
        ecb.AddComponent(sortKey, newNode, new LocalToWorld { Value = localToWorldData });
        ecb.AddComponent(sortKey, newNode, new GridNode { velocity = float3.zero, isDead = false, isBorder = false });
        if (connectionEntity != Entity.Null)
        {
            ecb.AddComponent(sortKey, newNode, new NeedsConnection { connection = connectionEntity });
        }
        ecb.AddComponent(sortKey, newNode, new DestroyOnLevelUnload());
        return newNode;
    }

    public Entity CreateNodeNow(EntityManager em, float3 pos, bool isBorder)
    {
        Entity e = em.Instantiate(prototypes.nodePrototype);

        float scale = Globals.sharedLevelInfo.Data.nodeSize;
        float4x4 localToWorldData = math.mul(float4x4.Translate(pos), float4x4.Scale(scale));

        em.AddComponentData(e, new GridNode { velocity = float3.zero, isDead = false, isBorder = isBorder });
        em.AddComponentData(e, new LocalToWorld { Value = localToWorldData });
        em.AddComponentData(e, new DestroyOnLevelUnload());
        return e;
    }

    public Entity CreateThrust1Async(int sortKey, EntityCommandBuffer.ParallelWriter ecb, Entity parent, ThrustHaver th, int thrusterNumber, float4x4 parentTransform)
    {
        Entity newThrust = ecb.Instantiate(sortKey, prototypes.thrust1Prototype);
        ecb.AddComponent(sortKey, newThrust, new Parent { Value = parent });

        float4x4 anchor = float4x4.Translate(th.GetPos(thrusterNumber));
        float4x4 transform = math.mul(anchor, math.mul(th.rotation, th.scale));
        ecb.AddComponent(sortKey, newThrust, new LocalToWorld { Value = math.mul(parentTransform, transform) });
        ecb.AddComponent(sortKey, newThrust, new RelativeTransform { Value = transform, lastParentValue = parentTransform });

        ecb.AddComponent(sortKey, newThrust, new DestroyOnLevelUnload());

        ecb.AddComponent(sortKey, newThrust, new Thrust { thrusterNumber = thrusterNumber });
        ecb.AddComponent(sortKey, newThrust, new NeedsAssignThrustEntity { parentEntity = parent, thrusterNumber = thrusterNumber });
        return newThrust;
    }
}
