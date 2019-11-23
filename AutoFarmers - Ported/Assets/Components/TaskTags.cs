using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// This temporarily exists so that the command buffer won't throw errors
// when tags are set as it seems to commonly do so
public struct TagData
{
    public Entity entity;
    public int type;
}

public enum TagTypes { MovingTag = 0, PerformTaskTag = 1, NeedsTaskTag = 2 };

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
