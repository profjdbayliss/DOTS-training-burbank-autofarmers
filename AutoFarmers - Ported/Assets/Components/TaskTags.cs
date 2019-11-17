using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


[Serializable]
public struct NeedsTaskTag : IComponentData
{
    
}

[Serializable]
public struct MovingTag : IComponentData
{

}

// would like to use this for catching general errors that shouldn't happen
[Serializable]
public struct ErrorTag : IComponentData
{

}

[Serializable]
public struct PerformRockTaskTag : IComponentData
{

}
[Serializable]
public struct RockTag : IComponentData
{

}

[Serializable]
public struct TilledSoilTag : IComponentData
{

}

[Serializable]
public struct PerformTillTaskTag : IComponentData
{

}

[Serializable]
public struct PerformPlantingTaskTag : IComponentData
{

}

[Serializable]
public struct PlantTag : IComponentData
{

}

[Serializable]
public struct PerformHarvestTaskTag : IComponentData
{

}