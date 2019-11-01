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
        public NativeHashMap<int, int> gridHashMap;
        [ReadOnly] public NativeArray<int> randArray;
        [ReadOnly] public int nextIndex;
        [ReadOnly] public int gridSize;
        [ReadOnly] public int radiusForSearch;

        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref actor_RunTimeComp movementComponent)
        {
            // set new task: should be more complicated
            int taskValue = 1; // (randArray[(nextIndex + index) % randArray.Length] % 4) + 1;
            float2 pos = new float2(translation.Value.x, translation.Value.z);
            float2 foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, taskValue, gridSize, gridSize);

            var rockPos = GridData.FindTheRock(gridHashMap, pos, MovementJob.FindMiddlePos(pos, movementComponent.targetPos), movementComponent.targetPos, gridSize, gridSize);
            if (foundLocation.x != -1 && foundLocation.y != -1 && rockPos.x != -1)
            {
                rockPos = new float2(rockPos.x + 0.5f, rockPos.y + 0.5f);
                movementComponent.targetPos = rockPos;
                //Debug.Log("Updated Position to " + rockPos + "Actor is now chasing a rock");
                var data = new actor_RunTimeComp { startPos = pos, speed = 2, targetPos = rockPos, intent = 5 };
                movementComponent = data;
                //entityManager.SetComponentData(instance, data);
                ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                ecb.AddComponent(index, entity, typeof(MovingTag));
                int key = GridData.ConvertToHash((int)rockPos.x, (int)rockPos.y);
                gridHashMap.Remove(key);
            }
            else
            {
                if (foundLocation.x != -1 && foundLocation.y != -1)
                {
                    foundLocation = new float2(foundLocation.x + 0.5f, foundLocation.y + 0.5f);
                    var data = new actor_RunTimeComp { startPos = pos, speed = 2, targetPos = foundLocation, intent = taskValue };
                    ecb.SetComponent(index, entity, data);
                    
                    
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.AddComponent(index, entity, typeof(MovingTag));

                    if (taskValue == 1)
                    {
                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        gridHashMap.Remove(key);
                    }

                }
                else
                {
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
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
        job.gridSize = GridData.width;
        job.radiusForSearch = 20;

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