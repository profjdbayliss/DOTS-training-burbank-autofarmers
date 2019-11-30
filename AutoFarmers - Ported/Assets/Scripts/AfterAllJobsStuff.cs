using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using Unity.Collections;

public struct TagInfo
{
    public Entity entity;
    public int shouldRemove;
    public Tags type;
}

public enum Tags { Moving = 0, NeedsTask = 1, PerformTask=2 };

public struct ComponentSetInfo
{
    public Entity entity;
    public PlantComponent plantComponent;
}

public class AfterAllJobsStuff : ComponentSystem
{
    private static Unity.Mathematics.Random rand;


    
    protected override void OnCreate()
    {
        rand = new Unity.Mathematics.Random(42);
    }

    protected override void OnDestroy()
    {
      
        base.OnDestroy();

    }

    protected override void OnUpdate()
    {
        EntityManager entityManager = World.Active.EntityManager;
        entityManager.CompleteAllJobs();

        // now do special stuff that can't be done in parallel!


        //
        // component buffer stuff that should eventually be parallel and in burst
        // and thus deleted from being here because it causes errors if it's not here
        //
        // right now the things that don't work for command buffers are:
        // setComponent, addComponent, removeComponent
        // so many tags can't be changed unless we're adding/removing them here

        // Drone Task System:
        while (DroneTaskSystem.addRemoveTags.Count > 0)
        {
            TagInfo tagInfo = DroneTaskSystem.addRemoveTags.Dequeue();
            if (tagInfo.shouldRemove == 1)
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(MovingTag));
                }
            }
            else
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(MovingTag));
                }
            }
        }

        while (DroneTaskSystem.componentSetInfo.Count > 0)
        {
            ComponentSetInfo setInfo = DroneTaskSystem.componentSetInfo.Dequeue();
            entityManager.SetComponentData(setInfo.entity, setInfo.plantComponent);
        }

        // farmer Task System:
        while (FarmerTaskSystem.addRemoveTags.Count > 0)
        {
            TagInfo tagInfo = FarmerTaskSystem.addRemoveTags.Dequeue();
            if (tagInfo.shouldRemove == 1)
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(MovingTag));
                }
            }
            else
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(MovingTag));
                }
            }
        }

        while (FarmerTaskSystem.componentSetInfo.Count > 0)
        {
            ComponentSetInfo setInfo = FarmerTaskSystem.componentSetInfo.Dequeue();
            entityManager.SetComponentData(setInfo.entity, setInfo.plantComponent);
        }

        // movement system:
        while (MovementSystem.addRemoveTags.Count > 0)
        {
            TagInfo tagInfo = MovementSystem.addRemoveTags.Dequeue();
            if (tagInfo.shouldRemove == 1)
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(MovingTag));
                } else if (tagInfo.type == Tags.PerformTask)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(PerformTaskTag));
                }
            }
            else
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(MovingTag));
                }
                else if (tagInfo.type == Tags.PerformTask)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(PerformTaskTag));
                }
            }
        }

        // perform tasks system:
        while (PerformTaskSystem.addRemoveTags.Count > 0)
        {
            TagInfo tagInfo = PerformTaskSystem.addRemoveTags.Dequeue();
            if (tagInfo.shouldRemove == 1)
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(MovingTag));
                }
                else if (tagInfo.type == Tags.PerformTask)
                {
                    entityManager.RemoveComponent(tagInfo.entity, typeof(PerformTaskTag));
                }
            }
            else
            {
                if (tagInfo.type == Tags.NeedsTask)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(NeedsTaskTag));
                }
                else if (tagInfo.type == Tags.Moving)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(MovingTag));
                }
                else if (tagInfo.type == Tags.PerformTask)
                {
                    entityManager.AddComponent(tagInfo.entity, typeof(PerformTaskTag));
                }
            }
        }

        while (PerformTaskSystem.componentSetInfo.Count > 0)
        {
            ComponentSetInfo setInfo = PerformTaskSystem.componentSetInfo.Dequeue();
            entityManager.SetComponentData(setInfo.entity, setInfo.plantComponent);
        }

        // Plant system:
        while (PlantSystem.componentSetInfo.Count > 0)
        {
            PlantSystem.ComponentTransInfo setInfo = PlantSystem.componentSetInfo.Dequeue();
            Translation trans = new Translation { Value = setInfo.trans };
            entityManager.SetComponentData(setInfo.entity, trans);
        }

        //=============================================
        //
        // PLANT SYSTEM
        //
        while (PlantSystem.plantCreationDeletionInfo.Count > 0)
        {
            Entity info = (Entity)PlantSystem.plantCreationDeletionInfo.Dequeue();
            // set deleted plants invisible and add them to the free plant list
            entityManager.AddComponent(info, typeof(Disabled));
            PlantSystem.freePlants.Enqueue(info);
        }

        //
        // PERFORM TASK SYSTEM 
        //

        // parallelization would be difficult since multiple farmers can potentially
        // turn the same tile
        while (PerformTaskSystem.tillChanges.Count > 0)
        {
            float2 pos = PerformTaskSystem.tillChanges.Dequeue();
            if ((int)pos.x != -1 && (int)pos.y != -1)
            {
                // set the uv's on the mesh
                // NOTE: set pos to be a specific number if you want to test it
                Mesh tmp = GridDataInitialization.getMesh((int)pos.x, (int)pos.y,
                    GridDataInitialization.BoardWidth);
                int width = GridDataInitialization.getMeshWidth(tmp, (int)pos.x,
                    (int)pos.y, GridDataInitialization.BoardWidth);

                Vector2[] uv = tmp.uv;
                TextureUV tex = GridDataInitialization.textures[(int)GridDataInitialization.BoardTypes.TilledDirt];
                int uvStartIndex = (GridDataInitialization.getPosForMesh((int)pos.y) +
                    width *
                    GridDataInitialization.getPosForMesh((int)pos.x)) * 4;
                //Debug.Log("changing uv at! " + pos + " " + width + " " + uvStartIndex + " " + GridDataInitialization.getPosForMesh((int)pos.x) +
                //    " " + GridDataInitialization.getPosForMesh((int)pos.y) + "array length: " + uv.Length);

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

        // max this gets run is once a frame and
        // many times it doesn't get run at all
        if (PerformTaskSystem.plantsSold[0] > 0)
        {
            PerformTaskSystem.storeInfo.moneyForFarmers += PerformTaskSystem.plantsSold[0];
            PerformTaskSystem.storeInfo.moneyForDrones += PerformTaskSystem.plantsSold[0];
            if (PerformTaskSystem.storeInfo.moneyForFarmers >= 10 &&
                GridDataInitialization.farmerCount < GridDataInitialization.MaxFarmers)
            {

                // spawn a new farmer - never more than 1 a frame
                PerformTaskSystem.storeInfo.moneyForFarmers -= 10;
                var instance = entityManager.Instantiate(GridDataInitialization.farmerEntity);
                GridDataInitialization.farmerCount++;
                int startX = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;
                int startZ = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;

                // Place the instantiated entity in a random position on the grid
                var position = new float3(startX, 2, startZ);
                entityManager.SetComponentData(instance, new Translation() { Value = position });
                var farmerData = new MovementComponent
                {
                    startPos = new float2(startX, startZ),
                    speed = 2,
                    targetPos = new float2(startX, startZ)
                };
                var entityData = new EntityInfo { type = -1 };
                entityManager.SetComponentData(instance, farmerData);
                entityManager.AddComponentData(instance, entityData);
                // give his first command 
                entityManager.AddComponent<NeedsTaskTag>(instance);

            }

            if (PerformTaskSystem.storeInfo.moneyForDrones >= 50 &&
                GridDataInitialization.droneCount < GridDataInitialization.MaxDrones)
            {
                // spawn a new drone
                PerformTaskSystem.storeInfo.moneyForDrones -= 50;
                var instance = entityManager.Instantiate(GridDataInitialization.droneEntity);
                GridDataInitialization.droneCount++;
                int startX = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;
                int startZ = System.Math.Abs(rand.NextInt()) % GridData.GetInstance().width;

                // Place the instantiated entity in a random position on the grid
                var position = new float3(startX, 2, startZ);
                entityManager.SetComponentData(instance, new Translation() { Value = position });
                var droneData = new MovementComponent
                {
                    startPos = new float2(startX, startZ),
                    speed = 2,
                    targetPos = new float2(startX, startZ),
                };
                var entityData = new EntityInfo { type = -1 };
                entityManager.SetComponentData(instance, droneData);
                entityManager.SetComponentData(instance, entityData);
                // give his first command 
                entityManager.AddComponent<NeedsTaskTag>(instance);
            }
            PerformTaskSystem.plantsSold[0] = 0;
        }

        //
        // DRONE TASK SYSTEM:
        //

        GridData data = GridData.GetInstance();
        // take care of drones
        while (DroneTaskSystem.hashRemovalsDrone.Count > 0)
        {
            DroneTaskSystem.RemovalInfo remInfo = (DroneTaskSystem.RemovalInfo)DroneTaskSystem.hashRemovalsDrone.Dequeue();
            int key = remInfo.key;
            EntityInfo value;
            if (data.gridStatus.TryGetValue(key, out value))
            {
                if (value.type == (int)Tiles.Plant)
                {
                    // this is a harvest, so try to remove and if we can
                    // then set up the entity to harvest it
                    if (data.gridStatus.ContainsKey(key))
                    {
                        data.gridStatus.Remove(key);
                        // set reserve data in plant component for this entity
                        entityManager.SetComponentData(value.specificEntity, new PlantComponent
                        {
                            timeGrown = PlantSystem.MAX_GROWTH,
                            state = (int)PlantState.None,
                            reserveIndex = remInfo.requestingEntity.Index,
                        });
                    }

                }
                else
                {
                    data.gridStatus.Remove(key);
                }

            }

        }

        //
        // FARMER TASK SYSTEM:
        //

        // this happens in the main thread
        // removal isn't in the parallel writer for the hash table
        // so it's just going to have to happen sequentially
        while (FarmerTaskSystem.hashRemovalsFarmer.Count > 0)
        {
            FarmerTaskSystem.RemovalInfo remInfo = FarmerTaskSystem.hashRemovalsFarmer.Dequeue();
            int key = remInfo.key;
            EntityInfo value;
            if (data.gridStatus.TryGetValue(key, out value))
            {
                if (value.type == (int)Tiles.Till)
                {
                    float plantingHeight = 1.0f;
                    // we're planting here and need to add a plant entity
                    data.gridStatus.Remove(key);
                    float2 trans = new float2(GridData.getRow(key), GridData.getCol(key));
                    Entity instance;
                    if (PlantSystem.freePlants.Count > 0)
                    {
                        // this will become the new plant to put it back into use
                        instance = (Entity)PlantSystem.freePlants.Dequeue();
                        entityManager.RemoveComponent(instance, typeof(Disabled));
                    }
                    else
                    {
                        // we really have to instantiate the plant
                        instance = entityManager.Instantiate(GridDataInitialization.plantEntity);
                        Rotation rotation = entityManager.GetComponentData<Rotation>(instance);
                        var newRot = rotation.Value * Quaternion.Euler(0, 0, 90);
                        entityManager.SetComponentData(instance, new Rotation { Value = newRot });

                    }

                    EntityInfo plantInfo = new EntityInfo { type = (int)Tiles.Plant, specificEntity = instance };
                    if (data.gridStatus.TryAdd(key, plantInfo))
                    {
                        float3 pos = new float3((int)trans.x, plantingHeight, (int)trans.y);
                        entityManager.SetComponentData(instance, new Translation { Value = pos });
                        entityManager.SetComponentData(instance, new NonUniformScale { Value = new float3(1.0f, 1.0f, 1.0f) });
                        // for some reason the original plant mesh creation happens on the wrong axis, 
                        // so we have to rotate it 90 degrees
                        entityManager.SetComponentData(instance, new PlantComponent
                        {
                            timeGrown = 0,
                            state = (int)PlantState.Growing,
                        });
                        //Debug.Log("added grid plant " + instance.Index);
                    }
                }
                else
                if (value.type == (int)Tiles.Plant)
                {
                    // this is a harvest, so try to remove and if we can
                    // then set up the entity to harvest it
                    if (data.gridStatus.ContainsKey(key))
                    {
                        data.gridStatus.Remove(key);
                        // set reserve data in plant component for this entity
                        entityManager.SetComponentData(value.specificEntity, new PlantComponent
                        {
                            timeGrown = PlantSystem.MAX_GROWTH,
                            state = (int)PlantState.None,
                            reserveIndex = remInfo.requestingEntity.Index,
                        });
                    }
                }
                else
                {
                    data.gridStatus.Remove(key);
                }
            }

        }

    }
}
