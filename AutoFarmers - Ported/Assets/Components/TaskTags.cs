using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


// would like to use this for catching general errors that shouldn't happen
[Serializable]
public struct ErrorTag : IComponentData
{

}

[Serializable]
public struct NeedsTaskTag : IComponentData
{
    
}

[Serializable]
public struct MovingTag : IComponentData
{

}

[Serializable]
public struct PerformTaskTag : IComponentData
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
public struct PlantTag : IComponentData
{

}

[Serializable]
public struct DroneTag : IComponentData
{

}

[Serializable]
public struct FarmerTag : IComponentData
{

}
