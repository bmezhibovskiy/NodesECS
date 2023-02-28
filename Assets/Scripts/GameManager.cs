using UnityEngine;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Assertions;

public static class Globals
{
    public readonly static SharedStatic<InputState> sharedInputState = SharedStatic<InputState>.GetOrCreate<InputStateKey>();
    public readonly static SharedStatic<EntityPrototypes> sharedPrototypes = SharedStatic<EntityPrototypes>.GetOrCreate<EntityPrototypesKey>();
    public readonly static SharedStatic<LevelInfo> sharedLevelInfo = SharedStatic<LevelInfo>.GetOrCreate<LevelInfoKey>();
    private class InputStateKey { }
    private class EntityPrototypesKey { }
    private class LevelInfoKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
        sharedLevelInfo.Data.Initialize();
    }
}

public struct InputState
{
    public bool AfterburnerKeyDown;
    public bool ForwardThrustKeyDown;
    public bool ReverseThrustKeyDown;
    public bool RotateLeftKeyDown;
    public bool RotateRightKeyDown;
    public bool HyperspaceKeyDown;

    public void Initialize()
    {
        AfterburnerKeyDown = false;
        ForwardThrustKeyDown = false;
        ReverseThrustKeyDown = false;
        RotateLeftKeyDown = false;
        RotateRightKeyDown = false;
        HyperspaceKeyDown = false;
    }
}

public struct EntityPrototypes
{
    public Entity nodePrototype;
}

public struct LevelInfo
{
    public int sectorIndex;
    public float nodeDistance;
    public float nodeSize;
    public bool needsDestroy;
    public void Initialize(int sectorIndex = 0, float nodeDistance = 0, float nodeSize = 0)
    {
        this.sectorIndex = sectorIndex;
        this.nodeDistance = nodeDistance;
        this.nodeSize = nodeSize;
        this.needsDestroy = false;
    }
}

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
    public Dictionary<string, PartRenderInfo> parts = new Dictionary<string, PartRenderInfo>();
    public void AddPart(string name, PartRenderInfo info)
    {
        parts[name] = info;
    }
}

public class GameManager : MonoBehaviour
{
    [SerializeField]
    Camera mainCamera;

    [SerializeField]
    Mesh nodeMesh;

    [SerializeField]
    Material nodeMaterial;

    ShipInfos shipInfos;

    Dictionary<string, PartsRenderInfo> partsRenderInfos = new Dictionary<string, PartsRenderInfo>();

    GameObject mapObject;

    // Start is called before the first frame update
    void Start()
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;

        shipInfos = ShipInfos.FromJsonFile("Ships.json");

        foreach(ShipInfo shipInfo in shipInfos.ships)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            foreach (ShipPartInfo partInfo in shipInfo.parts)
            {
                string materialPath = shipInfo.path + "/" + partInfo.material;
                materials[partInfo.mesh] = Resources.Load<Material>(materialPath);
            }
            PartsRenderInfo pri = new PartsRenderInfo();
            string meshBundlePath = shipInfo.path + "/" + shipInfo.meshBundle;
            GameObject shipObject = Resources.Load(meshBundlePath) as GameObject;
            for (int i = 0; i < shipObject.transform.childCount; ++i)
            {
                Transform childTransform = shipObject.transform.GetChild(i);
                GameObject child = childTransform.gameObject;
                Mesh mesh = child.GetComponent<MeshFilter>().sharedMesh;
                Material mat = materials[child.name];
                Assert.IsNotNull(mat);
                pri.AddPart(child.name, new PartRenderInfo(mesh, mat, childTransform));
                
            }
            partsRenderInfos[shipInfo.name] = pri;
        }

        SetUpPrototypes();

        mapObject = new GameObject("Map");
        Map map = mapObject.AddComponent<Map>();

        map.Instantiate(mainCamera, partsRenderInfos, shipInfos);
    }

    private void SetUpPrototypes()
    {
        EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype ea = em.CreateArchetype();
        Entity nodePrototype = em.CreateEntity(ea);

        RenderMeshDescription rmd = new RenderMeshDescription(ShadowCastingMode.Off, false);
        RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { nodeMaterial }, new Mesh[] { nodeMesh });
        RenderMeshUtility.AddComponents(nodePrototype, em, rmd, renderMeshArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

        Globals.sharedPrototypes.Data.nodePrototype = nodePrototype;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateInput();

        UpdateFPSCounter();
    }

    private void UpdateInput()
    {
        Globals.sharedInputState.Data.AfterburnerKeyDown = Input.GetKey(KeyCode.Space);
        Globals.sharedInputState.Data.ForwardThrustKeyDown = Input.GetKey(KeyCode.UpArrow);
        Globals.sharedInputState.Data.ReverseThrustKeyDown = Input.GetKey(KeyCode.DownArrow);
        Globals.sharedInputState.Data.RotateLeftKeyDown = Input.GetKey(KeyCode.LeftArrow);
        Globals.sharedInputState.Data.RotateRightKeyDown = Input.GetKey(KeyCode.RightArrow);
        Globals.sharedInputState.Data.HyperspaceKeyDown = Input.GetKey(KeyCode.H);
    }


    private const int maxFpsHistoryCount = 60;
    private List<float> fpsHistory = new List<float>();
    private float fps = 0;
    private void UpdateFPSCounter()
    {
        fpsHistory.Add(1f / Time.deltaTime);
        if (fpsHistory.Count > maxFpsHistoryCount) { fpsHistory.RemoveAt(0); }
        if (Time.frameCount % maxFpsHistoryCount == 0)
        {
            float total = 0;
            foreach (float f in fpsHistory)
            {
                total += f;
            }
            fps = total / fpsHistory.Count;
        }
    }

    private void OnDrawGizmos()
    {
        GUIStyle style = GUI.skin.label;
        style.fontSize = 6;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 labelPos = new Vector3(camPos.x - 2.3f, camPos.y - 2.4f, 0);
        UnityEditor.Handles.Label(labelPos, "FPS: " + (int)(fps), style);
    }
}
