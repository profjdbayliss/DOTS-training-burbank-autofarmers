using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class HarvestSystem : JobComponentSystem
{
    //static Unity.Mathematics.Random rand;

    private EntityCommandBufferSystem ecbs;
	private EntityQuery plantQuery;

	protected override void OnCreate()
	{
        //rand = new Unity.Mathematics.Random(42);
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
		plantQuery = GetEntityQuery(new EntityQueryDesc
		{
			All = new[] { ComponentType.ReadOnly<PlantTag>(), typeof(Translation) },

		});
	}

	[BurstCompile]
	[RequireComponentTag(typeof(PerformHarvestTaskTag))]
	struct HarvestSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
	{
		public EntityCommandBuffer.Concurrent ecb;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> plantLocations;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> plantEntities;
		public int plantCount;
        public float2 targetStore;

		public NativeHashMap<int, int>.ParallelWriter grid;
		[ReadOnly] public Entity plantEntity;

		public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref actor_RunTimeComp movementComponent)
		{
            Debug.Log("trying to harvest");
            //float plantingHeight = 0.25f;
            if (
			grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
			GridData.ConvertDataValue(2, 0)))
			{

				ecb.RemoveComponent(index, entity, typeof(PerformHarvestTaskTag));
				Debug.Log("harvest system called");

				//Loop through and find the plant
				for (int i = 0; i < plantCount; i++)
				{
					if ((int)plantLocations[i].Value.x == (int)translation.Value.x &&
					(int)plantLocations[i].Value.z == (int)translation.Value.z)
					{
                        //Debug.Log("found plant with location: " + translation.Value.x + " " + translation.Value.z);
                        // Search for a store

                        // Set the plants 
                        ecb.AddComponent(i, plantEntities[i], typeof(actor_RunTimeComp));
                        ecb.SetComponent(i, plantEntities[i], new actor_RunTimeComp {
							intent = 11, speed = 5,
							startPos = new Unity.Mathematics.float2(plantLocations[i].Value.x, plantLocations[i].Value.z),
							targetPos = targetStore
						});
						ecb.AddComponent(i, plantEntities[i], typeof(MovingTag));

                        //farmer
						ecb.SetComponent(index, entity, new actor_RunTimeComp{
							intent = 11, speed = 5,
							startPos = new Unity.Mathematics.float2(plantLocations[i].Value.x, plantLocations[i].Value.z),
							targetPos = targetStore
						});
						ecb.AddComponent(index, entity, typeof(MovingTag));

					}
				}
			}
		

			else
			{
				Debug.Log("did not add to grid");
				ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
				ecb.RemoveComponent(index, entity, typeof(PerformHarvestTaskTag));
			}


		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
        //int nextX = System.Math.Abs(rand.NextInt()) % (GridData.width);
        //int nextZ = System.Math.Abs(rand.NextInt()) % (GridData.width);
        int nextX =  (GridData.width/2);
        int nextZ = (GridData.width/2);

        var job = new HarvestSystemJob
		{
			ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
			plantLocations = plantQuery.ToComponentDataArray<Translation>(Allocator.TempJob),
			plantEntities = plantQuery.ToEntityArray(Allocator.TempJob),
			plantCount = plantQuery.CalculateEntityCount(),
            targetStore = GridData.Search(GridData.gridStatus, new float2(nextX, nextZ), 50, 4, GridData.width, GridData.width),

        // plantEntity = GridDataInitialization.plantEntity,
        grid = GridData.gridStatus.AsParallelWriter()
		}.Schedule(this, inputDependencies);
		job.Complete();
        Debug.Log("scheduled harvest job");
		return job; // job.Schedule(this, inputDependencies);
	}
}