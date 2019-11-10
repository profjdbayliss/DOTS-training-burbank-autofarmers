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
    private static NativeArray<float2> tillChanges;
    private NativeArray<int> hasChanged; // variable to let me know when something changes the tillChanges array

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();

        // this is basically just a bool that gets passed by reference from
        // being in an array to the job system
        hasChanged = new NativeArray<int>(1, Allocator.Persistent);
        hasChanged[0] = 0;
    }

    public static void InitializeTillSystem (int maxFarmers)
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        } else
        {
            tillChanges = new NativeArray<float2>(maxFarmers, Allocator.Persistent);

            for (int i = 0; i < maxFarmers; i++)
            {
                tillChanges[i] = new float2(-1, -1);
            }
        }

    }

    protected override void OnDestroy()
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }
    }

    [BurstCompile]
    [RequireComponentTag(typeof(PerformTillTaskTag))]
    struct TillSystemJob : IJobForEachWithEntity<Translation, actor_RunTimeComp>
    {
        public EntityCommandBuffer.Concurrent ecb;
        public NativeHashMap<int, int>.ParallelWriter grid;
        public NativeArray<float2> changes;
        public NativeArray<int> hasChanged;
        [ReadOnly] public Entity tilledSoil;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref actor_RunTimeComp movementComponent)
        {
            float tillBlockHeight = 0.25f;
            if (
            grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
            GridData.ConvertDataValue(2, 0)))
            {
                float3 pos = new float3((int)translation.Value.x, tillBlockHeight, (int)translation.Value.z);

                changes[index] = new float2((int)pos.x, (int)pos.z);
                hasChanged[0] = 1;

                //var instance = ecb.Instantiate(index, tilledSoil);
                //ecb.SetComponent(index, instance, new Translation { Value = pos });
                //Debug.Log("added grid tilling");
            }
            //else
            //{
            //    //Debug.Log("did not add to grid");
            //    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            //    ecb.RemoveComponent(index, entity, typeof(PerformTillTaskTag));
            //}
            ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            ecb.RemoveComponent(index, entity, typeof(PerformTillTaskTag));
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();
        var job = new TillSystemJob
        {
            ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
            tilledSoil = GridDataInitialization.tilledTileEntity,
            changes = tillChanges,
            hasChanged = this.hasChanged,
            grid = data.gridStatus.AsParallelWriter()
        }.Schedule(this, inputDependencies);
        job.Complete();

        if (hasChanged[0] == 1)
        {
            for (int i = 0; i < GridDataInitialization.MaxFarmers; i++)
            {
                float2 pos = tillChanges[i];
                if ((int)pos.x != -1 && (int)pos.y != -1)
                {
                    // set the uv's on the mesh
                    // NOTE: setting pos to be a specific number is helpful for testing
                    Mesh tmp = GridDataInitialization.getMesh((int)pos.x, (int)pos.y,
                        GridDataInitialization.BoardWidth);
                    int width = GridDataInitialization.getMeshWidth(tmp, (int)pos.x,
                        (int)pos.y, GridDataInitialization.BoardWidth);
                    //Debug.Log("changing uv at! " + pos + " " + width );
                   
                    Vector2[] uv = tmp.uv;
                    TextureUV tex = GridDataInitialization.textures[(int)GridDataInitialization.BoardTypes.TilledDirt];
                    int uvStartIndex = (GridDataInitialization.getPosForMesh((int)pos.y) +
                        width * 
                        GridDataInitialization.getPosForMesh((int)pos.x)) * 4;
                    uv[uvStartIndex] = new float2(tex.pixelStartX,
                        tex.pixelStartY);
                    uv[uvStartIndex + 1] = new float2(tex.pixelStartX,
                        tex.pixelEndY);
                    uv[uvStartIndex + 2] = new float2(tex.pixelEndX,
                        tex.pixelEndY);
                    uv[uvStartIndex + 3] = new float2(tex.pixelEndX,
                        tex.pixelStartY);
                    // then set the pos back to -1's
                    tillChanges[i] = new float2(-1, -1);
                    tmp.SetUVs(0, uv);
                    tmp.MarkModified();
                }
                hasChanged[0] = 0;
            }


        }

        return job; // job.Schedule(this, inputDependencies);
    }
}