using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct PlantComponent : IComponentData
{
    public float timeGrown;
    public Entity farmerToFollow;
    public int state;
    public int reserveIndex;
}

public enum PlantState { None = 0, Growing = 1, Following = 2, Deleted=3 };

