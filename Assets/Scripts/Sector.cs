using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System.Collections.Generic;
using com.borismez.ShockwavesHDRP;

public class Sector : MonoBehaviour
{
    [SerializeField]
    [Range(0f, 4f)]
    float explosionSize = 1.0f;

    Map parent;
    Camera mainCamera;
    private Dictionary<string, PartsRenderInfo> partsRenderInfos;
    private Dictionary<string, ShipInfo> shipInfos;
    private Dictionary<string, StationTypeInfo> stationTypeInfos;
    private ExplosionManager explosionManager;
    Dictionary<Entity, List<GameObject>> lightObjects = new Dictionary<Entity, List<GameObject>>();

    string displayName;

    float sideLength;
    int numSideNodes;
    float3 startPos;

    int numNodes;
    float nodeDistance;
    float3 nodeOffset;

    private EntityManager em;
    private Entity playerEntity;

    private bool isPlayerJumping = false;

    private EntityQuery needExplosionEntityQuery;

    void Start()
    {
    }

    public void Initialize(SectorInfo info, Camera mainCamera, Map parent, Dictionary<string, PartsRenderInfo> partsRenderInfos, ShipInfos shipInfos, StationTypeInfos stationInfos, ExplosionManager explosionManager)
    {
        this.displayName = info.name;
        this.sideLength = info.sideLength;
        this.numSideNodes = info.sideNodes;
        this.startPos = info.startPosition;

        this.parent = parent;
        this.mainCamera = mainCamera;
        this.partsRenderInfos = partsRenderInfos;
        this.shipInfos = shipInfos.ToDictionary();
        this.stationTypeInfos = stationInfos.ToDictionary();
        this.explosionManager = explosionManager;

        this.numNodes = numSideNodes * numSideNodes;
        this.nodeDistance = sideLength / (float)numSideNodes;
        this.nodeOffset = new float3(0, nodeDistance, 0);

        Globals.sharedLevelInfo.Data.Initialize(info.sectorId, nodeDistance, info.nodeSize);

        this.em = World.DefaultGameObjectInjectionWorld.EntityManager;

        int numBorderNodes = 2 * numSideNodes + 2 * (numSideNodes - 2);

        NativeArray<Entity> borderNodes = new NativeArray<Entity>(numBorderNodes, Allocator.Temp);
        NativeArray<Entity> nonBorderNodes = new NativeArray<Entity>(numNodes - numBorderNodes, Allocator.Temp);
        GenerateNodes(borderNodes, nonBorderNodes);
        GenerateConnections(borderNodes, nonBorderNodes);
        foreach(StationInfo si in info.stationInfos)
        {
            AddStation(si.name, si.type, si.position, si.size, si.factionIndex, si.moduleInfos);
        }
        this.playerEntity = AddShip("Scaphe", this.startPos, true);
        //AddShip("Scaphe", new float3(-4,1,0), false);

        needExplosionEntityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NeedsDestroy>().Build(em);
    }

    void Update()
    {
        UpdateLights();

        if (isPlayerJumping) { return; }

        float3 shipPos = em.GetComponentData<LocalToWorld>(playerEntity).Position;
        float camPosScale = 0.9f;
        mainCamera.transform.position = new Vector3(shipPos.x * camPosScale, shipPos.y * camPosScale, mainCamera.transform.position.z);

        Ship ship = em.GetComponentData<Ship>(playerEntity);
        if(ship.ShouldJumpNow())
        {
            isPlayerJumping = true;
            int newSectorId = ship.hyperspaceTarget;
            parent.Jump(newSectorId);
        }

        NativeArray<Entity> entities = needExplosionEntityQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; ++i)
        {
            Entity entity = entities[i];
            NeedsDestroy nd = em.GetComponentData<NeedsDestroy>(entity);
            if (nd.destroyTime < Time.time && nd.explosionShowed == false)
            {
                LocalToWorld ltw = em.GetComponentData<LocalToWorld>(entity);
                explosionManager.AddExplosion(ltw.Position, mainCamera, explosionSize);
                em.SetComponentData<NeedsDestroy>(entity, new NeedsDestroy { destroyTime = nd.destroyTime, explosionShowed = true });
            }
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

            int[] raw = new int[] { raw2D.x, raw2D.y };
            float x = (float)(raw[0] - numSideNodes / 2) * nodeDistance;
            float y = (float)(raw[1] - numSideNodes / 2) * nodeDistance;
            bool isBorder = IsBorder(raw);
            Entity addedNode;
            addedNode = AddNode(new float3(x, y, 0) + nodeOffset, isBorder);
            
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
        return Globals.sharedEntityFactory.Data.CreateNodeNow(em, pos, isBorder);
    }

    private void AddStation(string name, string type, float3 pos, float size, int factionIndex, StationModuleInfo[] moduleInfos)
    {
        EntityArchetype ea = em.CreateArchetype(typeof(Station));
        Entity e = em.CreateEntity(ea);
        em.AddComponentData(e, new LocalToWorld { Value = float4x4.Translate(pos) });
        em.AddComponentData(e, new NextTransform { facing = new float3(1, 0, 0), nextPos = pos, scale = 1f });

        StationModules modules = new StationModules();
        foreach(StationModuleInfo moduleInfo in moduleInfos)
        {
            StationModule module = new StationModule { type = StationModuleTypeFromString(moduleInfo.type) };
            foreach(float param in moduleInfo.parameters)
            {
                module.AddParam(param);
            }
            modules.Add(module);
        }
        Station s = new Station { size = size, factionIndex = factionIndex, modules = modules };
        em.AddComponentData(e, s);
        em.AddComponentData(e, new DestroyOnLevelUnload());

        StationTypeInfo info = stationTypeInfos[type];

        lightObjects[e] = new List<GameObject>();
        foreach (LightInfo li in info.displayInfo.lights)
        {
            AddLight(e, pos, li);
        }
        partsRenderInfos[type].AddRenderComponents(em, e, info.displayInfo.anchor, info.displayInfo.initialRotationDegrees, info.displayInfo.initialScale);
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

        em.AddComponentData(e, new NextTransform { facing = new float3(1, 0, 0), nextPos = pos, scale = 1f });
        em.AddComponentData(e, new Accelerating { prevPos = pos, accel = float3.zero, prevAccel = float3.zero, vel = float3.zero, nodeOffset = float3.zero, closestNodes = ClosestNodes.empty }); ;

        Ship s = new Ship
        {
            size = 0.25f,
            maxThrust = info.maxThrust,
            jerk = info.jerk,
            rotationSpeed = info.rotationSpeed,
            weaponSlots = WeaponSlots.Empty,
            shootingPrimary = false,
            shootingSecondary = false,
            target = Entity.Null
        };
        s.weaponSlots.Set(new WeaponSlot { type = WeaponType.StraightRocket, relativePos = new float3(0, 0, 0), damage = 10f, speed = 2f, isSecondary = false, secondsBetweenFire = 0.5f, lastFireSeconds = 0 }, 0);
        s.weaponSlots.Set(new WeaponSlot { type = WeaponType.StraightRocket, relativePos = new float3(0, 0, 0), damage = 10f, speed = 2f, isSecondary = true, secondsBetweenFire = 0.25f, lastFireSeconds = 0 }, 1);
        s.weaponSlots.Set(new WeaponSlot { type = WeaponType.StraightRocket, relativePos = new float3(0, 0, 0), damage = 10f, speed = 2f, isSecondary = true, secondsBetweenFire = 0.25f, lastFireSeconds = 0 }, 2);
        em.AddComponentData(e, s);
        em.AddComponentData(e, new Docked { dockedAt = Entity.Null, isUndocking = false });
        if (isPlayer)
        {
            em.AddComponentData(e, new Player { });
        }
        else
        {
            em.AddComponentData(e, new AIData { type = AIData.Type.GoToPosition, target = playerEntity, phase = 0 });
        }

        em.AddComponentData(e, ThrustHaver.Two(new float3(-0.35f, 0.3f, 0), new float3(-0.35f, -0.3f, 0), 270f, 10f, false));

        em.AddComponentData(e, new DestroyOnLevelUnload());

        lightObjects[e] = new List<GameObject>();
        foreach (LightInfo li in info.displayInfo.lights)
        {
            AddLight(e, pos, li);
        }
        partsRenderInfos[name].AddRenderComponents(em, e, info.displayInfo.anchor, info.displayInfo.initialRotationDegrees, info.displayInfo.initialScale);

        return e;
    }

    private void AddLight(Entity e, float3 pos, LightInfo lightInfo)
    {
        GameObject lightObject = new GameObject("Light");
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
            if(em.HasComponent<LocalToWorld>(pair.Key))
            {
                foreach (GameObject light in pair.Value)
                {
                    if (em.HasComponent<Ship>(pair.Key))
                    {
                        light.SetActive(em.GetComponentData<Ship>(pair.Key).lightsOn);
                    }

                    Vector3 relativePos = Vector3.zero;
                    Vector3 relativeFacing = Vector3.right;
                    LightInfoBehavior behavior = light.GetComponent<LightInfoBehavior>();
                    if (behavior != null)
                    {
                        relativePos = behavior.lightInfo.RelativePos();
                        relativeFacing = behavior.lightInfo.RelativeFacing();
                    }

                    Vector3 facing = Vector3.right;
                    if (em.HasComponent<NextTransform>(pair.Key))
                    {
                        facing = em.GetComponentData<NextTransform>(pair.Key).facing;
                    }

                    float signedAngle = Vector3.SignedAngle(Vector3.right, facing, Vector3.forward);
                    Quaternion rotation = Quaternion.AngleAxis(signedAngle, Vector3.forward);
                    Vector3 rotatedFacing = rotation * relativeFacing;

                    light.transform.forward = (facing + rotatedFacing).normalized;

                    Vector3 pos = em.GetComponentData<LocalToWorld>(pair.Key).Position;
                    Vector3 rotatedRelativePos = rotation * relativePos;

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
