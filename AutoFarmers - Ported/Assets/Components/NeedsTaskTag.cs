using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct NeedsTaskTag : IComponentData
{
    
}

public struct MovingTag : IComponentData
{

}

public struct ErrorTag : IComponentData
{

}

public struct PerformRockTaskTag : IComponentData
{

}
public struct RockTag : IComponentData
{

}

public struct DestroyRockTag : IComponentData
{

}

public struct TilledSoilTag : IComponentData
{

}

public struct PerformTillTaskTag : IComponentData
{

}

public struct PerformPlantingTaskTag : IComponentData
{

}

