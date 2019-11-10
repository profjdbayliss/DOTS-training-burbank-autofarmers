using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct PlantComponent : IComponentData
{
    public float timeGrown;
}
