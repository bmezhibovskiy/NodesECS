using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using static UnityEngine.Rendering.DebugUI;
using Unity.Rendering;

public struct DestroyOnLevelUnload: IComponentData
{

}

public class Sector : MonoBehaviour
{
    Map parent;
    Camera mainCamera;

    string displayName;

    bool is3d;
    float sideLength;
    int numSideNodes;
    float3 startPos;

    int numNodes;
    float nodeDistance;
    float3 nodeOffset;

    private EntityManager em;
    private Entity playerEntity;

    private bool isPlayerJumping = false;

    void Start()
    {
    }


    public void Initialize(SectorInfo info, Camera mainCamera, Map parent)
    {
        this.displayName = info.name;
        this.is3d = info.is3d;
        this.sideLength = info.sideLength;
        this.numSideNodes = info.sideNodes;
        this.startPos = info.startPosition;

        this.parent = parent;
        this.mainCamera = mainCamera;

        mainCamera.orthographic = !is3d;

        this.numNodes = is3d ? numSideNodes * numSideNodes * numSideNodes : numSideNodes * numSideNodes;
        this.nodeDistance = sideLength / (float)numSideNodes;
        this.nodeOffset = new float3(0, nodeDistance, 0);

        Globals.sharedLevelInfo.Data.Initialize(info.sectorId, nodeDistance);

        this.em = World.DefaultGameObjectInjectionWorld.EntityManager;

        int numBorderNodes = 2 * numSideNodes + 2 * (numSideNodes - 2);
        if(is3d)
        {
            numBorderNodes = 2 * (numSideNodes * numSideNodes) + 2 * ((numSideNodes - 2) * numSideNodes) + 2 * ((numSideNodes - 2) * (numSideNodes - 2));
        }
        NativeArray<Entity> borderNodes = new NativeArray<Entity>(numBorderNodes, Allocator.Temp);
        NativeArray<Entity> nonBorderNodes = new NativeArray<Entity>(numNodes - numBorderNodes, Allocator.Temp);
        GenerateNodes(borderNodes, nonBorderNodes);
        GenerateConnections(borderNodes, nonBorderNodes);
        foreach(SectorObjectInfo soi in info.sectorObjectInfos)
        {
            AddSectorObject(soi.name, soi.position, soi.size, soi.factionIndex, soi.moduleInfos);
        }
        this.playerEntity = AddShip(this.startPos, true);
    }

    void Update()
    {
        if (isPlayerJumping) { return; }

        float3 shipPos = em.GetComponentData<LocalToWorld>(playerEntity).Position;
        mainCamera.transform.position = new Vector3(shipPos.x, shipPos.y, mainCamera.transform.position.z);

        Ship ship = em.GetComponentData<Ship>(playerEntity);
        if(ship.ShouldJumpNow())
        {
            isPlayerJumping = true;
            int newSectorId = ship.hyperspaceTarget;
            parent.Jump(newSectorId);
        }
        
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 100, 20), displayName);
    }

    private void GenerateNodes(NativeArray<Entity> borderNodes, NativeArray<Entity> nonBorderNodes)
    {
        int numBorderNodes = 0;
        int numNonBorderNodes = 0;
        for (int i = 0; i < numNodes; i++)
        {
            int2 raw2D = Utils.to2D(i, numSideNodes);
            int3 raw3D = Utils.to3D(i, numSideNodes);

            int[] raw = is3d ? new int[] { raw3D.x, raw3D.y, raw3D.z } : new int[] { raw2D.x, raw2D.y };
            float x = (float)(raw[0] - numSideNodes / 2) * nodeDistance;
            float y = (float)(raw[1] - numSideNodes / 2) * nodeDistance;
            bool isBorder = IsBorder(raw);
            Entity addedNode;
            if (is3d)
            {
                float z = (float)(raw[2] - numSideNodes / 2) * nodeDistance;
                addedNode = AddNode(new float3(x, y, z) + nodeOffset, isBorder);
            }
            else
            {
                addedNode = AddNode(new float3(x, y, 0) + nodeOffset, isBorder);
            }
            if(isBorder)
            {
                borderNodes[numBorderNodes++] = addedNode;
            }
            else
            {
                nonBorderNodes[numNonBorderNodes++] = addedNode;
            }
        }
    }
    private void GenerateConnections(NativeArray<Entity> borderNodes, NativeArray<Entity> nonBorderNodes)
    {
        for (int i = 0; i < borderNodes.Length; ++i)
        {
            Entity borderNode = borderNodes[i];

            float3 borderNodePos = em.GetComponentData<LocalToWorld>(borderNode).Position;

            Entity closest = ClosestNode(borderNodePos, nonBorderNodes);
            AddConnection(borderNode, closest);
        }
    }

    private Entity ClosestNode(float3 pos, NativeArray<Entity> nonBorderNodes)
    {
        Entity currentClosest = nonBorderNodes[0];
        float currentDistSq = math.distancesq(pos, em.GetComponentData<LocalToWorld>(currentClosest).Position);
        for (int i = 1; i < nonBorderNodes.Length; ++i)
        {
            Entity candidate = nonBorderNodes[i];
            float3 nodePos = em.GetComponentData<LocalToWorld>(candidate).Position;
            float distSq = math.distancesq(pos, nodePos);
            if (distSq < currentDistSq)
            {
                currentClosest = candidate;
                currentDistSq = distSq;
            }
        }
        return currentClosest;
    }

    private void AddConnection(Entity a, Entity b)
    {
        EntityArchetype ea = em.CreateArchetype(typeof(NodeConnection));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new NodeConnection { a = a, b = b });
        em.AddComponentData(e, new DestroyOnLevelUnload());
    }

    private Entity AddNode(float3 pos, bool isBorder)
    {
        Entity e = em.Instantiate(Globals.sharedPrototypes.Data.nodePrototype);

        float scale = 0.2f;
        float4x4 localToWorldData = math.mul(float4x4.Translate(pos), float4x4.Scale(scale));

        em.AddComponentData(e, new GridNode { velocity = float3.zero, isDead = false, isBorder = isBorder });
        em.AddComponentData(e, new LocalToWorld { Value = localToWorldData });
        if(isBorder)
        {
            em.AddComponentData(e, new HDRPMaterialPropertyBaseColor { Value = new float4(1, 0, 0, 1)});
        }
        em.AddComponentData(e, new DestroyOnLevelUnload());
        return e;
    }

    private void AddSectorObject(string name, float3 pos, float size, int factionIndex, SectorObjectModuleInfo[] moduleInfos)
    {
        EntityArchetype ea = em.CreateArchetype(typeof(Station));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });

        StationModules modules = new StationModules();
        foreach(SectorObjectModuleInfo moduleInfo in moduleInfos)
        {
            StationModule module = new StationModule { type = StationModuleTypeFromString(moduleInfo.type) };
            foreach(float param in moduleInfo.parameters)
            {
                module.AddParam(param);
            }
            modules.Add(module);
        }
        em.AddComponentData(e, new Station { displayName = new FixedString128Bytes(name), size = size, factionIndex = factionIndex, modules = modules });
        em.AddComponentData(e, new DestroyOnLevelUnload());
    }

    private StationModuleType StationModuleTypeFromString(string str)
    {
        switch(str)
        {
            case "NodePuller":
                return StationModuleType.NodePuller;
            case "NodeEater":
                return StationModuleType.NodeEater;
            case "ShipRepellent":
                return StationModuleType.ShipRepellent;
            case "ShipSphereCollider":
                return StationModuleType.ShipSphereCollider;
            case "Dock":
                return StationModuleType.Dock;
        }
        return StationModuleType.None;
    }

    private Entity AddShip(float3 pos, bool isPlayer)
    {
        EntityArchetype ea = isPlayer ?
            em.CreateArchetype(typeof(Player)) :
            em.CreateArchetype(typeof(Ship));

        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });
        em.AddComponentData(e, new Ship
        {
            size = 0.25f,
            closestNodes = ClosestNodes.empty,
            nodeOffset = float3.zero,
            prevPos = pos,
            nextPos = pos,
            facing = new float3(0, 1, 0),
            accel = float3.zero,
            vel = float3.zero,
            dockedAt = Entity.Null,
            isUndocking = false
        });
        if (isPlayer)
        {
            em.AddComponentData(e, new Player { });
        }
        em.AddComponentData(e, new DestroyOnLevelUnload());
        return e;
    }

    private bool IsBorder(int[] raw)
    {
        foreach (int i in raw)
        {
            if (i == 0 || i == numSideNodes - 1)
            {
                return true;
            }
        }
        return false;
    }
}
