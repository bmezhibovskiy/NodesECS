using UnityEngine;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.Rendering;

public static class Globals
{
    public readonly static SharedStatic<InputState> sharedInputState = SharedStatic<InputState>.GetOrCreate<InputStateKey>();
    public readonly static SharedStatic<EntityFactory> sharedEntityFactory = SharedStatic<EntityFactory>.GetOrCreate<EntityFactoryKey>();
    public readonly static SharedStatic<LevelInfo> sharedLevelInfo = SharedStatic<LevelInfo>.GetOrCreate<LevelInfoKey>();
    public readonly static SharedStatic<Unity.Mathematics.Random> sharedRandom = SharedStatic<Unity.Mathematics.Random>.GetOrCreate<RandomGeneratorKey>();
    private class InputStateKey { }
    private class EntityFactoryKey { }
    private class LevelInfoKey { }
    private class RandomGeneratorKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
        sharedLevelInfo.Data.Initialize();
        sharedRandom.Data = new Unity.Mathematics.Random(123);
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
    public bool LightsKeyPressed;
    public bool PrimaryWeaponKeyDown;
    public bool SecondaryWeaponKeyDown;
    public bool MinimapClicked;
    public float ScrollWheelDelta;

    public void Initialize()
    {
        AfterburnerKeyDown = false;
        ForwardThrustKeyDown = false;
        ReverseThrustKeyDown = false;
        RotateLeftKeyDown = false;
        RotateRightKeyDown = false;
        HyperspaceKeyDown = false;
        LightsKeyPressed = false;
        PrimaryWeaponKeyDown = false;
        SecondaryWeaponKeyDown = false;
        MinimapClicked = false;
        ScrollWheelDelta = 0;
    }
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

public class GameManager : MonoBehaviour
{
    [SerializeField]
    Camera mainCamera;

    [SerializeField]
    Camera minimapCamera;

    [SerializeField]
    Camera targetCamera;

    [SerializeField]
    ExplosionManager explosionManager;

    [SerializeField]
    GameObject minimapLight;

    [SerializeField]
    GameObject targetLight;

    ShipInfos shipInfos;
    StationTypeInfos stationTypeInfos;

    Dictionary<string, PartsRenderInfo> partsRenderInfos = new Dictionary<string, PartsRenderInfo>();

    GameObject mapObject;

    private float originalFov;


    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        targetLight.GetComponent<Light>().enabled = camera == targetCamera;
        minimapLight.GetComponent<Light>().enabled = camera == minimapCamera;
    }

    void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void Start()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

        GraphicsSettings.useScriptableRenderPipelineBatching = true;

        originalFov = mainCamera.fieldOfView;

        shipInfos = ShipInfos.FromJsonFile("Ships.json");

        foreach(ShipInfo shipInfo in shipInfos.ships)
        {
            partsRenderInfos[shipInfo.name] = PartsRenderInfo.CreatePartsRenderInfo(shipInfo.displayInfo);
        }

        stationTypeInfos = StationTypeInfos.FromJsonFile("Stations.json");

        foreach(StationTypeInfo stationTypeInfo in stationTypeInfos.stations)
        {
            partsRenderInfos[stationTypeInfo.name] = PartsRenderInfo.CreatePartsRenderInfo(stationTypeInfo.displayInfo);
        }    

        SetUpPrototypes();

        mapObject = new GameObject("Map");
        Map map = mapObject.AddComponent<Map>();

        map.Instantiate(mainCamera, targetCamera, partsRenderInfos, shipInfos, stationTypeInfos, explosionManager);
    }

    private void SetUpPrototypes()
    {
        Globals.sharedEntityFactory.Data.SetUpPrototypes(World.DefaultGameObjectInjectionWorld.EntityManager);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateInput();

        UpdateMinimapSize();

        UpdateZoom();


        UpdateFPSCounter();
    }

    private void UpdateMinimapSize()
    {
        //TODO: Put this somewhere else like Sector
        if (Globals.sharedInputState.Data.MinimapClicked)
        {
            //Cycle through minimap sizes
            float min = 12;
            float max = 44;
            float step = 8;
            minimapCamera.orthographicSize += step;
            if (minimapCamera.orthographicSize > max)
            {
                minimapCamera.orthographicSize = min;
            }
        }
        //
    }

    private void UpdateZoom()
    {
        float delta = Globals.sharedInputState.Data.ScrollWheelDelta;

        if (delta < 0 && mainCamera.fieldOfView != originalFov)
        {
            mainCamera.fieldOfView = originalFov;
        }
        else if (delta > 0 && mainCamera.fieldOfView == originalFov)
        {
            mainCamera.fieldOfView = originalFov * 0.5f;
        }
    }

    private void UpdateInput()
    {
        Globals.sharedInputState.Data.AfterburnerKeyDown = Input.GetKey(KeyCode.Z);
        Globals.sharedInputState.Data.ForwardThrustKeyDown = Input.GetKey(KeyCode.UpArrow);
        Globals.sharedInputState.Data.ReverseThrustKeyDown = Input.GetKey(KeyCode.DownArrow);
        Globals.sharedInputState.Data.RotateLeftKeyDown = Input.GetKey(KeyCode.LeftArrow);
        Globals.sharedInputState.Data.RotateRightKeyDown = Input.GetKey(KeyCode.RightArrow);
        Globals.sharedInputState.Data.HyperspaceKeyDown = Input.GetKey(KeyCode.H);
        Globals.sharedInputState.Data.LightsKeyPressed = Input.GetKeyDown(KeyCode.L);
        Globals.sharedInputState.Data.PrimaryWeaponKeyDown = Input.GetKey(KeyCode.Space);
        Globals.sharedInputState.Data.SecondaryWeaponKeyDown = Input.GetKey(KeyCode.X);
        Globals.sharedInputState.Data.ScrollWheelDelta = Input.mouseScrollDelta.y;
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
