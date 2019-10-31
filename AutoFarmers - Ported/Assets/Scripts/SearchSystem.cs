using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Movement;
using static Unity.Mathematics.math;

public class SearchSystem : JobComponentSystem
{
	public EntityCommandBufferSystem ecbs;
    public NativeArray<int> randomValues;
    public Unity.Mathematics.Random rand;
    const int RANDOM_SIZE = 256;

	protected override void OnCreate()
	{
        rand = new Unity.Mathematics.Random(42);
		ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        randomValues = new NativeArray<int>(RANDOM_SIZE, Allocator.Persistent);
        for (int i=0; i<RANDOM_SIZE; i++)
        {
            randomValues[i] = System.Math.Abs(rand.NextInt());
        }
	}

    [BurstCompile]
    [RequireComponentTag(typeof(NeedsTaskTag))]
    struct SearchSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
    {
        public EntityCommandBuffer.Concurrent ecb;
        [ReadOnly] public NativeHashMap<int, int> gridHashMap;
        [ReadOnly] public NativeArray<int> randArray;
        [ReadOnly] public int nextIndex;

        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref actor_RunTimeComp movementComponent)
        {
            // set new task: should be more complicated
            int taskValue = 1; // (randArray[(nextIndex + index) % randArray.Length] % 4) + 1;
            float2 pos = new float2(translation.Value.x, translation.Value.z);
            float2 foundLocation = GridData.Search(gridHashMap, pos, 20, taskValue, 20, 20);

            var rockPos = GridData.FindTheRock(gridHashMap, pos, MovementJob.FindMiddlePos(pos, movementComponent.targetPos), movementComponent.targetPos, 10, 10);
            if (foundLocation.x != -1 && foundLocation.y != -1 && rockPos.x != -1)
            {
                movementComponent.targetPos = rockPos;
                Debug.Log("Updated Position to " + rockPos + "Actor is now chasing a rock");
                var data = new actor_RunTimeComp { startPos = pos, speed = 2, targetPos = rockPos, intent = 5 };
                movementComponent = data;
                //entityManager.SetComponentData(instance, data);
                ecb.RemoveComponent<NeedsTaskTag>(index, entity);
                ecb.AddComponent<MovingTag>(index, entity);
            }
            else
            {
                if (foundLocation.x != -1 && foundLocation.y != -1)
                {
                    var data = new actor_RunTimeComp { startPos = pos, speed = 2, targetPos = foundLocation, intent = taskValue };
                    movementComponent = data;
                    //entityManager.SetComponentData(instance, data);
                    ecb.RemoveComponent<NeedsTaskTag>(index, entity);
                    ecb.AddComponent<MovingTag>(index, entity);
            
                }
                else
                {
                    //TODO: Get a new task!
                    // right now it's an error though because we should always find things
                    //ecb.AddComponent<ErrorTag>(index, entity);
                    ecb.RemoveComponent<NeedsTaskTag>(index, entity);
                    ecb.AddComponent<MovingTag>(index, entity);
                }
            }
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        int index = System.Math.Abs(rand.NextInt()) % randomValues.Length;
        var job = new SearchSystemJob();
        job.gridHashMap = GridData.gridStatus;
        job.randArray = randomValues;
        job.nextIndex = index;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();

        //Debug.Log("nextInt: " + (randomValues[(index) % randomValues.Length]%4 + 1));
        var jobHandle = job.ScheduleSingle(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);
        return jobHandle;
    }

    protected override void OnDestroy()
    {
        if (randomValues.IsCreated)
        {
            randomValues.Dispose();
        }
        base.OnDestroy();
    }
}