using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
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
			All = new[] { ComponentType.ReadOnly<RockTag>()},
		});
	}

	[RequireComponentTag(typeof(PerformRockTaskTag))]
	[BurstCompile]
	struct FindRockSystemJob : IJobForEachWithEntity<Translation>
	{
		public EntityCommandBuffer.Concurrent ecb;
		public NativeArray<Translation> rockLocations;
		public NativeArray<Entity> rockEntities;
		public int rockCount;

		public void Execute(Entity entity, int index, ref Translation translation)
		{
			
			for (int i = 0; i < rockCount; i++)
			{
			   if (rockLocations[i].Value.x == (int)translation.Value.x &&
				rockLocations[i].Value.z == (int)translation.Value.z)
				{
					ecb.AddComponent<DestroyRockTag>(index, rockEntities[i]);
					ecb.RemoveComponent<PerformRockTaskTag>(index, entity);
				}
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
		var job = new FindRockSystemJob();
		job.rockCount = m_RockQuery.CalculateEntityCount();
		job.rockLocations = m_RockQuery.ToComponentDataArray<Translation>(Allocator.Temp);
		job.rockEntities = m_RockQuery.ToEntityArray(Allocator.Temp);
		job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();


		return job.Schedule(this, inputDependencies);
	}
}