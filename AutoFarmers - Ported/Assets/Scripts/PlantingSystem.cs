using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class PlantingSystem : JobComponentSystem
{
	private EntityCommandBufferSystem ecbs;

	protected override void OnCreate()
	{
		ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
	}

	[BurstCompile]
	[RequireComponentTag(typeof(PerformPlantingTaskTag))]
	struct PlantingSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
	{
		public EntityCommandBuffer.Concurrent ecb;
		public NativeHashMap<int, int>.ParallelWriter grid;
		[ReadOnly] public Entity plantEntity;

		public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref actor_RunTimeComp movementComponent)
		{
			float plantingHeight = 0f;
           
            if (
			grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
			GridData.ConvertDataValue(3, 0)))
			{
				float3 pos = new float3((int)translation.Value.x, plantingHeight, (int)translation.Value.z);

				var instance = ecb.Instantiate(index, plantEntity);
				ecb.SetComponent(index, instance, new Translation { Value = pos });
				ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
				ecb.RemoveComponent(index, entity, typeof(PerformPlantingTaskTag));
				//Debug.Log("added grid plant");
			}
			else
			{
				//Debug.Log("did not add to plant");
				ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
				ecb.RemoveComponent(index, entity, typeof(PerformPlantingTaskTag));
			}


		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
		var job = new PlantingSystemJob
		{
			ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
			plantEntity = GridDataInitialization.plantEntity,
			grid = GridData.gridStatus.AsParallelWriter()
		}.Schedule(this, inputDependencies);
		job.Complete();

		return job; // job.Schedule(this, inputDependencies);
	}
}