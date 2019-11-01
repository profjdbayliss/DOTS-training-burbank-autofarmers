using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class TillSystem : JobComponentSystem
{
	private EntityCommandBufferSystem ecbs;

	protected override void OnCreate()
	{
		ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
	}

	[BurstCompile]
    [RequireComponentTag(typeof(PerformTillTaskTag))]
    struct TillSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
    {
		public EntityCommandBuffer.Concurrent ecb;
        public NativeHashMap<int, int>.ParallelWriter grid;
        [ReadOnly]public Entity tilledSoil;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref actor_RunTimeComp movementComponent)
		{
            float tillBlockHeight = 0.25f;
            if (
            grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
            GridData.ConvertDataValue(2, 0)))
            {
                float3 pos = new float3((int)translation.Value.x, tillBlockHeight, (int)translation.Value.z);

            var instance = ecb.Instantiate(index, tilledSoil);
            ecb.SetComponent(index, instance, new Translation { Value = pos });
            ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
		    ecb.RemoveComponent(index, entity, typeof(PerformTillTaskTag));
                //Debug.Log("added grid tilling");
            } else
            {
                //Debug.Log("did not add to grid");
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                ecb.RemoveComponent(index, entity, typeof(PerformTillTaskTag));
            }

           
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDependencies)
	{
        var job = new TillSystemJob
        {
            ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
            tilledSoil = GridDataInitialization.tilledTileEntity,
            grid = GridData.gridStatus.AsParallelWriter()
            }.Schedule(this, inputDependencies);
        job.Complete();

        return job; // job.Schedule(this, inputDependencies);
	}
}