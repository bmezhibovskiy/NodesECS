using UnityEngine;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Transforms;

public static class Globals
{
    public readonly static SharedStatic<InputState> sharedInputState = SharedStatic<InputState>.GetOrCreate<InputStateKey>();
    public readonly static SharedStatic<EntityPrototypes> sharedPrototypes = SharedStatic<EntityPrototypes>.GetOrCreate<EntityPrototypesKey>();
    private class InputStateKey { }
    private class EntityPrototypesKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
    }
}

public struct InputState
{
    public bool AfterburnerKeyDown;
    public bool ForwardThrustKeyDown;
    public bool ReverseThrustKeyDown;
    public bool RotateLeftKeyDown;
    public bool RotateRightKeyDown;
    public void Initialize()
    {
        AfterburnerKeyDown = false;
        ForwardThrustKeyDown = false;
        ReverseThrustKeyDown = false;
        RotateLeftKeyDown = false;
        RotateRightKeyDown = false;
    }
}

public struct EntityPrototypes
{
    public Entity nodePrototype;
}

public enum GameEntityType
{
    Node
}

public class GameManager : MonoBehaviour
{
    [SerializeField]
    Camera mainCamera;

    [SerializeField]
    Mesh nodeMesh;

    [SerializeField]
    Material nodeMaterial;

    GameObject mapObject;

    // Start is called before the first frame update
    void Start()
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;

        SetUpPrototypes();

        mapObject = new GameObject("Map");
        Map map = mapObject.AddComponent<Map>();

        map.Instantiate(mainCamera);
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
