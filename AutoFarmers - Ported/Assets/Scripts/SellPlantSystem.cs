using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class SellPlantSystem : JobComponentSystem
{
	private EntityCommandBufferSystem ecbs;

	protected override void OnCreate()
	{
		ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
	}

	[BurstCompile]
	[RequireComponentTag(typeof(PlantTag))]
	struct SellPlantSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
	{
		public EntityCommandBuffer.Concurrent ecb;
		[ReadOnly] public Entity farmer;
        
		public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref actor_RunTimeComp actor)
		{
            float tolerance = 0.25f;
			if (Mathf.Abs(translation.Value.x - actor.targetPos.x) < tolerance &&
			    Mathf.Abs(translation.Value.y - actor.targetPos.y) < tolerance
					    )
			{
				float farmerSpawnHeight = 0.25f;
				float3 pos = new float3((int)translation.Value.x, farmerSpawnHeight, (int)translation.Value.z);
				// create a farmer
				var instance = ecb.Instantiate(index, farmer);
				ecb.SetComponent(index, instance, new Translation { Value = pos });
				ecb.AddComponent(index, instance, typeof(NeedsTaskTag));
				ecb.AddComponent(index, instance, new actor_RunTimeComp
				{
					speed = 5,
					intent = 0,
				});

				// kill this plant
				ecb.DestroyEntity(index, entity);
				Debug.Log("added farmer & destroyed Plant");
			}
			else
			{
				Debug.Log("not at target location");
				return;
			}


		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
		var job = new SellPlantSystemJob
		{
			ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
			farmer = Spawner.farmerEntity,
		}.Schedule(this, inputDependencies);
		job.Complete();

		return job; // job.Schedule(this, inputDependencies);
	}
}