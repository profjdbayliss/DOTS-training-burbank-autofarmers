using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class actor_authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    //position that the object is at at time of "pathfinding" 
    public Vector2 startPos;
    //object destination
    public Vector2 targetPos;
    //how fast we goin?
    public float speed;

	public int intent;

    
    

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // Call methods on 'dstManager' to create runtime components on 'entity' here. Remember that:
        var data = new actor_RunTimeComp { startPos = startPos, speed = speed, targetPos = targetPos, intent = intent };
           dstManager.AddComponentData(entity,data);
        
        
    }
}
