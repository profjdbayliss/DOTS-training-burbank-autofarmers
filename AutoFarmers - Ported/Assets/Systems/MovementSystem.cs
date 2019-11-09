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

	public struct MovementJob : IJobForEachWithEntity<Translation, Rotation, actor_RunTimeComp>

	{
		public EntityCommandBuffer.Concurrent ecb;

		// Add fields here that your job needs to do its work.
		// For example,
		public float deltaTime;
		public float2 rockPos;
		[ReadOnly] public NativeHashMap<int, int> grid;
        public enum Intentions : int { None = 0, Rock = 1, Till = 2, Plant=3, Store = 4, MoveToRock = 5, PerformRock = 6, MoveToTill = 7, PerformTill = 8, MoveToPlant=9, PerformPlanting = 10, MovingToStore = 11, MovingToHarvest=12, PerformHarvest=13 };



        public void Execute(Entity entity, int index, ref Translation translation, [ReadOnly] ref Rotation rotation, ref actor_RunTimeComp actor)
		{
            float tolerance = 0.2f;

			// Calculate DX and DZ (y represents up, therefore we won't be using that in this case).  
			float dx = actor.targetPos.x - translation.Value.x;
			float dz = actor.targetPos.y - translation.Value.z;

            if ((int)actor.middlePos.x != -1 && (int)actor.middlePos.y != -1)
            {
                //Debug.Log("targetting middle" + actor.middlePos);
                // our target is the middle pos before the target 
                dx = actor.middlePos.x - translation.Value.x;
                dz = actor.middlePos.y - translation.Value.z;
            }

			//the specs state that we want to move in the shortest distance first, therefore, we will perform a check to decide whether x or z is smaller
			//move from there. 
			bool moveXFirst;
			//
			//bool headedToRock = false;
			float2 currentPos = new float2(translation.Value.x, translation.Value.z);


            //Debug.Log(dx);
            //Debug.Log(dz);

            // You should only access data that is local or that is a
            // field on this job. Note that the 'rotation' parameter is
            // marked as [ReadOnly], which means it cannot be modified,
            // but allows this job to run in parallel with other jobs
            // that want to read Rotation component data.
            // For example,

            //Debug.Log("goal an target : " + actor.intent + " " + actor.targetPos.x + " " + actor.targetPos.y);
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
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = actor.intent, middlePos = new float2(-1,-1) };
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (actor.intent == (int)Intentions.MoveToRock)
                    {
                        // need to figure out right place to put this
                        // maybe at top of the file?
                        // double check we haven't redone our path
                        //var rockPos = GridData.FindTheRock(gridHashMap, currentPos, MovementJob.FindMiddlePos(currentPos, actor.targetPos), actor.targetPos, gridSize, gridSize);

                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformRock, middlePos = new float2(-1, -1) };

                        ecb.SetComponent(index, entity, data);

                        ecb.AddComponent(index, entity, typeof(PerformRockTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (actor.intent == (int)Intentions.MoveToTill)
                    {
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformTill, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);

                        ecb.AddComponent(index, entity, typeof(PerformTillTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (actor.intent == (int)Intentions.MoveToPlant)
                    {
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformPlanting, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);
                        //Debug.Log("Performing plant");
                        ecb.AddComponent(index, entity, typeof(PerformPlantingTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (actor.intent == (int)Intentions.MovingToHarvest)
                    {
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformHarvest, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);
                        //Debug.Log("moving to harvest plant1");
                        ecb.AddComponent(index, entity, typeof(PerformHarvestTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else
                    {
                        if (actor.intent == (int)Intentions.Rock)
                        {
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MoveToRock, middlePos = new float2(-1, -1) };
                            //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                            ecb.SetComponent(index, entity, data);
                        } else if (actor.intent == (int)Intentions.Till)
                        {
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MoveToTill, middlePos = new float2(-1, -1) };
                            //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                            ecb.SetComponent(index, entity, data);
                        }
                        else if (actor.intent == (int)Intentions.Plant)
                        {
                            //Debug.Log("moving to plant");
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MoveToPlant, middlePos = new float2(-1, -1) };
                            //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                            ecb.SetComponent(index, entity, data);
                        }
                        else if (actor.intent == (int)Intentions.Store)
                        {
                            Debug.Log("moving to harvest");
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MovingToHarvest, middlePos = new float2(-1, -1) };
                            //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                            ecb.SetComponent(index, entity, data);
                        }
                        else if (actor.intent == (int)Intentions.PerformHarvest)
                        {
                            //Debug.Log("moving to plant");
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MovingToHarvest, middlePos = new float2(-1, -1) };
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
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = actor.intent, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (actor.intent == (int)Intentions.MoveToRock)
					{
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformRock, middlePos = new float2(-1, -1) };

                        ecb.SetComponent(index, entity, data);
						ecb.AddComponent<PerformRockTaskTag>(index, entity);
                        ecb.RemoveComponent<MovingTag>(index, entity);
                    }
                    else if (actor.intent == (int)Intentions.MoveToTill)
                    {
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformTill, middlePos = new float2(-1, -1) };

                        ecb.SetComponent(index, entity, data);
                        ecb.AddComponent<PerformTillTaskTag>(index, entity);
                        ecb.RemoveComponent<MovingTag>(index, entity);
                    }
                    else if (actor.intent == (int)Intentions.MoveToPlant)
                    {
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformPlanting, middlePos = new float2(-1, -1) };

                        ecb.SetComponent(index, entity, data);
                        ecb.AddComponent<PerformPlantingTaskTag>(index, entity);
                        ecb.RemoveComponent<MovingTag>(index, entity);
                    }
                    else if (actor.intent == (int)Intentions.MovingToHarvest)
                    {
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.PerformHarvest, middlePos = new float2(-1, -1) };
                        ecb.SetComponent(index, entity, data);
                        //Debug.Log("performing harvest next");
                        ecb.AddComponent(index, entity, typeof(PerformHarvestTaskTag));
                        ecb.RemoveComponent(index, entity, typeof(MovingTag));
                    }
                    else if (actor.intent == (int)Intentions.Store)
                    {
                        //Debug.Log("moving to harvest");
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MovingToHarvest, middlePos = new float2(-1, -1) };
                        //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                        ecb.SetComponent(index, entity, data);
                    }
                    else if (actor.intent == (int)Intentions.PerformHarvest)
                    {
                        //Debug.Log("moving to plant");
                        var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MovingToHarvest, middlePos = new float2(-1, -1) };
                        //ecb.SetComponent<actor_RunTimeComp>(index, entity, data);
                        ecb.SetComponent(index, entity, data);
                    }
                    else
					{
                        if (actor.intent == (int)Intentions.Rock)
                        {
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MoveToRock, middlePos = new float2(-1, -1) };
                            ecb.SetComponent(index, entity, data);
                        }
                        else if (actor.intent == (int)Intentions.Till)
                        {
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MoveToTill, middlePos = new float2(-1, -1) };
                            ecb.SetComponent(index, entity, data);
                        }
                        else if (actor.intent == (int)Intentions.Plant)
                        {
                            var data = new actor_RunTimeComp { startPos = actor.startPos, speed = actor.speed, targetPos = actor.targetPos, intent = (int)Intentions.MoveToPlant, middlePos = new float2(-1, -1) };
                            ecb.SetComponent(index, entity, data);
                        }
                        else
                        {
                            ecb.AddComponent<NeedsTaskTag>(index, entity);
                            ecb.RemoveComponent<MovingTag>(index, entity);
                        }
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
        GridData data = GridData.GetInstance();

		// Assign values to the fields on your job here, so that it has
		// everything it needs to do its work when it runs later.
		// For example,
		job.deltaTime = Time.deltaTime;
		job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
		job.grid = data.gridStatus;


		// Now that the job is set up, schedule it to be run. 
		return job.Schedule(this, inputDependencies);
	}
}