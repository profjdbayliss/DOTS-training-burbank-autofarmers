using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class Spawner : MonoBehaviour
{

    public GameObject Prefab;
    public int CountX = 2;
    public int CountY = 2;

    void Start()
    {
        // Create entity prefab from the game object hierarchy once
        Entity prefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(Prefab, World.Active);
        var entityManager = World.Active.EntityManager;

        // Efficiently instantiate a bunch of entities from the already converted entity prefab
        var instance = entityManager.Instantiate(prefab);

        // Place the instantiated entity in a grid with some noise
        var position = transform.TransformPoint(new float3(5, 0, 5));
        //var position = transform.TransformPoint(new float3(0,0,0));
        entityManager.SetComponentData(instance, new Translation() { Value = position });
        var data = new actor_RunTimeComp { startPos = new float2(8,8), speed = 2, targetPos = new float2(8,8) };
        entityManager.SetComponentData(instance, data);
        // give his first command based on the 1's in the hash
        entityManager.AddComponentData(instance, new MovingTag());
    }
}

