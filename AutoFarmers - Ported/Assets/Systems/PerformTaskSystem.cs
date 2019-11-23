using System.Collections;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class PerformTaskSystem : JobComponentSystem
{
    private EntityCommandBufferSystem ecbs;
    private static NativeQueue<float2> tillChanges;
    private static Store storeInfo;
    private static NativeArray<int> plantsSold;
    private static Unity.Mathematics.Random rand;
    private static NativeQueue<PlantDataSet> plantDataSet;
    private static NativeQueue<TagData> addTagData;
    private static NativeQueue<TagData> removeTagData;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        plantsSold = new NativeArray<int>(1, Allocator.Persistent);
        rand = new Unity.Mathematics.Random(42);

        plantDataSet = new NativeQueue<PlantDataSet>(Allocator.Persistent);
        addTagData = new NativeQueue<TagData>(Allocator.Persistent);
        removeTagData = new NativeQueue<TagData>(Allocator.Persistent);

    }

    public struct PlantDataSet
    {
        public Entity entity;
        public PlantComponent plantData;
    }

    public static void InitializeTillSystem(int maxFarmers)
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }
        else
        {
            tillChanges = new NativeQueue<float2>(Allocator.Persistent);
        }

    }

    protected override void OnDestroy()
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }

        if (plantsSold.IsCreated)
        {
            plantsSold.Dispose();
        }

        if (plantDataSet.IsCreated)
        {
            plantDataSet.Dispose();
        }

        if (addTagData.IsCreated)
        {
            addTagData.Dispose();
        }

        if (removeTagData.IsCreated)
        {
            removeTagData.Dispose();
        }
        base.OnDestroy();
    }

    [RequireComponentTag(typeof(PerformTaskTag))]
    [BurstCompile]
    struct PerformTaskSystemJob : IJobForEachWithEntity<Translation, Rotation, EntityInfo>
    {
        // var's used by multiple tasks
        public EntityCommandBuffer.Concurrent ecb;
        public NativeHashMap<int, EntityInfo>.ParallelWriter grid;

        // var specific to rock tasks:
        [ReadOnly] public ComponentDataFromEntity<RockTag> IsRockType;

        // var's used by till task:
        public NativeQueue<float2>.ParallelWriter changes;
        [ReadOnly] public float MAX_GROWTH;

        // var used by store:
        public NativeArray<int> plantsSold;

        // temporary parallel queues for command buffer changes
        public NativeQueue<PlantDataSet>.ParallelWriter plantDataSet;
        public NativeQueue<TagData>.ParallelWriter addTagSet;
        public NativeQueue<TagData>.ParallelWriter removeTagSet;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref Rotation rotation, ref EntityInfo entityInfo)
        {

            if (entityInfo.type == (int)Tiles.Rock)
            {
                //Debug.Log("destroying rock");
                ecb.DestroyEntity(entityInfo.specificEntity.Index, entityInfo.specificEntity);
                addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                // FIX: ecb fails on tag removal/adding with burst
                //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Till)
            {
                float tillBlockHeight = 0.25f;
                EntityInfo tillInfo = new EntityInfo { type = (int)Tiles.Till };
                if (
                grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
                tillInfo))
                {
                    float3 pos = new float3((int)translation.Value.x, tillBlockHeight, (int)translation.Value.z);

                    changes.Enqueue(new float2((int)pos.x, (int)pos.z));
                }
                // FIX: ecb fails on tag removal/adding with burst
                addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Plant)
            {
                // since the plant needs to be instantiated and then cached
                // into the hash table it's done in the main thread
                // FIX: ecb fails on tag removal/adding with burst
                addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Harvest)
            {
                EntityInfo tillInfo = new EntityInfo { type = (int)Tiles.Till };

                if (grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
                tillInfo))
                {
                    // plant needs to follow the farmer
                    PlantComponent plantInfo = new PlantComponent
                    {
                        timeGrown = MAX_GROWTH,
                        state = (int)PlantState.Following,
                        farmerToFollow = entity
                    };
                    plantDataSet.Enqueue(new PlantDataSet { entity = entityInfo.specificEntity, plantData = plantInfo });
                    // FIX: ecb errors when setting this
                    //ecb.SetComponent(entityInfo.specificEntity.Index,
                    //     entityInfo.specificEntity, plantInfo);
                }
                // FIX: ecb fails on tag removal/adding with burst
                addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            }

            else if (entityInfo.type == (int)Tiles.Store)
            {
                // we need to remove the plant from the farmer
                PlantComponent plantInfo = new PlantComponent
                {
                    timeGrown = MAX_GROWTH,
                    state = (int)PlantState.Deleted
                };
                plantDataSet.Enqueue(new PlantDataSet { entity = entityInfo.specificEntity, plantData = plantInfo });
                // FIX: ecb errors when setting this
                //ecb.SetComponent(entityInfo.specificEntity.Index,
                //     entityInfo.specificEntity, plantInfo);
                // FIX: ecb fails on tag removal/adding with burst
                addTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.NeedsTaskTag });
                removeTagSet.Enqueue(new TagData { entity = entity, type = (int)TagTypes.PerformTaskTag });
                //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));

                // and should actually sell stuff here
                // we just need to know how much was sold from all of them
                unsafe
                {
                    Interlocked.Increment(ref ((int*)plantsSold.GetUnsafePtr())[0]);
                }
               
                
            }

        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();

        var job = new PerformTaskSystemJob();
        job.IsRockType = GetComponentDataFromEntity<RockTag>(true);
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.changes = tillChanges.AsParallelWriter();
        job.grid = data.gridStatus.AsParallelWriter();
        job.plantsSold = plantsSold;
        job.plantDataSet = plantDataSet.AsParallelWriter();
        job.addTagSet = addTagData.AsParallelWriter();
        job.removeTagSet = removeTagData.AsParallelWriter();
        job.MAX_GROWTH = PlantSystem.MAX_GROWTH;

        JobHandle jobHandle = job.Schedule(this, inputDependencies);

        jobHandle.Complete();

        EntityManager entityManager = World.Active.EntityManager;
        while (plantDataSet.Count > 0)
        {
            
            PlantDataSet plantData = plantDataSet.Dequeue();
            entityManager.SetComponentData(plantData.entity, plantData.plantData);

        }
        while (addTagData.Count > 0)
        {
            TagData tagData = addTagData.Dequeue();
            if (tagData.type == (int)TagTypes.NeedsTaskTag)
                entityManager.AddComponent(tagData.entity, typeof(NeedsTaskTag));
            else if (tagData.type == (int)TagTypes.PerformTaskTag)
                entityManager.AddComponent(tagData.entity, typeof(PerformTaskTag));
        }
        while (removeTagData.Count > 0)
        {
            TagData tagData = removeTagData.Dequeue();
            if (tagData.type == (int)TagTypes.NeedsTaskTag)
                entityManager.RemoveComponent(tagData.entity, typeof(NeedsTaskTag));
            else if (tagData.type == (int)TagTypes.PerformTaskTag)
                entityManager.RemoveComponent(tagData.entity, typeof(PerformTaskTag));
        }
        // we have to have a sync point here since we're about to change all uv's for the frame
        // This happens on main thread since uv's are Vector2 types and can't be changed inside of the job
        while (tillChanges.Count > 0)
        {
            float2 pos = tillChanges.Dequeue();
            if ((int)pos.x != -1 && (int)pos.y != -1)
            {
                // set the uv's on the mesh
                // NOTE: set pos to be a specific number if you want to test it
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
                tmp.SetUVs(0, uv);
                tmp.MarkModified();
            }
        }

        // fairly rare occurrence
        if (plantsSold[0] > 0)
        {
            
            storeInfo.moneyForFarmers += plantsSold[0];
            storeInfo.moneyForDrones += plantsSold[0];
            if (storeInfo.moneyForFarmers >= 10 && 
                GridDataInitialization.farmerCount < GridDataInitialization.MaxFarmers)
            {
                // spawn a new farmer - never more than 1 a frame
                storeInfo.moneyForFarmers -= 10;
                var instance = entityManager.Instantiate(GridDataInitialization.farmerEntity);
                GridDataInitialization.farmerCount++;
                int startX = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;
                int startZ = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;

                // Place the instantiated entity in a grid with some noise
                var position = new float3(startX, 2, startZ);
                entityManager.SetComponentData(instance, new Translation() { Value = position });
                var farmerData = new MovementComponent { myType=(int)MovingType.Farmer, startPos = new float2(startX, startZ), speed = 2, targetPos = new float2(startX, startZ) };
                var entityData = new EntityInfo { type = -1 };
                entityManager.SetComponentData(instance, farmerData);
                entityManager.AddComponentData(instance, entityData);
                // give his first command 
                entityManager.AddComponent<NeedsTaskTag>(instance);
            }

            if (storeInfo.moneyForDrones >= 50 &&
                GridDataInitialization.droneCount < GridDataInitialization.maxDrones)
            {
                // spawn a new drone
                storeInfo.moneyForDrones -= 50;
                // spawn a new drone - never more than 1 a frame
                var instance = entityManager.Instantiate(GridDataInitialization.droneEntity);
                GridDataInitialization.droneCount++;
                int startX = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;
                int startZ = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;

                // Place the instantiated entity in a grid with some noise
                var position = new float3(startX, 2, startZ);
                entityManager.SetComponentData(instance, new Translation() { Value = position });
                var droneData = new MovementComponent {myType = (int)MovingType.Drone, startPos = new float2(startX, startZ), speed = 2, targetPos = new float2(startX, startZ) };
                var entityData = new EntityInfo { type = -1 };
                entityManager.SetComponentData(instance, droneData);
                entityManager.SetComponentData(instance, entityData);
                // give his first command 
                entityManager.AddComponent<NeedsTaskTag>(instance);
            }
            plantsSold[0] = 0;
        }

        return jobHandle;
    }
}