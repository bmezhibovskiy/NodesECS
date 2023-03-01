using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using System.Collections.Generic;
using UnityEngine.Rendering;

public struct DestroyOnLevelUnload: IComponentData
{

}

public class Sector : MonoBehaviour
{
    Map parent;
    Camera mainCamera;
    private Dictionary<string, PartsRenderInfo> partsRenderInfos;
    private Dictionary<string, ShipInfo> shipInfos;
    Dictionary<Entity, List<GameObject>> lightObjects = new Dictionary<Entity, List<GameObject>>();

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


    public void Initialize(SectorInfo info, Camera mainCamera, Map parent, Dictionary<string, PartsRenderInfo> partsRenderInfos, ShipInfos shipInfos)
    {
        this.displayName = info.name;
        this.is3d = info.is3d;
        this.sideLength = info.sideLength;
        this.numSideNodes = info.sideNodes;
        this.startPos = info.startPosition;

        this.parent = parent;
        this.mainCamera = mainCamera;
        this.partsRenderInfos = partsRenderInfos;
        this.shipInfos = shipInfos.ToDictionary();

        mainCamera.orthographic = !is3d;

        this.numNodes = is3d ? numSideNodes * numSideNodes * numSideNodes : numSideNodes * numSideNodes;
        this.nodeDistance = sideLength / (float)numSideNodes;
        this.nodeOffset = new float3(0, nodeDistance, 0);

        Globals.sharedLevelInfo.Data.Initialize(info.sectorId, nodeDistance, info.nodeSize);

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
        this.playerEntity = AddShip("Scaphe", this.startPos, true);
    }

    void Update()
    {
        UpdateLights();

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

    private void OnDestroy()
    {
        foreach (Entity key in lightObjects.Keys)
        {
            if (lightObjects[key] == null) { continue; }

            foreach (GameObject light in lightObjects[key])
            {
                Destroy(light);
            }
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

        float scale = Globals.sharedLevelInfo.Data.nodeSize;
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
        Station s = new Station { displayName = new FixedString128Bytes(name), size = size, factionIndex = factionIndex, modules = modules };
        em.AddComponentData(e, s);
        em.AddComponentData(e, new DestroyOnLevelUnload());

        lightObjects[e] = new List<GameObject>();
        AddStationLight(e, s, pos);
    }

    private void AddStationLight(Entity e, Station s, float3 pos)
    {
        GameObject lightObject = new GameObject("Station Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = 250;
        light.color = Color.white;
        light.transform.position = pos;
        lightObjects[e].Add(lightObject);
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

    private Entity AddShip(string name, float3 pos, bool isPlayer)
    {
        EntityArchetype ea = isPlayer ?
            em.CreateArchetype(typeof(Player)) :
            em.CreateArchetype(typeof(Ship));

        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });

        ShipInfo info = shipInfos[name];
        Ship s = new Ship
        {
            size = 0.25f,
            closestNodes = ClosestNodes.empty,
            nodeOffset = float3.zero,
            prevPos = pos,
            nextPos = pos,
            facing = new float3(1, 0, 0),
            accel = float3.zero,
            vel = float3.zero,
            thrust = info.thrust,
            rotationSpeed = info.rotationSpeed,
            initialScale = float4x4.Scale(info.initialScale),
            initialRotation = math.mul(float4x4.RotateX(math.radians(info.initialRotationDegrees[0])), math.mul(float4x4.RotateY(math.radians(info.initialRotationDegrees[1])), float4x4.RotateZ(math.radians(info.initialRotationDegrees[2])))),
            dockedAt = Entity.Null,
            isUndocking = false
        };
        em.AddComponentData(e, s);
        if (isPlayer)
        {
            em.AddComponentData(e, new Player { });
        }
        em.AddComponentData(e, new DestroyOnLevelUnload());

        lightObjects[e] = new List<GameObject>();
        foreach (LightInfo li in info.lights)
        {
            AddShipLight(e, s, pos, li);
        }

        foreach(KeyValuePair<string, PartRenderInfo> pair in partsRenderInfos[name].parts)
        {
            Entity child = em.CreateEntity();
            em.AddComponentData(child, new Parent { Value = e });
            float4x4 transform = pair.Value.transform.localToWorldMatrix;
            em.AddComponentData(child, new LocalToWorld { Value = transform });
            em.AddComponentData(child, new RelativeTransform { Value = transform, lastParentValue = float4x4.zero });
            em.AddComponentData(child, new DestroyOnLevelUnload());

            RenderMeshDescription rmd = new RenderMeshDescription(ShadowCastingMode.On, true);
            RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { pair.Value.material }, new Mesh[] { pair.Value.mesh });
            RenderMeshUtility.AddComponents(child, em, rmd, renderMeshArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        return e;
    }

    private void AddShipLight(Entity e, Ship s, float3 pos, LightInfo lightInfo)
    {
        GameObject lightObject = new GameObject("Ship Light");
        Light light = lightObject.AddComponent<Light>();
        if (lightInfo.IsPoint())
        {
            light.type = LightType.Point;
        }
        else
        {
            light.type = LightType.Spot;
            light.spotAngle = lightInfo.spotAngleDegrees;
            light.innerSpotAngle = lightInfo.spotInnerAngleDegrees;
        }
        light.intensity = lightInfo.intensity;
        light.color = lightInfo.GetColor();
        lightInfo.AddToGameObject(lightObject);
        lightObjects[e].Add(lightObject);
    }

    public void UpdateLights()
    {
        List<Entity> toRemove = new List<Entity>();
        foreach(KeyValuePair<Entity, List<GameObject>> pair in lightObjects)
        {
            if(em.HasComponent<LocalToWorld>(pair.Key) && em.HasComponent<Ship>(pair.Key))
            {
                foreach (GameObject light in pair.Value)
                {
                    Vector3 pos = em.GetComponentData<LocalToWorld>(pair.Key).Position;
                    Vector3 facing = em.GetComponentData<Ship>(pair.Key).facing;
                    Vector3 relativePos = float3.zero;
                    Vector3 relativeFacing = float3.zero;
                    LightInfoBehavior behavior = light.GetComponent<LightInfoBehavior>();
                    if(behavior != null)
                    {
                        relativePos = behavior.lightInfo.RelativePos();
                        relativeFacing = behavior.lightInfo.RelativeFacing();
                    }

                    float signedAngle = Vector3.SignedAngle(Vector3.right, facing, Vector3.forward);
                    Quaternion rotation = Quaternion.AngleAxis(signedAngle, Vector3.forward);
                    Vector3 rotatedRelativePos = rotation * relativePos;
                    Vector3 rotatedFacing = rotation * relativeFacing;

                    light.transform.forward = (facing + rotatedFacing).normalized;
                    light.transform.position = pos + rotatedRelativePos;
                }
            }
            else
            {
                toRemove.Add(pair.Key);
            }
        }
        foreach(Entity removeThis in toRemove)
        {
            if (lightObjects[removeThis] == null) { continue; }

            foreach(GameObject light in lightObjects[removeThis])
            {
                Destroy(light);
            }
            lightObjects[removeThis] = null;
        }
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
