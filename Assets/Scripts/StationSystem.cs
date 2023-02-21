using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Assertions;
using Unity.Transforms;

public enum StationModuleType
{
    None, NodePuller, ShipRepellent, Dock
}

public struct StationModule
{
    public readonly static int maxParams = 3;

    public readonly static StationModule Null = new StationModule {
        type = StationModuleType.None,
        param1 = 0,
        param2 = 0,
        param3 = 0,
        paramCount = 0
    };

    public StationModuleType type;
    float param1;
    float param2;
    float param3;
    int paramCount;

    public float GetParam(int index)
    {
        switch (index)
        {
            case 0:
                return param1;
            case 1:
                return param2;
            case 2:
                return param3;
            default:
                return float.NaN;
        }
    }
    public void AddParam(float p)
    {
        if (paramCount >= maxParams) { return; }

        switch (paramCount)
        {
            case 0:
                param1 = p;
                break;
            case 1:
                param2 = p;
                break;
            case 2:
                param3 = p;
                break;
        }
        ++paramCount;
    }
}
public struct StationModules
{
    public readonly static int maxModules = 3;
    public readonly static StationModules empty = new StationModules
    {
        module1 = StationModule.Null,
        module2 = StationModule.Null,
        module3 = StationModule.Null,
        Count = 0
    };

    private StationModule module1;
    private StationModule module2;
    private StationModule module3;
    public int Count;

    public StationModule Get(int index)
    {
        switch (index)
        {
            case 0:
                return module1;
            case 1:
                return module2;
            case 2:
                return module3;
            default:
                return StationModule.Null;
        }
    }

    public void Add(StationModule m)
    {
        if(Count >= maxModules) { return; }

        switch(Count)
        {
            case 0:
                module1 = m;
                break;
            case 1:
                module2 = m;
                break;
            case 2:
                module3 = m;
                break;
        }
        ++Count;
    }
}

public struct Station: IComponentData
{
    //public string displayName;
    public float size;
    public int factionIndex;
    public StationModules modules;
}

[BurstCompile]
public partial struct RenderStationsJob: IJobEntity
{
    void Execute(in Station s, in Translation t)
    {
        Utils.DebugDrawCircle(t.Value, s.size, Color.white, 20);
    }
}

public partial class StationSystem : SystemBase
{
    [BurstCompile]
    protected override void OnUpdate()
    {
        Dependency = new RenderStationsJob().ScheduleParallel(Dependency);
    }
}
