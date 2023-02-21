using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Collections;

public static class Globals
{
    public readonly static SharedStatic<InputState> sharedInputState = SharedStatic<InputState>.GetOrCreate<InputStateKey>();
    public readonly static SharedStatic<TimeState> sharedTimeState = SharedStatic<TimeState>.GetOrCreate<TimeStateKey>();
    private class InputStateKey { }
    private class TimeStateKey { }

    static Globals()
    {
        sharedInputState.Data.Initialize();
        sharedTimeState.Data.Initialize();
    }
}
public struct TimeState
{
    public float deltaTime;
    public void Initialize()
    {
        deltaTime = 0.0167f;
    }
}

public struct InputState
{
    public bool isSpaceDown;
    public bool isIKeyDown;
    public bool isUpKeyDown;
    public bool isDownKeyDown;
    public bool isLeftKeyDown;
    public bool isRightKeyDown;
    public void Initialize()
    {
        isSpaceDown = false;
        isIKeyDown = false;
        isUpKeyDown = false;
        isDownKeyDown = false;
        isLeftKeyDown = false;
        isRightKeyDown = false;
    }
}

public class GameManager : MonoBehaviour
{
    [SerializeField]
    Camera mainCamera;

    GameObject mapObject;

    // Start is called before the first frame update
    void Start()
    {
        mapObject = new GameObject("Map");
        Map map = mapObject.AddComponent<Map>();
        map.Instantiate(mainCamera);
    }

    // Update is called once per frame
    void Update()
    {
        Globals.sharedTimeState.Data.deltaTime = Time.deltaTime;
 
        UpdateInput();

        UpdateFPSCounter();
    }

    private void UpdateInput()
    {
        Globals.sharedInputState.Data.isSpaceDown = Input.GetKey(KeyCode.Space);
        Globals.sharedInputState.Data.isIKeyDown = Input.GetKey(KeyCode.I);
        Globals.sharedInputState.Data.isUpKeyDown = Input.GetKey(KeyCode.UpArrow);
        Globals.sharedInputState.Data.isDownKeyDown = Input.GetKey(KeyCode.DownArrow);
        Globals.sharedInputState.Data.isLeftKeyDown = Input.GetKey(KeyCode.LeftArrow);
        Globals.sharedInputState.Data.isRightKeyDown = Input.GetKey(KeyCode.RightArrow);
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
        Vector3 labelPos = new Vector3(camPos.x - 4.3f, camPos.y - 4.4f, 0);
        UnityEditor.Handles.Label(labelPos, "FPS: " + (int)(fps), style);
    }
}
