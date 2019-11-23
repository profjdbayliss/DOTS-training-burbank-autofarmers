using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using UnityEngine;

public class Movement : JobComponentSystem
{
    public float deltaTime;
    public EntityCommandBufferSystem ecbs;
    private static NativeQueue<MovementSetData> movementSetData;
    private static NativeQueue<TagData> addTagData;
    private static NativeQueue<TagData> removeTagData;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        movementSetData = new NativeQueue<MovementSetData>(Allocator.Persistent);
        addTagData = new NativeQueue<TagData>(Allocator.Persistent);
        removeTagData = new NativeQueue<TagData>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (movementSetData.IsCreated)
        {
            movementSetData.Dispose();
        }

        if (addTagData.IsCreated)
        {
            addTagData.Dispose();
        }

        if (removeTagData.IsCreated)
        {
            removeTagData.Dispose();
        }

        base.OnDestroy();

    }
    


    [RequireComponentTag(typeof(MovingTag))]
    [BurstCompile]
    public struct MovementJob : IJobForEachWithEntity<Translation, Rotation, MovementComponent, EntityInfo>

    {
        public EntityCommandBuffer.Concurrent ecb;
        public float deltaTime;
        public float2 rockPos;
        public NativeQueue<MovementSetData>.ParallelWriter movementSet;
        public NativeQueue<TagData>.ParallelWriter addTagSet;
        public NativeQueue<TagData>.ParallelWriter removeTagSet;

        public void Execute(Entity entity, int index, ref Translation translation, [ReadOnly] ref Rotation rotation, ref MovementComponent actor, ref EntityInfo intent)
        {
            float tolerance = 0.2f;

            // Calculate DX and DZ (y represents up, therefore we won't be using that in this case).  
            float dx = actor.targetPos.x - translation.Value.x;
            float dz = actor.targetPos.y - translation.Value.z;

            if ((int)actor.middlePos.x != -1 && (int)actor.middlePos.y != -1)
            {
                //Debug.Log("targeting middle" + actor.middlePos);
                // our target is the middle pos before the target 
                dx = actor.middlePos.x - translation.Value.x;
                dz = actor.middlePos.y - translation.Value.z;
            }

            //the specs state that we want to move in the shortest distance first, therefore, we will perform a check to decide whether x or z is smaller
            //move from there. 
            bool moveXFirst;
            float2 currentPos = new float2(translation.Value.x, translation.Value.z);


            //Debug.Log(dx);
            //Debug.Log(dz);
            //Debug.Log("goal and target : " + actor.intent + " " + actor.targetPos.x + " " + actor.targetPos.y);
            if (Mathf.Abs(dx) <= Mathf.Abs(dz))
            {
                moveXFirst = true;
            }
            else
            {
                moveXFirst = false;
            }

            if (moveXFirst)
            {

                if (Mathf.Abs(dx) > tolerance)
                {
                    if (dx > 0)
                    {
                        //Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x + actor.speed * deltaTime, translation.Value.y, translation.Value.z);
                    }
                    else
                    {
                        //Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x - actor.speed * deltaTime, translation.Value.y, translation.Value.z);
                    }
                }
                else if (Mathf.Abs(dz) > tolerance)
                {
                    if (dz < 0)
                    {
                        //Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z - actor.speed * deltaTime);
                    }
                    else
                    {
                        //Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z + actor.speed * deltaTime);
                    }
                }
                else
                {
                    //Debug.Log("At destination and was I headed to a rock1?: " + actor.intent + " " + actor.targetPos.x + " " + actor.targetPos.y);
                    if ((int)actor.middlePos.x != -1 && (int)actor.middlePos.y != -1)
                    {
                        // we just hit the middle pos and so set things to go to target now
                        var data = new MovementComponent { myType = actor.myType, startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, middlePos = new float2(-1, -1) };
                        //movementSet.Enqueue(new MovementSetData { entity = entity, movementData = data });
                        actor = data;    
                    // FIX: ecb throws errors for setting this component
                        //ecb.SetComponent(index, entity, data);
                    }
                    else if (intent.type == (int)Tiles.Rock || intent.type == (int)Tiles.Till ||
                        intent.type == (int)Tiles.Plant || intent.type == (int)Tiles.Harvest ||
                        intent.type == (int)Tiles.Store)
                    {
                        addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                        removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.MovingTag });
                        // FIX: ecb on tags causes errors
                        //ecb.AddComponent(index, entity, typeof(PerformTaskTag));
                        //ecb.RemoveComponent(index, entity, typeof(MovingTag));

                    }
                    
                   
                    else
                    {
                        addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                        removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.MovingTag });
                        // FIX: ecb causes errors for tags 
                        //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                        //ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }

                }

            }

            else
            {
                if (Mathf.Abs(dz) > tolerance)
                {
                    if (dz > 0)
                    {
                        //Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z + actor.speed * deltaTime);
                    }
                    else
                    {
                        //Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z - actor.speed * deltaTime);
                    }
                }
                else if (Mathf.Abs(dx) > tolerance)
                {
                    if (dx > 0)
                    {
                        //Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x + actor.speed * deltaTime, translation.Value.y, translation.Value.z);
                    }
                    else
                    {
                        //Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x - actor.speed * deltaTime, translation.Value.y, translation.Value.z);

                    }

                }
                else
                {
                    //Debug.Log("At destination and was I headed to a rock?: " + actor.intent);

                    if ((int)actor.middlePos.x != -1 && (int)actor.middlePos.y != -1)
                    {
                        //Debug.Log("just got to the middle" + actor.middlePos);
                        // we just hit the middle pos and so set things to go to target now
                        var data = new MovementComponent { myType = actor.myType, startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, middlePos = new float2(-1, -1) };
                        actor = data;
                        // movementSet.Enqueue(new MovementSetData { entity = entity, movementData = data });
                        // FIX: ecb throws errors for setting this component
                        //ecb.SetComponent(index, entity, data);
                    }
                    else if (intent.type == (int)Tiles.Rock || intent.type == (int)Tiles.Till ||
                       intent.type == (int)Tiles.Plant || intent.type == (int)Tiles.Harvest ||
                       intent.type == (int)Tiles.Store)
                    {
                        addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                        removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.MovingTag });
                        // FIX: ecb causes errors for tags
                        //ecb.AddComponent(index, entity, typeof(PerformTaskTag));
                        //ecb.RemoveComponent(index, entity, typeof(MovingTag));

                    }
                    else
                    {
                        addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                        removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.MovingTag });
                        // FIX: ecb causes errors for tags 
                        //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                        //ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
   
                }


            }
        }

        public static float2 FindMiddlePos(float2 currentPos, float2 targetPos)
        {
            float2 middlePos = new float2();

            var dx = targetPos.x - currentPos.x;
            var dz = targetPos.y - currentPos.y;

            if (Mathf.Abs(dx) <= Mathf.Abs(dz))
            {
                middlePos = new float2(currentPos.x + dx, currentPos.y);
            }
            else
            {
                middlePos = new float2(currentPos.x, currentPos.y + dz);
            }

            return middlePos;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new MovementJob();

        // Assign values to the fields on your job here, so that it has
        // everything it needs to do its work when it runs later.
        // For example,
        job.deltaTime = Time.deltaTime;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.movementSet = movementSetData.AsParallelWriter();
        job.addTagSet = addTagData.AsParallelWriter();
        job.removeTagSet = removeTagData.AsParallelWriter();
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        jobHandle.Complete();

        EntityManager entityManager = World.Active.EntityManager;
        //while (movementSetData.Count > 0)
        //{
        //    MovementSetData data = movementSetData.Dequeue();
        //    entityManager.SetComponentData(data.entity, data.movementData);
        //}
        while (addTagData.Count > 0)
        {
            TagData data = addTagData.Dequeue();
            if (data.type == (int)TagTypes.NeedsTaskTag)
                entityManager.AddComponent(data.entity, typeof(NeedsTaskTag));
            else if (data.type == (int)TagTypes.PerformTaskTag)
                entityManager.AddComponent(data.entity, typeof(PerformTaskTag));
            else if (data.type == (int)TagTypes.MovingTag)
                entityManager.AddComponent(data.entity, typeof(MovingTag));
        }
        while (removeTagData.Count > 0)
        {
            TagData data = removeTagData.Dequeue();
            if (data.type == (int)TagTypes.NeedsTaskTag)
                entityManager.RemoveComponent(data.entity, typeof(NeedsTaskTag));
            else if (data.type == (int)TagTypes.PerformTaskTag)
                entityManager.RemoveComponent(data.entity, typeof(PerformTaskTag));
            else if (data.type == (int)TagTypes.MovingTag)
                entityManager.RemoveComponent(data.entity, typeof(MovingTag));
        }

        // Now that the job is set up, schedule it to be run. 
        return jobHandle;
    }
}