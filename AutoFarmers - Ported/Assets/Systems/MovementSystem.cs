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
    private static PerformRockTaskTag data = new PerformRockTaskTag();


    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
    }

    [RequireComponentTag(typeof(MovingTag))]
    [BurstCompile]
    public struct MovementJob : IJobForEachWithEntity<Translation, Rotation, MovementComponent, IntentionComponent>

    {
        public EntityCommandBuffer.Concurrent ecb;
        public float deltaTime;
        public float2 rockPos;
        public enum Intentions : int { None = 0, Rock = 1, Till = 2, Plant = 3, Store = 4, PerformRock = 5, PerformTill = 6, PerformPlanting = 7, MovingToStore = 11, MovingToHarvest = 12, PerformHarvest = 13 };



        public void Execute(Entity entity, int index, ref Translation translation, [ReadOnly] ref Rotation rotation, ref MovementComponent actor, ref IntentionComponent intent)
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
                        var data = new MovementComponent { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (intent.intent == (int)Intentions.Rock)
                    {
                        //Debug.Log("performing rock1 now");
                        var data = new IntentionComponent {  intent = (int)Intentions.PerformRock };

                        ecb.SetComponent(index, entity, data);

                        ecb.AddComponent(index, entity, typeof(PerformRockTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));

                    }
                    else if (intent.intent == (int)Intentions.Till)
                    {
                        var data = new IntentionComponent { intent = (int)Intentions.PerformTill }; 
                        ecb.SetComponent(index, entity, data);

                        ecb.AddComponent(index, entity, typeof(PerformTillTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (intent.intent == (int)Intentions.Plant)
                    {
                        var data = new IntentionComponent { intent = (int)Intentions.PerformPlanting };
                        ecb.SetComponent(index, entity, data);
                        //Debug.Log("Performing plant");
                        ecb.AddComponent(index, entity, typeof(PerformPlantingTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (intent.intent == (int)Intentions.Store)
                    {
                        Debug.Log("moving to harvest");
                        var data = new IntentionComponent { intent = (int)Intentions.MovingToHarvest };
                        //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (intent.intent == (int)Intentions.PerformHarvest)
                    {
                        //Debug.Log("moving to plant");
                        var data = new IntentionComponent { intent = (int)Intentions.MovingToHarvest };
                        //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                        ecb.SetComponent(index, entity, data);
                    }
                    else
                    {
                        ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
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
                        var data = new MovementComponent { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);
                    }
                    //               else if (actor.intent == (int)Intentions.MovingToHarvest)
                    //               {
                    //                   var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformHarvest, middlePos = new float2(-1, -1) };
                    //                   ecb.SetComponent(index, entity, data);
                    //                   //Debug.Log("performing harvest next");
                    //                   ecb.AddComponent(index, entity, typeof(PerformHarvestTaskTag));
                    //                   ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    //               }
                    else if (intent.intent == (int)Intentions.Store)
                    {
                        //Debug.Log("moving to harvest");
                        var data = new IntentionComponent { intent = (int)Intentions.MovingToHarvest };
                        //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (intent.intent == (int)Intentions.PerformHarvest)
                    {
                        //Debug.Log("moving to plant");
                        var data = new IntentionComponent { intent = (int)Intentions.MovingToHarvest };
                        //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (intent.intent == (int)Intentions.Rock)
                    {
                        //Debug.Log("performing rock now");
                        var data = new IntentionComponent { intent = (int)Intentions.PerformRock };
                        ecb.SetComponent(index, entity, data);
                        ecb.AddComponent<PerformRockTaskTag>(index, entity);
                        ecb.RemoveComponent<MovingTag>(index, entity);
                    }
                    else if (intent.intent == (int)Intentions.Till)
                    {
                        var data = new IntentionComponent { intent = (int)Intentions.PerformTill };
                        ecb.SetComponent(index, entity, data);

                        ecb.AddComponent(index, entity, typeof(PerformTillTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (intent.intent == (int)Intentions.Plant)
                    {
                        var data = new IntentionComponent { intent = (int)Intentions.PerformPlanting };
                        ecb.SetComponent(index, entity, data);
                        //Debug.Log("Performing plant");
                        ecb.AddComponent(index, entity, typeof(PerformPlantingTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else
                    {
                        ecb.AddComponent<NeedsTaskTag>(index, entity);
                        ecb.RemoveComponent<MovingTag>(index, entity);
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


        // Now that the job is set up, schedule it to be run. 
        return job.Schedule(this, inputDependencies);
    }
}