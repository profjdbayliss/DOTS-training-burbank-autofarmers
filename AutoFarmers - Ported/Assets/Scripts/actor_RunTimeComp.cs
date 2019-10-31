using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct actor_RunTimeComp : IComponentData
{
    public float2 targetPos;
    public float2 startPos; // WORK - delete this!
    public float speed;
    public int intent;
}
