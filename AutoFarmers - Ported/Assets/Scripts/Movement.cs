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
   

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
    }

    // This declares a new kind of job, which is a unit of work to do.
    // The job is declared as an IJobForEach<Translation, Rotation>,
    // meaning it will process all entities in the world that have both
    // Translation and Rotation components. Change it to process the component
    // types you want.
    //
    // The job is also tagged with the BurstCompile attribute, which means
    // that the Burst compiler will optimize it for the best performance.
    [RequireComponentTag(typeof(MovingTag))]
    [BurstCompile]
    struct MovementJob : IJobForEachWithEntity<Translation, Rotation, actor_RunTimeComp>
    {
        public EntityCommandBuffer.Concurrent ecb;

        // Add fields here that your job needs to do its work.
        // For example,
        public float deltaTime;
        public float2 rockPos;
        [ReadOnly] public NativeHashMap<int, int> grid;



        public void Execute(Entity entity, int index, ref Translation translation, [ReadOnly] ref Rotation rotation, ref actor_RunTimeComp actor)
        {
            // Calculate DX and DZ (y represents up, therefore we won't be using that in this case).  
            float dx = actor.targetPos.x - translation.Value.x;
            float dz = actor.targetPos.y - translation.Value.z;
            //the specs state that we want to move in the shortest distance first, therefore, we will perform a check to decide whether x or z is smaller
            //move from there. 
            bool moveXFirst;
            //
            bool headedToRock = false;
            float2 currentPos = new float2(translation.Value.x, translation.Value.z);
            rockPos = GridData.FindTheRock(grid, currentPos, FindMiddlePos(currentPos, actor.targetPos), actor.targetPos,10,10);


            if (rockPos.x != -1)
            {
                actor.targetPos = rockPos;
                Debug.Log("Updated Position to " + rockPos + "Actor is now chasing a rock");
                headedToRock = true;
            }

            Debug.Log(dx);
            Debug.Log(dz);

            // You should only access data that is local or that is a
            // field on this job. Note that the 'rotation' parameter is
            // marked as [ReadOnly], which means it cannot be modified,
            // but allows this job to run in parallel with other jobs
            // that want to read Rotation component data.
            // For example,

            if (Mathf.Abs(actor.targetPos.x) < Mathf.Abs(actor.targetPos.y))
            {
                moveXFirst = true;
            }
            else
            {
                moveXFirst = false;
            }

            if (moveXFirst)
            {

                if ((int)dx != 0)
                {
                    if (dx > 0)
                    {
                        Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x + actor.speed * deltaTime, translation.Value.y, translation.Value.z);
                    }
                    else
                    {
                        Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x - actor.speed * deltaTime, translation.Value.y, translation.Value.z);
                    }
                }
                else if ((int)dz != 0)
                {
                    if (dz < 0)
                    {
                        Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z - actor.speed * deltaTime);
                    }
                    else
                    {
                        Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z + actor.speed * deltaTime);
                    }
                }

            }

            else
            {
                if ((int)dz != 0)
                {
                    if (dz > 0)
                    {
                        Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z + actor.speed * deltaTime);
                    }
                    else
                    {
                        Debug.Log("moved towards dx");
                        translation.Value = new float3(translation.Value.x, translation.Value.y, translation.Value.z - actor.speed * deltaTime);
                    }
                }
                else if ((int)dx != 0)
                {
                    if (dx > 0)
                    {
                        Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x + actor.speed * deltaTime, translation.Value.y, translation.Value.z);
                    }
                    else
                    {
                        Debug.Log("moved towards dz");
                        translation.Value = new float3(translation.Value.x - actor.speed * deltaTime, translation.Value.y, translation.Value.z);

                    }

                }
                else
                {
                    Debug.Log("At destination");
                    ecb.RemoveComponent<MovingTag>(index, entity);
                    
                    if (headedToRock)
                    {
                        ecb.AddComponent<PerformRockTaskTag>(index, entity);
                        headedToRock = false;
                    }
                    else
                    {
                        ecb.AddComponent<NeedsTaskTag>(index, entity);
                        headedToRock = false;
                    }
                }


            }
        }

        public float2 FindMiddlePos(float2 currentPos, float2 targetPos)
        {
            float2 middlePos = new float2();

            var dx = targetPos.x-currentPos.x;
            var dz = targetPos.y-currentPos.y;

            if (dx < dz)
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
        job.grid = GridData.gridStatus;
        

        // Now that the job is set up, schedule it to be run. 
        return job.Schedule(this, inputDependencies);
    }
}