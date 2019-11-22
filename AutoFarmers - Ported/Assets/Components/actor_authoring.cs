using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class actor_authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    //position that the object is at at time of "pathfinding" 
    public float2 startPos;
    //object destination
    public float2 targetPos;
    //how fast we goin?
    public float speed;
    // intention of actor data
    public int intent;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // Call methods on 'dstManager' to create runtime components on 'entity' here. Remember that:
        var data = new MovementComponent { startPos = startPos, speed = speed, targetPos = targetPos };
        dstManager.AddComponentData(entity,data);        
    }
}
