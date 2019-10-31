using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

public class TestCubeJob : JobComponentSystem
{
    int total = 0;
    // This declares a new kind of job, which is a unit of work to do.
    // The job is declared as an IJobForEach<Translation, Rotation>,
    // meaning it will process all entities in the world that have both
    // Translation and Rotation components. Change it to process the component
    // types you want.
    //
    // The job is also tagged with the BurstCompile attribute, which means
    // that the Burst compiler will optimize it for the best performance.
    [BurstCompile]
    struct TestCubeJobJob : IJobForEach<TestCubeTag, Translation>
    {
        // Add fields here that your job needs to do its work.
        // For example,
        //    public float deltaTime;
        [ReadOnly] public NativeHashMap<int, int> gridHashMap;


        public void Execute([ReadOnly] ref TestCubeTag tag, ref Translation pos)
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    int value;
                    gridHashMap.TryGetValue(GridData.ConvertToHash(i,j), out value);
                    if (value == 1)
                    {
                        float3 dif =pos.Value - new float3(i, 0, j);
                        pos.Value += dif;
                       
                    }
                }
            }
            
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new TestCubeJobJob();

        // Assign values to the fields on your job here, so that it has
        // everything it needs to do its work when it runs later.
        // For example,
        //     job.deltaTime = UnityEngine.Time.deltaTime;
         job.gridHashMap =   GridData.gridStatus;
         // Now that the job is set up, schedule it to be run. 
        return job.Schedule(this, inputDependencies);
    }
}