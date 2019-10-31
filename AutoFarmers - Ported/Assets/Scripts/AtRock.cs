using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct AtRock : IComponentData
{
    float2 rockLocation;

}
