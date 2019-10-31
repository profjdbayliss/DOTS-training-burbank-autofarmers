using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

public class SearchSystem : JobComponentSystem
{
	public EntityCommandBufferSystem ecbs;

	protected override void OnCreate()
	{
		ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
	}

    [BurstCompile]
    [RequireComponentTag(typeof(NeedsTaskTag))]
    struct SearchSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
    {
        public EntityCommandBuffer.Concurrent ecb;
        [ReadOnly] public NativeHashMap<int, int> gridHashMap;


        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref actor_RunTimeComp movementComponent)
        {
            float2 pos = new float2(translation.Value.x, translation.Value.x);
            float2 foundLocation = GridData.Search(gridHashMap, pos, 10, 1, 10, 10);
            if (foundLocation.x != -1 && foundLocation.y != -1)
            {
                //actor.targetPosition = foundLocation;
                var data = new actor_RunTimeComp { startPos = pos, speed = 2, targetPos = foundLocation };
                movementComponent = data;
                //entityManager.SetComponentData(instance, data);
                ecb.RemoveComponent<NeedsTaskTag>(index, entity);
                ecb.AddComponent<MovingTag>(index, entity);
            } else
            {
                //TODO: Get a new task!
            }
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new SearchSystemJob();
        job.gridHashMap = GridData.gridStatus;

		job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();

        var jobHandle = job.ScheduleSingle(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);
        return jobHandle;
    }
}