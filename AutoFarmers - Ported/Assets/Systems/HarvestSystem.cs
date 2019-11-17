﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class HarvestSystem : JobComponentSystem
{
    private EntityCommandBufferSystem ecbs;
	private EntityQuery plantQuery;

	protected override void OnCreate()
	{
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
		plantQuery = GetEntityQuery(new EntityQueryDesc
		{
			All = new[] { ComponentType.ReadOnly<PlantTag>(), typeof(Translation) },

		});

	}

	[BurstCompile]
	[RequireComponentTag(typeof(PerformHarvestTaskTag))]
	struct HarvestSystemJob : IJobForEachWithEntity<Translation, MovementComponent, IntentionComponent>
	{
		public EntityCommandBuffer.Concurrent ecb;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> plantLocations;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> plantEntities;
		public int plantCount;
        public float2 targetStore;

		public NativeHashMap<int, EntityInfo>.ParallelWriter grid;
		[ReadOnly] public Entity plantEntity;

		public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref MovementComponent movementComponent, ref IntentionComponent intent)
		{
            // Debug.Log("trying to harvest");
            //float plantingHeight = 0.25f;
            EntityInfo harvestInfo = new EntityInfo { type = (int)Tiles.Till };
            if (
			grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
			harvestInfo))
			{

				ecb.RemoveComponent(index, entity, typeof(PerformHarvestTaskTag));
				//Debug.Log("harvest system called");

				//Loop through and find the plant
				for (int i = 0; i < plantCount; i++)
				{
					if ((int)plantLocations[i].Value.x == (int)translation.Value.x &&
					(int)plantLocations[i].Value.z == (int)translation.Value.z)
					{
                        //Debug.Log("found plant with location: " + translation.Value.x + " " + translation.Value.z);
                        // Search for a store

                        // Set the plants 
                        ecb.AddComponent(i, plantEntities[i], typeof(MovementComponent));
                        ecb.SetComponent(i, plantEntities[i], new MovementComponent
                        {
							speed = 5,
							startPos = new Unity.Mathematics.float2(plantLocations[i].Value.x, plantLocations[i].Value.z),
							targetPos = targetStore
						});
                        ecb.SetComponent(i, plantEntities[i], new IntentionComponent { intent = 11 });
						ecb.AddComponent(i, plantEntities[i], typeof(MovingTag));

                        //farmer
						ecb.SetComponent(index, entity, new MovementComponent
                        {
							speed = 5,
							startPos = new Unity.Mathematics.float2(plantLocations[i].Value.x, plantLocations[i].Value.z),
							targetPos = targetStore
						});
                        ecb.SetComponent(i, plantEntities[i], new IntentionComponent { intent = 11 });
                        ecb.AddComponent(index, entity, typeof(MovingTag));

					}
				}
			}
		

			else
			{
				//Debug.Log("did not add to grid");
				ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
				ecb.RemoveComponent(index, entity, typeof(PerformHarvestTaskTag));
			}


		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
        //int nextX = System.Math.Abs(rand.NextInt()) % (GridData.width);
        //int nextZ = System.Math.Abs(rand.NextInt()) % (GridData.width);
        GridData data = GridData.GetInstance();
        int nextX =  (data.width/2);
        int nextZ = (data.width/2);

        var job = new HarvestSystemJob
        {
            ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
            plantLocations = plantQuery.ToComponentDataArray<Translation>(Allocator.TempJob),
            plantEntities = plantQuery.ToEntityArray(Allocator.TempJob),
            plantCount = plantQuery.CalculateEntityCount(),
            targetStore = GridData.Search(data.gridStatus, new float2(nextX, nextZ), 50,
            (int)Tiles.Store, data.width, data.width),
            // plantEntity = GridDataInitialization.plantEntity,
            grid = data.gridStatus.AsParallelWriter()
        };
        var jobHandle = job.ScheduleSingle(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);

		return jobHandle;
	}
}