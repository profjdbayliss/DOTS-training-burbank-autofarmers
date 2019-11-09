using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class FindRockSystem : JobComponentSystem
{
	private EntityQuery m_RockQuery;
	private EntityCommandBufferSystem ecbs;

	protected override void OnCreate()
	{
		ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
		m_RockQuery = GetEntityQuery(new EntityQueryDesc
		{
			All = new[] { ComponentType.ReadOnly<RockTag>(), typeof(Translation)},

		});
	}

	[RequireComponentTag(typeof(PerformRockTaskTag))]
	[BurstCompile]
	struct FindRockSystemJob : IJobForEachWithEntity<Translation>
	{
		public EntityCommandBuffer.Concurrent ecb;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<Translation> rockLocations;
		[DeallocateOnJobCompletion][ReadOnly]public NativeArray<Entity> rockEntities;
		public int rockCount;

		public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation)
		{
			
			for (int i = 0; i < rockCount; i++)
			{
                //Debug.Log("rock locations: " + translation.Value.x + " " + translation.Value.z + 
                //    " " +rockLocations[i].Value.x + " " + rockLocations[i].Value.z );
                if ((int)rockLocations[i].Value.x == (int)translation.Value.x &&
				(int)rockLocations[i].Value.z == (int)translation.Value.z)
				{
                    //Debug.Log("destroying a rock with location: " + translation.Value.x + " " + translation.Value.z);
					//ecb.AddComponent(i, rockEntities[i], typeof(DestroyRockTag));
                    ecb.DestroyEntity(i, rockEntities[i]);
                    ecb.RemoveComponent(index, entity, typeof(PerformRockTaskTag));
                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                }
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
		var job = new FindRockSystemJob();
		job.rockCount = m_RockQuery.CalculateEntityCount();
		job.rockLocations = m_RockQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
		job.rockEntities = m_RockQuery.ToEntityArray(Allocator.TempJob);
		job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();


		return job.Schedule(this, inputDependencies);
	}
}