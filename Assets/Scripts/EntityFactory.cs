using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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

    public void SetUpPrototypes(EntityManager em)
    {
        RenderMeshDescription rmd = new RenderMeshDescription(ShadowCastingMode.Off, false);
        MaterialMeshInfo mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

        prototypes.nodePrototype = em.CreateEntity();
        
        Material nodeMaterial = Resources.Load<Material>("Art/Misc/NodeMaterial");
        Mesh nodeMesh = Resources.Load<Mesh>("Art/Misc/Sphere");

        RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { nodeMaterial }, new Mesh[] { nodeMesh });
        RenderMeshUtility.AddComponents(prototypes.nodePrototype, em, rmd, renderMeshArray, mmi);


        prototypes.rocket1Prototype = em.CreateEntity();

        Material rocket1Material = Resources.Load<Material>("Art/Misc/RocketsPalletteRed");
        Mesh rocket1Mesh = Resources.Load<Mesh>("Art/Misc/Rocket01");

        RenderMeshArray renderMeshArray2 = new RenderMeshArray(new Material[] { rocket1Material }, new Mesh[] { rocket1Mesh });
        RenderMeshUtility.AddComponents(prototypes.rocket1Prototype, em, rmd, renderMeshArray2, mmi);


        prototypes.thrust1Prototype = em.CreateEntity();
        Material thrust1Material = Resources.Load<Material>("Art/Misc/FireThrustMaterial");
        Mesh thrust1Mesh = Resources.Load<Mesh>("Art/Misc/uncappedCylinder");
        RenderMeshArray renderMeshArray3 = new RenderMeshArray(new Material[] { thrust1Material }, new Mesh[] { thrust1Mesh });
        RenderMeshUtility.AddComponents(prototypes.thrust1Prototype, em, rmd, renderMeshArray3, mmi);
    }

    public Entity CreateRocket1Async(int sortKey, Entity shooter, EntityCommandBuffer.ParallelWriter ecb, float3 pos, float3 facing, double elapsedTime)
    {
        Entity newRocket = ecb.CreateEntity(sortKey);
        float4x4 translate = float4x4.Translate(pos);
        ecb.AddComponent(sortKey, newRocket, new LocalToWorld { Value = translate });
        ecb.AddComponent(sortKey, newRocket, new Accelerating { prevPos = pos, accel = float3.zero, prevAccel = float3.zero, vel = float3.zero });
        ecb.AddComponent(sortKey, newRocket, new NextTransform { nextPos = pos, scale = 1.0f, facing = facing });
        ecb.AddComponent(sortKey, newRocket, new ConstantThrust { thrust = facing * 10.1f });
        ecb.AddComponent(sortKey, newRocket, new WeaponShot { Shooter = shooter, size = 0.1f });

        ecb.AddComponent(sortKey, newRocket, ThrustHaver.One(new float3(-0.37f, 0.0f, 0), 270, 10f, true));

        ecb.AddComponent(sortKey, newRocket, new NeedsDestroy { destroyTime = elapsedTime + 1.0f });
        ecb.AddComponent(sortKey, newRocket, new DestroyOnLevelUnload());

        Entity rocketDisplayChild = ecb.Instantiate(sortKey, prototypes.rocket1Prototype);
        ecb.AddComponent(sortKey, rocketDisplayChild, new Parent { Value = newRocket });
        float4x4 scale = float4x4.Scale(0.1f);
        float4x4 rotation = float4x4.RotateZ(math.radians(270));
        float4x4 initialTransform = math.mul(rotation, scale);
        ecb.AddComponent(sortKey, rocketDisplayChild, new LocalToWorld { Value = initialTransform });
        ecb.AddComponent(sortKey, rocketDisplayChild, new RelativeTransform { Value = initialTransform });
        ecb.AddComponent(sortKey, rocketDisplayChild, new DestroyOnLevelUnload());

        return newRocket;
    }

    public Entity CreateNodeAsync(int sortKey, EntityCommandBuffer.ParallelWriter ecb, float3 pos, Entity connectionEntity)
    {
        Entity newNode = ecb.CreateEntity(sortKey);

        float4x4 scale = float4x4.Scale(Globals.sharedLevelInfo.Data.nodeSize);
        float4x4 translate = float4x4.Translate(pos);
        float4x4 transform = math.mul(translate, scale);

        ecb.AddComponent(sortKey, newNode, new LocalToWorld { Value = transform });
        ecb.AddComponent(sortKey, newNode, new GridNode { velocity = float3.zero, isBorder = false });
        if (connectionEntity != Entity.Null)
        {
            ecb.AddComponent(sortKey, newNode, new NeedsConnection { connection = connectionEntity });
        }
        ecb.AddComponent(sortKey, newNode, new DestroyOnLevelUnload());

        Entity nodeDisplayChild = ecb.Instantiate(sortKey, prototypes.nodePrototype);
        ecb.AddComponent(sortKey, nodeDisplayChild, new Parent { Value = newNode });
        ecb.AddComponent(sortKey, nodeDisplayChild, new LocalToWorld { Value = transform });
        ecb.AddComponent(sortKey, nodeDisplayChild, new RelativeTransform { Value = float4x4.identity });
        ecb.AddComponent(sortKey, nodeDisplayChild, new DestroyOnLevelUnload());

        return newNode;
    }

    public Entity CreateNodeNow(EntityManager em, float3 pos, bool isBorder)
    {
        Entity newNode = em.CreateEntity();

        float4x4 scale = float4x4.Scale(Globals.sharedLevelInfo.Data.nodeSize);
        float4x4 translate = float4x4.Translate(pos);
        float4x4 transform = math.mul(translate, scale);

        em.AddComponentData(newNode, new LocalToWorld { Value = transform });
        em.AddComponentData(newNode, new GridNode { velocity = float3.zero, isBorder = isBorder });
        em.AddComponentData(newNode, new DestroyOnLevelUnload());

        Entity nodeDisplayChild = em.Instantiate(prototypes.nodePrototype);
        em.AddComponentData(nodeDisplayChild, new Parent { Value = newNode });
        em.AddComponentData(nodeDisplayChild, new LocalToWorld { Value = transform });
        em.AddComponentData(nodeDisplayChild, new RelativeTransform { Value = float4x4.identity });
        em.AddComponentData(nodeDisplayChild, new DestroyOnLevelUnload());

        return newNode;
    }

    public Entity CreateThrust1Async(int sortKey, EntityCommandBuffer.ParallelWriter ecb, Entity parent, ThrustHaver th, int thrusterNumber, float4x4 parentTransform)
    {
        Entity newThrust = ecb.Instantiate(sortKey, prototypes.thrust1Prototype);
        ecb.AddComponent(sortKey, newThrust, new Parent { Value = parent });

        float4x4 transform = th.Transform(thrusterNumber, 1);
        ecb.AddComponent(sortKey, newThrust, new LocalToWorld { Value = math.mul(parentTransform, transform) });
        ecb.AddComponent(sortKey, newThrust, new RelativeTransform { Value = transform });

        ecb.AddComponent(sortKey, newThrust, new DestroyOnLevelUnload());

        ecb.AddComponent(sortKey, newThrust, new Thrust { thrusterNumber = thrusterNumber });
        ecb.AddComponent(sortKey, newThrust, new NeedsAssignThrustEntity { parentEntity = parent, thrusterNumber = thrusterNumber });
        return newThrust;
    }
    public Entity CreateAOENow(EntityManager em, float3 pos, float radius, float maxTime)
    {
        Entity e = em.CreateEntity();

        em.AddComponentData(e, new AreaOfEffect { radius = radius });
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });
        em.AddComponentData(e, new NeedsDestroy { destroyTime = maxTime, confirmDestroy = true });
        em.AddComponentData(e, new DestroyOnLevelUnload());
        return e;
    }
}
