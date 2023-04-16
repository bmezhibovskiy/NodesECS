using System.Collections.Generic;
using System.Globalization;
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
using UnityEngine.UIElements;

public enum CustomLayer: int
{
    Default = 0, Nodes = 8, WeaponShots = 9, Effects = 10, Minimap = 11, VisibleOnMinimap = 12, Invisible = 13
}

public struct EntityFactory
{
    private struct PartDisplayPrototype
    {
        public Entity partPrototype;
        public float4x4 initialTransform;
    }

    private struct DisplayPrototypes
    {
        public NativeArray<PartDisplayPrototype> nodePrototype;
        public NativeArray<PartDisplayPrototype> rocket1Prototype;
        public NativeArray<PartDisplayPrototype> thrust1Prototype;
        public NativeArray<PartDisplayPrototype> shieldHitPrototype;
    }
    private DisplayPrototypes prototypes;

    public void SetUpPrototypes(EntityManager em)
    {
        prototypes.nodePrototype = new NativeArray<PartDisplayPrototype>(1, Allocator.Persistent);
        prototypes.rocket1Prototype = new NativeArray<PartDisplayPrototype>(1, Allocator.Persistent);
        prototypes.thrust1Prototype = new NativeArray<PartDisplayPrototype>(1, Allocator.Persistent);
        prototypes.shieldHitPrototype = new NativeArray<PartDisplayPrototype>(1, Allocator.Persistent);

        MaterialMeshInfo mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

        Entity nodePrototype = em.CreateEntity();
        Material nodeMaterial = Resources.Load<Material>("Art/Misc/NodeMaterial");
        Mesh nodeMesh = Resources.Load<Mesh>("Art/Misc/Sphere");
        RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { nodeMaterial }, new Mesh[] { nodeMesh });
        RenderMeshUtility.AddComponents(nodePrototype, em, new RenderMeshDescription(ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, (int)CustomLayer.Nodes), renderMeshArray, mmi);
        float4x4 nodeInitialTransform = float4x4.identity;
        prototypes.nodePrototype[0] = (new PartDisplayPrototype { partPrototype = nodePrototype, initialTransform = nodeInitialTransform });

        Entity rocket1Prototype = em.CreateEntity();
        Material rocket1Material = Resources.Load<Material>("Art/Misc/RocketsPalletteRed");
        Mesh rocket1Mesh = Resources.Load<Mesh>("Art/Misc/Rocket01");
        RenderMeshArray renderMeshArray2 = new RenderMeshArray(new Material[] { rocket1Material }, new Mesh[] { rocket1Mesh });
        RenderMeshUtility.AddComponents(rocket1Prototype, em, new RenderMeshDescription(ShadowCastingMode.On, true, MotionVectorGenerationMode.Camera, (int)CustomLayer.WeaponShots), renderMeshArray2, mmi);
        float4x4 rocket1InitialTransform = math.mul(float4x4.RotateZ(math.radians(270)), float4x4.Scale(0.1f));
        prototypes.rocket1Prototype[0] = (new PartDisplayPrototype { partPrototype = rocket1Prototype, initialTransform = rocket1InitialTransform });

        Entity thrust1Prototype = em.CreateEntity();
        Material thrust1Material = Resources.Load<Material>("Art/Misc/FireThrustMaterial");
        Mesh thrust1Mesh = Resources.Load<Mesh>("Art/Misc/uncappedCylinder");
        RenderMeshArray renderMeshArray3 = new RenderMeshArray(new Material[] { thrust1Material }, new Mesh[] { thrust1Mesh });
        RenderMeshUtility.AddComponents(thrust1Prototype, em, new RenderMeshDescription(ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, (int)CustomLayer.Effects), renderMeshArray3, mmi);
        float4x4 thrust1InitialTransform = float4x4.identity;
        prototypes.thrust1Prototype[0] = (new PartDisplayPrototype { partPrototype = thrust1Prototype, initialTransform = thrust1InitialTransform });

        Entity shieldHitPrototype = em.CreateEntity();
        Material shieldHitMaterial = Resources.Load<Material>("Art/Misc/ShieldHitMaterial");
        Mesh shieldHitMesh = Resources.Load<Mesh>("Art/Misc/Sphere");
        RenderMeshArray renderMeshArray4 = new RenderMeshArray(new Material[] { shieldHitMaterial }, new Mesh[] { shieldHitMesh });
        RenderMeshUtility.AddComponents(shieldHitPrototype, em, new RenderMeshDescription(ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, (int)CustomLayer.Effects), renderMeshArray4, mmi);
        float4x4 shieldHitInitialTransform = float4x4.Scale(2.0f);
        prototypes.shieldHitPrototype[0] = (new PartDisplayPrototype { partPrototype = shieldHitPrototype, initialTransform = shieldHitInitialTransform });
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

        AddDisplayChildrenAsync(sortKey, ecb, newRocket, prototypes.rocket1Prototype);
        return newRocket;
    }

    public Entity CreateNodeAsync(int sortKey, EntityCommandBuffer.ParallelWriter ecb, float3 pos, Entity connectionEntity)
    {
        Entity newNode = ecb.CreateEntity(sortKey);

        float4x4 transform = math.mul(float4x4.Translate(pos), float4x4.Scale(Globals.sharedLevelInfo.Data.nodeSize));

        ecb.AddComponent(sortKey, newNode, new LocalToWorld { Value = transform });
        ecb.AddComponent(sortKey, newNode, new GridNode { velocity = float3.zero, isBorder = false });
        if (connectionEntity != Entity.Null)
        {
            ecb.AddComponent(sortKey, newNode, new NeedsConnection { connection = connectionEntity });
        }
        ecb.AddComponent(sortKey, newNode, new DestroyOnLevelUnload());

        AddDisplayChildrenAsync(sortKey, ecb, newNode, prototypes.nodePrototype);

        return newNode;
    }

    public Entity CreateNodeNow(EntityManager em, float3 pos, bool isBorder)
    {
        Entity newNode = em.CreateEntity();

        float4x4 transform = math.mul(float4x4.Translate(pos), float4x4.Scale(Globals.sharedLevelInfo.Data.nodeSize));

        em.AddComponentData(newNode, new LocalToWorld { Value = transform });
        em.AddComponentData(newNode, new GridNode { velocity = float3.zero, isBorder = isBorder });
        em.AddComponentData(newNode, new DestroyOnLevelUnload());

        AddDisplayChildrenNow(em, newNode, prototypes.nodePrototype);

        return newNode;
    }

    public Entity CreateThrust1Async(int sortKey, EntityCommandBuffer.ParallelWriter ecb, Entity parent, ThrustHaver th, int thrusterNumber, float4x4 parentTransform)
    {
        //This is a display-only entity that gets attached to an existing parent
        //But we need to add additional components to the display child
        NativeArray<Entity> thrustDisplayEntities = AddDisplayChildrenAsync(sortKey, ecb, parent, prototypes.thrust1Prototype);
        
        Debug.Assert(thrustDisplayEntities.Length == 1);

        Entity newThrust = thrustDisplayEntities[0];
        ecb.AddComponent(sortKey, newThrust, new Thrust { thrusterNumber = thrusterNumber });
        ecb.AddComponent(sortKey, newThrust, new NeedsAssignThrustEntity { parentEntity = parent, thrusterNumber = thrusterNumber });

        return newThrust;
    }

    public void CreateAOENow(EntityManager em, float3 pos, float radius, float maxTime)
    {
        Entity e = em.CreateEntity();

        em.AddComponentData(e, new AreaOfEffect { radius = radius });
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });
        em.AddComponentData(e, new NeedsDestroy { destroyTime = maxTime, confirmDestroy = true });
        em.AddComponentData(e, new DestroyOnLevelUnload());

        //This is an invisible entity, so it doesn't need display children.
    }

    public void CreateShieldHitAsync(int sortKey, EntityCommandBuffer.ParallelWriter ecb, double maxTime, Entity parent, float4x4 parentTransform)
    {
        //This is a display-only entity that gets attached to an existing parent
        AddDisplayChildrenAsync(sortKey, ecb, parent, prototypes.shieldHitPrototype, maxTime);
    }

    private NativeArray<Entity> AddDisplayChildrenAsync(int sortKey, EntityCommandBuffer.ParallelWriter ecb, Entity parent, NativeArray<PartDisplayPrototype> parts, double destroyTime = 0)
    {
        NativeArray<Entity> children = new NativeArray<Entity>(parts.Length, Allocator.Temp);
        for (int i = 0; i < parts.Length; ++i)
        {
            PartDisplayPrototype pdp = parts[i];
            Entity displayChild = ecb.Instantiate(sortKey, pdp.partPrototype);
            ecb.AddComponent(sortKey, displayChild, new Parent { Value = parent });
            ecb.AddComponent(sortKey, displayChild, new LocalToWorld { Value = pdp.initialTransform });
            ecb.AddComponent(sortKey, displayChild, new RelativeTransform { Value = pdp.initialTransform });
            ecb.AddComponent(sortKey, displayChild, new DestroyOnLevelUnload());
            if(destroyTime > 0)
            {
                ecb.AddComponent(sortKey, displayChild, new NeedsDestroy { destroyTime = destroyTime, confirmDestroy = true });
            }
            children[i] = displayChild;
        }
        return children;
    }

    private NativeArray<Entity> AddDisplayChildrenNow(EntityManager em, Entity parent, NativeArray<PartDisplayPrototype> parts, double destroyTime = 0)
    {
        NativeArray<Entity> children = new NativeArray<Entity>(parts.Length, Allocator.Temp);
        for (int i = 0; i < parts.Length; ++i)
        {
            PartDisplayPrototype pdp = parts[i];
            Entity displayChild = em.Instantiate(pdp.partPrototype);
            em.AddComponentData(displayChild, new Parent { Value = parent });
            em.AddComponentData(displayChild, new LocalToWorld { Value = pdp.initialTransform });
            em.AddComponentData(displayChild, new RelativeTransform { Value = pdp.initialTransform });
            em.AddComponentData(displayChild, new DestroyOnLevelUnload());
            if (destroyTime > 0)
            {
                em.AddComponentData(displayChild, new NeedsDestroy { destroyTime = destroyTime, confirmDestroy = true });
            }
            children[i] = displayChild;
        }
        return children;
    }
}
