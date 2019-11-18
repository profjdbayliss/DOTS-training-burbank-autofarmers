using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


// tag for the grid
[Serializable]
public struct GridBoard : IComponentData
{

}

// information for what's on a tile on the grid
[Serializable]
public struct EntityInfo : IComponentData
{
    public Entity specificEntity;
    public short type;
}

// tile types
[Serializable]
public enum Tiles : int { None = 0, Rock = 1, Till = 2, Plant = 3, Harvest=4, Store = 5,};

