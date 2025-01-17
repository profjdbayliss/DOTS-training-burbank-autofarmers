﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using System;

public class GridDataInitialization : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{

    [Header("Grid Parameters")]
    public int boardWidth = 10;
    public int rockSpawnAttempts;
    public int storeCount;
    public int maxFarmers;
    public int farmerNumber;
    public int maxDrones;

    [Header("Grid Objects")]
    public GameObject RockPrefab;
    public GameObject StorePrefab;
    public GameObject PlantMeshPrefab;
    public GameObject TilePrefab;
    public GameObject FarmerPrefab;
    public GameObject DronePrefab;

    // entity information for converted things
    EntityManager entityManager;
    Entity rockEntity;
    public static Entity farmerEntity;
    public static Entity tilledTileEntity;
    public static Entity[] plantEntity;
    public const int DIFF_PLANT_COUNT = 6;
    public static Entity droneEntity;
    public static Entity firstFarmer;

    // counts for maximum numbers of entities
    public static int farmerCount;
    public static int droneCount;

    // board size - can be reset by editor   
    public static int MAX_MESH_WIDTH = 64;

    // texture atlas variables:
    //public static readonly int pixelWidth = 16;
    //public static readonly int pixelHeight = 16;
    //public static int atlasHeight = 0;
    //public static int atlasWidth = 0;
    //public static Texture2D atlas;
    public static int TEXTURE_NUMBER = 2;
    public enum BoardTypes : int { Board = 0, TilledDirt = 1 };
    //public static string[] names;
    public static TextureUV[] textures;
    public static int MaxFarmers;
    public static int MaxDrones;
    public static int BoardWidth;
    public static Mesh plantMesh;

    //  renderer info
    public static int MATERIAL_NUMBER = 1;
    public static Mesh[] allMeshes;
    public static NativeArray<float2>[] allUVs;
    public static NativeArray<float3>[] allVerts; 
    public static NativeArray<int>[] allTris;

    // Board rendering variables
    private NativeArray<int> blockIndices; // stores which uv's are used per block
    private EntityArchetype boardArchetype; // includes the game board tag
    private Entity boardEntity; // the actual board

    public static void MyDestroy()
    {
    UnityEngine.Debug.Log("getting rid of all init native arrays");
        for (int i = 0; i < allTris.Length; i++)
        {
            if (allTris[i].IsCreated)
                allTris[i].Dispose();
        }
    
        for (int i = 0; i < allVerts.Length; i++)
        {
            if (allVerts[i].IsCreated)
                allVerts[i].Dispose();
        }
    
        for (int i = 0; i < allTris.Length; i++)
        {
            if (allTris[i].IsCreated)
                allTris[i].Dispose();
        }
        
        
    
        // GridData data = GridData.GetInstance();
        // if (data.gridStatus.IsCreated)
        //     data.gridStatus.Dispose();
    }

    // Referenced prefabs have to be declared so that the conversion system knows about them ahead of time
    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(TilePrefab);
        referencedPrefabs.Add(RockPrefab);
        referencedPrefabs.Add(StorePrefab);
        referencedPrefabs.Add(PlantMeshPrefab);
        referencedPrefabs.Add(DronePrefab);
        referencedPrefabs.Add(FarmerPrefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // set up initial counts and values for everything
        MaxFarmers = maxFarmers;
        MaxDrones = maxDrones;
        droneCount = 0;
        farmerCount = 0;
        BoardWidth = boardWidth;
        PerformTaskSystem.InitializeTillSystem(maxFarmers);

        // set up mesh rendering from prefab
        MeshRenderer meshRenderer = TilePrefab.GetComponent<MeshRenderer>();
        var meshFilter = TilePrefab.GetComponent<MeshFilter>();
        var materials = new List<Material>(MATERIAL_NUMBER);
        var mesh = meshFilter.sharedMesh;
        meshRenderer.GetSharedMaterials(materials);

        // set up entity and manager
        entityManager = dstManager;
        boardEntity = conversionSystem.GetPrimaryEntity(TilePrefab);

        // a board archetype
        boardArchetype = entityManager.CreateArchetype(
            typeof(Translation), typeof(GridBoard));

        // Generate rock prefab entity
        rockEntity = conversionSystem.GetPrimaryEntity(RockPrefab);
        entityManager.AddComponentData(rockEntity, new RockTag { });

        // Generate drone prefab entity
        droneEntity = conversionSystem.GetPrimaryEntity(DronePrefab);
        entityManager.AddComponentData(droneEntity, new MovementComponent { });
        entityManager.AddComponentData(droneEntity, new EntityInfo { specificEntity = droneEntity, type = -1 });
        entityManager.AddComponent(droneEntity, typeof(DroneTag));

        // generate 3 plants to use for everything else
        plantEntity = new Entity[DIFF_PLANT_COUNT];
        Unity.Mathematics.Random rand = new Unity.Mathematics.Random(42);
        int nextRandom = rand.NextInt();
        MeshRenderer meshPlantRenderer = PlantMeshPrefab.GetComponent<MeshRenderer>();
        var meshPlantFilter = PlantMeshPrefab.GetComponent<MeshFilter>();
        var materials2 = new List<Material>(MATERIAL_NUMBER);
        meshPlantRenderer.GetSharedMaterials(materials2);
        
        for (int i = 0; i < DIFF_PLANT_COUNT; i++)
        {
            
            plantMesh = GeneratePlantMesh(nextRandom);
            var meshPlantTmp = Instantiate(plantMesh);
            meshPlantFilter.sharedMesh = meshPlantTmp;
            plantEntity[i] = conversionSystem.GetPrimaryEntity(PlantMeshPrefab);
            Convert(plantEntity[i], dstManager, conversionSystem, meshPlantRenderer, meshPlantTmp, materials2);
            entityManager.AddComponent(plantEntity[i], typeof(PlantTag));
            entityManager.SetComponentData(plantEntity[i], new Translation { Value = new float3(-1, -5, -1) });
            entityManager.AddComponentData(plantEntity[i], new NonUniformScale { Value = new float3(1.0f, 2.0f, 1.0f) });
            entityManager.AddComponentData(plantEntity[i], new PlantComponent
            {
                timeGrown = 0,
                state = (int)PlantState.None,
                reserveIndex = -1
            });
            nextRandom = rand.NextInt();
        }
        // create atlas and texture info
        CalculatePredeterminedTextures();
        // if textures changed we'd have to re-create them with the following:
        //CreateAtlasData();
        //CreateTextures();

        // the texture indices in the world
        // clearing memory gives everything the first image in the uv's, 
        // which is conveniently non-tilled ground
        blockIndices = new NativeArray<int>(BoardWidth * BoardWidth, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        // initialize hash table that stores all the tile state info
        GridData gdata = GridData.GetInstance();
        gdata.Initialize(BoardWidth);

        // generate the terrain mesh and add it to the world
        Mesh mesh2;
        int maxX = BoardWidth / MAX_MESH_WIDTH;
        int maxZ = BoardWidth / MAX_MESH_WIDTH;
        // only one mesh
        allMeshes = new Mesh[(maxX + 1) * (maxZ + 1)];
        allUVs = new NativeArray<float2>[(maxX + 1) * (maxZ + 1)];
        allVerts = new NativeArray<float3>[(maxX + 1) * (maxZ + 1)]; 
        allTris = new NativeArray<int>[(maxX + 1) * (maxZ + 1)]; 

        // for small enough meshes it's just a single mesh in the world
        int height = 0;
        if (maxX == 0 && maxZ == 0)
        {
            int cornerX = 0;
            int cornerZ = 0;
            allUVs[0] = new NativeArray<float2>(BoardWidth * BoardWidth * 4, Allocator.Persistent);
            mesh2 = GenerateTerrainMesh(BoardWidth, BoardWidth, 0, 0, height, allUVs[0]);

            mesh = Instantiate(mesh2);
            allMeshes[0] = mesh;
            meshFilter.sharedMesh = mesh;

            var segmentEntity = conversionSystem.CreateAdditionalEntity(gameObject);
            var pos = new float3(cornerX, 0, cornerZ);

            var localToWorld = new LocalToWorld
            {
                Value = float4x4.Translate(pos)
            };
            var aabb = new AABB
            {
                Center = pos,
                Extents = new float3(BoardWidth, 0.5f, BoardWidth)
            };
            var worldRenderBounds = new WorldRenderBounds
            {
                Value = aabb
            };

            dstManager.AddComponentData(segmentEntity, localToWorld);
            dstManager.AddComponentData(segmentEntity, worldRenderBounds);
            dstManager.AddComponent(segmentEntity, ComponentType.ChunkComponent<ChunkWorldRenderBounds>());
            //dstManager.AddComponent(segmentEntity, typeof(Frozen));

            Convert(segmentEntity, dstManager, conversionSystem, meshRenderer, mesh, materials);

        }
        else
        {
            // FIX: this could be parallelized as all meshes are 
            // independent from each other
            // for larger meshes it's broken up into
            // 64k vertex pieces
            
            for (int index = 0; index < (maxX + 1) * (maxZ + 1) ; index++) { 
            //Parallel.For(0, (maxX + 1) * (maxZ + 1), (index) =>
            //{
                int x = (int)(index / (maxX + 1));
                int z = index - (maxX + 1) * x;
                //UnityEngine.Debug.Log("x: " + x + " " + z);
                int cornerX = x * MAX_MESH_WIDTH;
                int cornerZ = z * MAX_MESH_WIDTH;
                int width = 0;
                int depth = 0;
                int startX = 0;
                int startZ = 0;

                float3 pos = new float3(cornerX, 0, cornerZ);

                if (x < maxX && z < maxZ)
                {
                    startX = cornerX;
                    startZ = cornerZ;
                    width = MAX_MESH_WIDTH;
                    depth = MAX_MESH_WIDTH;
                }
                else if (x < maxX)
                {
                    startX = cornerX;
                    startZ = cornerZ;
                    width = MAX_MESH_WIDTH;
                    depth = BoardWidth - cornerZ;
                }
                else if (z < maxZ)
                {
                    startX = cornerX;
                    startZ = cornerZ;
                    width = BoardWidth - cornerX;
                    depth = MAX_MESH_WIDTH;
                }
                else
                {
                    startX = cornerX;
                    startZ = cornerZ;
                    width = BoardWidth - cornerX;
                    depth = BoardWidth - cornerZ;
                }
                //UnityEngine.Debug.Log("index " + index + " " + width + " " + depth + 
                //    " " + cornerX + " " + cornerZ);

                allUVs[index] = new NativeArray<float2>(width * depth * 4, Allocator.Persistent);
                allVerts[index] = new NativeArray<float3>(width * depth * 4, Allocator.Persistent);
                allTris[index] = new NativeArray<int>(width * depth * 6, Allocator.Persistent);
                NativeArray<float2> uvs = allUVs[index];
                NativeArray<float3> vert = allVerts[index];
                NativeArray<int> tri = allTris[index];

                int triangleIndex = 0;
                int vertexIndex = 0;
                int vertexMultiplier = 4; // create quads to fit uv's to so we can use more than one uv

                int uvIndex = 0;

                for (x = 0; x < width; x++)
                {
                    for (z = 0; z < depth; z++)
                    {
                        int y = height;
                        int textureIndex = 0;
                        int index2D = (z + startZ) + width * (x + startX);
                        textureIndex = blockIndices[index2D];

                        // add vertices for the quad first
                        // front
                        vert[vertexIndex] = new float3(x + 0.5f, 0.5f, z + -0.5f);
                        vert[vertexIndex + 1] = new float3(x + 0.5f, 0.5f, z + 0.5f);
                        vert[vertexIndex + 2] = new float3(x + -0.5f, 0.5f, z + 0.5f);
                        vert[vertexIndex + 3] = new float3(x + -0.5f, 0.5f, z + -0.5f);

                        // set the UV's
                        uvs[uvIndex] = new float2(textures[textureIndex].pixelStartX,
                           textures[textureIndex].pixelStartY);
                        uvs[uvIndex + 1] = new float2(textures[textureIndex].pixelStartX,
                                textures[textureIndex].pixelEndY);
                        uvs[uvIndex + 2] = new float2(textures[textureIndex].pixelEndX,
                            textures[textureIndex].pixelEndY);
                        uvs[uvIndex + 3] = new float2(textures[textureIndex].pixelEndX,
                            textures[textureIndex].pixelStartY);
                        uvIndex += 4;

                        // front or top face                   
                        tri[triangleIndex] = vertexIndex;
                        tri[triangleIndex + 1] = vertexIndex + 2;
                        tri[triangleIndex + 2] = vertexIndex + 1;
                        tri[triangleIndex + 3] = vertexIndex;
                        tri[triangleIndex + 4] = vertexIndex + 3;
                        tri[triangleIndex + 5] = vertexIndex + 2;
                        triangleIndex += 6;

                        // increment the vertices
                        vertexIndex += vertexMultiplier;
                    }

                }
                AABB aabb;
                aabb = new AABB
                {
                    Center = pos,
                    Extents = new float3(width, 0.5f, depth)
                };
                mesh2 = new Mesh();
                mesh2.SetVertices(allVerts[index]);
                mesh2.SetUVs(0, allUVs[index]);
                mesh2.SetTriangles(allTris[index].ToArray(), 0);
                mesh2.RecalculateNormals();
                mesh2.RecalculateBounds();
                mesh = Instantiate(mesh2);
                meshFilter.sharedMesh = mesh;
                //Debug.Log("creating mesh for : " + x + " " + z + "  " + (z + (maxX + 1) * x));
                allMeshes[index] = mesh;
                var segmentEntity = conversionSystem.CreateAdditionalEntity(gameObject);

                var localToWorld = new LocalToWorld
                {
                    Value = float4x4.Translate(pos)
                };
                var worldRenderBounds = new WorldRenderBounds
                {
                    Value = aabb
                };

                dstManager.AddComponentData(segmentEntity, localToWorld);
                dstManager.AddComponentData(segmentEntity, worldRenderBounds);
                dstManager.AddComponent(segmentEntity, ComponentType.ChunkComponent<ChunkWorldRenderBounds>());
                //dstManager.AddComponent(segmentEntity, typeof(Frozen));

                Convert(segmentEntity, dstManager, conversionSystem, meshRenderer, mesh, materials);

            }
            //);

        }

        // generate rocks and such on the grid
        GenerateGrid();

        // generate the farmers
        // Create farmer entity prefab from the game object hierarchy once
        farmerEntity = conversionSystem.GetPrimaryEntity(FarmerPrefab);
        entityManager.AddComponent<FarmerTag>(farmerEntity);

        // FIX: this could be parallelized
        // NOTE: NOT WORTH DOING UNTIL ALL THE add/set component is
        // part of burst as that's all this is
        // Also note: usually not that many farmers to justify
        // parallelization.
        for (int i = 0; i < farmerNumber; i++)
        {
            farmerCount++;
            var instance = entityManager.Instantiate(farmerEntity);
            if (i==0)
            {
                firstFarmer = instance;
            }
            int startX = Math.Abs(rand.NextInt()) % gdata.width;
            int startZ = Math.Abs(rand.NextInt()) % gdata.width;

            // Place the instantiated entity in a grid with some noise
            var position = new float3(startX, 2, startZ);
            entityManager.SetComponentData(instance, new Translation() { Value = position });
            var data = new MovementComponent
            {
                startPos = new float2(startX, startZ),
                speed = 2,
                targetPos = new float2(startX, startZ),
            };
            var entityData = new EntityInfo { specificEntity = instance,type = -1 };
            entityManager.AddComponentData(instance, data);
            entityManager.AddComponentData(instance, entityData);

            // give his first command based on the 1's in the hash
            entityManager.AddComponent<NeedsTaskTag>(instance);

        }
        
        if (blockIndices.IsCreated)
            blockIndices.Dispose();
    }

    public static void Convert(
        Entity entity,
        EntityManager dstEntityManager,
        GameObjectConversionSystem conversionSystem,
        Renderer meshRenderer,
        Mesh mesh,
        List<Material> materials)
    {
        var materialCount = materials.Count;

        // Don't add RenderMesh (and other required components) unless both mesh and material assigned.
        if ((mesh != null) && (materialCount > 0))
        {
            var renderMesh = new RenderMesh
            {
                mesh = mesh,
                castShadows = meshRenderer.shadowCastingMode,
                receiveShadows = meshRenderer.receiveShadows,
                layer = meshRenderer.gameObject.layer
            };

            //@TODO: Transform system should handle RenderMeshFlippedWindingTag automatically. This should not be the responsibility of the conversion system.
            float4x4 localToWorld = meshRenderer.transform.localToWorldMatrix;
            var flipWinding = math.determinant(localToWorld) < 0.0;

            if (materialCount == 1)
            {
                renderMesh.material = materials[0];
                renderMesh.subMesh = 0;

                dstEntityManager.AddSharedComponentData(entity, renderMesh);

                dstEntityManager.AddComponentData(entity, new PerInstanceCullingTag());
                dstEntityManager.AddComponentData(entity, new RenderBounds { Value = mesh.bounds.ToAABB() });

                if (flipWinding)
                    dstEntityManager.AddComponent(entity, ComponentType.ReadWrite<RenderMeshFlippedWindingTag>());

                conversionSystem.ConfigureEditorRenderData(entity, meshRenderer.gameObject, true);
            }
            else
            {
                for (var m = 0; m != materialCount; m++)
                {
                    var meshEntity = conversionSystem.CreateAdditionalEntity(meshRenderer);

                    renderMesh.material = materials[m];
                    renderMesh.subMesh = m;

                    dstEntityManager.AddSharedComponentData(meshEntity, renderMesh);

                    dstEntityManager.AddComponentData(meshEntity, new PerInstanceCullingTag());
                    dstEntityManager.AddComponentData(meshEntity, new RenderBounds { Value = mesh.bounds.ToAABB() });
                    dstEntityManager.AddComponentData(meshEntity, new LocalToWorld { Value = localToWorld });

                    if (!dstEntityManager.HasComponent<Static>(meshEntity))
                    {
                        dstEntityManager.AddComponentData(meshEntity, new Parent { Value = entity });
                        dstEntityManager.AddComponentData(meshEntity, new LocalToParent { Value = float4x4.identity });
                    }

                    if (flipWinding)
                        dstEntityManager.AddComponent(meshEntity, ComponentType.ReadWrite<RenderMeshFlippedWindingTag>());

                    conversionSystem.ConfigureEditorRenderData(meshEntity, meshRenderer.gameObject, true);
                }
            }
        }
    }
    
    // gets a texture that's created with the atlas from a 
    // set of images in a directory with the same height/width
    //public TextureUV getTextureUV(string name)
    //{

    //    if (textures.Length > 0)
    //    {
    //        for (int index = 0; index < names.Length; index++)
    //        {
    //            if (names[index].Equals(name))
    //            {
    //                return textures[index];
    //            }
    //        }
    //    }
    //    return new TextureUV();
    //}

    //// create the atlas texture image from lots of little images
    //// that are all the same width/height
    //public static void CreateAtlasData()
    //{
    //    names = Directory.GetFiles("blocks");
    //    textures = new TextureUV[TEXTURE_NUMBER];

    //    // this assumes images are a power of 2, so it's slightly off 
    //    int squareRoot = Mathf.CeilToInt(Mathf.Sqrt(names.Length));
    //    int squareRootH = squareRoot;
    //    atlasWidth = squareRoot * pixelWidth;
    //    atlasHeight = squareRootH * pixelHeight;
    //    if (squareRoot * (squareRoot - 1) > names.Length)
    //    {
    //        squareRootH = squareRootH - 1;
    //        atlasHeight = squareRootH * pixelHeight;
    //    }

    //    // allocate space for the atlas and file data
    //    atlas = new Texture2D(atlasWidth, atlasHeight);
    //    byte[][] fileData = new byte[names.Length][];

    //    // read the file data in parallel
    //    Parallel.For(0, names.Length,
    //    index =>
    //    {
    //        fileData[index] = File.ReadAllBytes(names[index]);
    //    });

    //    int x1 = 0;
    //    int y1 = 0;
    //    Texture2D temp = new Texture2D(pixelWidth, pixelHeight);
    //    float pWidth = (float)pixelWidth;
    //    float pHeight = (float)pixelHeight;
    //    float aWidth = (float)atlas.width;
    //    float aHeight = (float)atlas.height;

    //    for (int i = 0; i < names.Length; i++)
    //    {
    //        float pixelStartX = ((x1 * pWidth) + 1) / aWidth;
    //        float pixelStartY = ((y1 * pHeight) + 1) / aHeight;
    //        float pixelEndX = ((x1 + 1) * pWidth - 1) / aWidth;
    //        float pixelEndY = ((y1 + 1) * pHeight - 1) / aHeight;

    //        textures[i] = new TextureUV
    //        {
    //            nameID = i,
    //            pixelStartX = pixelStartX,
    //            pixelStartY = pixelStartY,
    //            pixelEndY = pixelEndY,
    //            pixelEndX = pixelEndX,
    //        };
    //        // useful for hard coding the textures:
    //        //UnityEngine.Debug.Log("texture " + i + " pixelStartX: " + pixelStartX + " pixelStartY " + pixelStartY +
    //        //    " pixelEndY " + pixelEndY + " pixelEndX " + pixelEndX);
    //        temp.LoadImage(fileData[i]);
    //        atlas.SetPixels(x1 * pixelWidth, y1 * pixelHeight, pixelWidth, pixelHeight, temp.GetPixels());

    //        x1 = (x1 + 1) % squareRoot;
    //        if (x1 == 0)
    //        {
    //            y1++;
    //        }


    //    }

    //    // the following only works in the editor:
    //    //atlas.alphaIsTransparency = true;
    //    atlas.wrapMode = TextureWrapMode.Clamp;
    //    atlas.filterMode = FilterMode.Point;

    //    atlas.Apply();
    //    // test to make sure there's not an off by one error on images
    //    //File.WriteAllBytes("../atlas.png", atlas.EncodeToPNG());
    //}


    // create individual textures and assign them numbers
    // after they're created with the atlas
    //protected void CreateTextures()
    //{
    //    // create all the textures we'll use

    //    TextureUV tex;

    //    // DIRT
    //    tex = getTextureUV("blocks\\ground.png");
    //    textures[(int)BoardTypes.Board] = tex;

    //    // Farmland dirt - tilled
    //    tex = getTextureUV("blocks\\groundTilled.png");
    //    textures[(int)BoardTypes.TilledDirt] = tex;

    //}

    protected void CalculatePredeterminedTextures()
    {
        // create all the textures we'll use
        textures = new TextureUV[TEXTURE_NUMBER];

        // DIRT
        textures[(int)BoardTypes.Board] = new TextureUV
        {
            nameID = 0,
            pixelStartX = 0.03125f,
            pixelStartY = 0.03125f,
            pixelEndX = 0.46875f,
            pixelEndY = 0.46875f
        };

        // Farmland dirt - tilled
        textures[(int)BoardTypes.TilledDirt] = new TextureUV
        {
            nameID = 1,
            pixelStartX = 0.53125f,
            pixelStartY = 0.03125f,
            pixelEndY = 0.46875f,
            pixelEndX = 0.96875f
        };


    }

    // generates the full tile mesh in pieces
    public Mesh GenerateTerrainMesh(int width, int depth, int startX, int startZ, int height,
        NativeArray<float2> uvs)
    {
        int triangleIndex = 0;
        int vertexIndex = 0;
        int vertexMultiplier = 4; // create quads to fit uv's to so we can use more than one uv

        Mesh terrainMesh = new Mesh();
        List<Vector3> vert = new List<Vector3>(width * depth * vertexMultiplier);
        List<int> tri = new List<int>(width * depth * 6);
        //List<Vector2> uv = new List<Vector2>(width * depth * vertexMultiplier);

        int uvIndex = 0;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int y = height;
                int textureIndex = 0;
                int index2D = (z + startZ) + width * (x + startX);
                textureIndex = blockIndices[index2D];

                // add vertices for the quad first
                // front
                vert.Add(new Vector3(x + 0.5f, 0.5f, z + -0.5f));
                vert.Add(new Vector3(x + 0.5f, 0.5f, z + 0.5f));
                vert.Add(new Vector3(x + -0.5f, 0.5f, z + 0.5f));
                vert.Add(new Vector3(x + -0.5f, 0.5f, z + -0.5f));
                //Debug.Log("starts and ends for UV: " + textures[textureIndex].pixelStartX + " " +
                //    textures[textureIndex].pixelStartY + " " + textures[textureIndex].pixelEndX + " " +
                //    textures[textureIndex].pixelEndY);
                //Debug.Log("textureindex == " + textureIndex);
                // test uv's that show whole block:
                //uv.Add(new Vector2(0,0));
                //uv.Add(new Vector2(0, 1));
                //uv.Add(new Vector2(1, 1));
                //uv.Add(new Vector2(1, 0));
                //uv.Add(new Vector2(textures[textureIndex].pixelStartX,
                //    textures[textureIndex].pixelStartY));
                //uv.Add(new Vector2(textures[textureIndex].pixelStartX,
                //        textures[textureIndex].pixelEndY));
                //uv.Add(new Vector2(textures[textureIndex].pixelEndX,
                //    textures[textureIndex].pixelEndY));
                //uv.Add(new Vector2(textures[textureIndex].pixelEndX,
                //    textures[textureIndex].pixelStartY));

                uvs[uvIndex] = new float2(textures[textureIndex].pixelStartX,
                   textures[textureIndex].pixelStartY);
                uvs[uvIndex + 1] = new float2(textures[textureIndex].pixelStartX,
                        textures[textureIndex].pixelEndY);
                uvs[uvIndex + 2] = new float2(textures[textureIndex].pixelEndX,
                    textures[textureIndex].pixelEndY);
                uvs[uvIndex + 3] = new float2(textures[textureIndex].pixelEndX,
                    textures[textureIndex].pixelStartY);
                uvIndex += 4;

                // front or top face                   
                tri.Add(vertexIndex);
                tri.Add(vertexIndex + 2);
                tri.Add(vertexIndex + 1);
                tri.Add(vertexIndex);
                tri.Add(vertexIndex + 3);
                tri.Add(vertexIndex + 2);
                triangleIndex += 6;

                // increment the vertices
                vertexIndex += vertexMultiplier;
            }

        }

        terrainMesh.vertices = vert.ToArray();
        terrainMesh.SetUVs(0, uvs);
        terrainMesh.triangles = tri.ToArray();
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();

        return terrainMesh;
    }
    void GenerateGrid()
    {

        int spawnedStores = 0;
        GridData data = GridData.GetInstance();

        while (spawnedStores < storeCount)
        {
            int x = UnityEngine.Random.Range(0, BoardWidth);
            int y = UnityEngine.Random.Range(0, BoardWidth);

            EntityInfo cellValue;
            data.gridStatus.TryGetValue(GridData.ConvertToHash(x, y), out cellValue);
            if (cellValue.type != (int)Tiles.Store)
            {
                cellValue = new EntityInfo { type = (int)Tiles.Store };
                data.gridStatus.TryAdd(GridData.ConvertToHash(x, y), cellValue);
                Instantiate(StorePrefab, new Vector3(x, 2, y), Quaternion.identity);
                spawnedStores++;
            }
        }

        // FIX: This could maybe be parallelized?
        for (int i = 0; i < rockSpawnAttempts; i++)
        {
            TrySpawnRock();
        }
    }

    // rock spawner from the original sim
    // except currently spawns cubes rather
    // than rectangles
    void TrySpawnRock()
    {


        GridData data = GridData.GetInstance();
        int width = UnityEngine.Random.Range(0, 4);
        int height = UnityEngine.Random.Range(0, 4);
        int rockX = UnityEngine.Random.Range(0, BoardWidth - width);
        int rockY = UnityEngine.Random.Range(0, BoardWidth - height);
        RectInt rect = new RectInt(rockX, rockY, width, height);

        bool blocked = false;
        for (int x = rockX; x <= rockX + width; x++)
        {
            for (int y = rockY; y <= rockY + height; y++)
            {
                EntityInfo tileValue;
                if (data.gridStatus.TryGetValue(GridData.ConvertToHash(x, y), out tileValue))
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked) break;
        }
        if (blocked == false)
        {
            //TODO: Combine rocks into groups

            for (int x = rockX; x <= rockX + width; x++)
            {
                for (int y = rockY; y <= rockY + height; y++)
                {
                    // get some new rock entities and add them to an array
                    // so we can find them later
                    Entity tmp = entityManager.Instantiate(rockEntity);
                    EntityInfo info = new EntityInfo { type = (int)Tiles.Rock, specificEntity = tmp };
                    data.gridStatus.TryAdd(GridData.ConvertToHash(x, y), info);
                    //Debug.Log("entity info is: " + x + " " + y + " index: " + entityIndex + " " + tmp.Index);
                    entityManager.SetComponentData(tmp, new Translation() { Value = new Unity.Mathematics.float3(x, height*0.25f, y) });
                }
            }
        }
    }

    // This algorithm is from the original
    // sim and is done once to represent all plants
    // in the game
    Mesh GeneratePlantMesh(int seed)
    {
        UnityEngine.Random.State oldRandState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(seed);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();
        List<Vector2> uv = new List<Vector2>();

        Color color1 = UnityEngine.Random.ColorHSV(0f, 1f, .5f, .8f, .25f, .9f);
        Color color2 = UnityEngine.Random.ColorHSV(0f, 1f, .5f, .8f, .25f, .9f);

        float height = UnityEngine.Random.Range(.4f, 1.4f);

        float angle = UnityEngine.Random.value * Mathf.PI * 2f;
        float armLength1 = UnityEngine.Random.value * .4f + .1f;
        float armLength2 = UnityEngine.Random.value * .4f + .1f;
        float armRaise1 = UnityEngine.Random.value * .3f;
        float armRaise2 = UnityEngine.Random.value * .6f - .3f;
        float armWidth1 = UnityEngine.Random.value * .5f + .2f;
        float armWidth2 = UnityEngine.Random.value * .5f + .2f;
        float armJitter1 = UnityEngine.Random.value * .3f;
        float armJitter2 = UnityEngine.Random.value * .3f;
        float stalkWaveStr = UnityEngine.Random.value * .5f;
        float stalkWaveFreq = UnityEngine.Random.Range(.25f, 1f);
        float stalkWaveOffset = UnityEngine.Random.value * Mathf.PI * 2f;

        int triCount = UnityEngine.Random.Range(15, 35);

        for (int i = 0; i < triCount; i++)
        {
            // front face
            triangles.Add(vertices.Count);
            triangles.Add(vertices.Count + 1);
            triangles.Add(vertices.Count + 2);

            // back face
            triangles.Add(vertices.Count + 1);
            triangles.Add(vertices.Count);
            triangles.Add(vertices.Count + 2);

            float t = i / (triCount - 1f);
            float armLength = Mathf.Lerp(armLength1, armLength2, t);
            float armRaise = Mathf.Lerp(armRaise1, armRaise2, t);
            float armWidth = Mathf.Lerp(armWidth1, armWidth2, t);
            float armJitter = Mathf.Lerp(armJitter1, armJitter2, t);
            float stalkWave = Mathf.Sin(t * stalkWaveFreq * 2f * Mathf.PI + stalkWaveOffset) * stalkWaveStr;

            float y = t * height;
            vertices.Add(new Vector3(stalkWave, y, 0f));
            Vector3 armPos = new Vector3(stalkWave + Mathf.Cos(angle) * armLength, y + armRaise, Mathf.Sin(angle) * armLength);
            vertices.Add(armPos + UnityEngine.Random.insideUnitSphere * armJitter);
            armPos = new Vector3(stalkWave + Mathf.Cos(angle + armWidth) * armLength, y + armRaise, Mathf.Sin(angle + armWidth) * armLength);
            vertices.Add(armPos + UnityEngine.Random.insideUnitSphere * armJitter);

            colors.Add(color1);
            colors.Add(color2);
            colors.Add(color2);
            uv.Add(Vector2.zero);
            uv.Add(Vector2.right);
            uv.Add(Vector2.right);

            // golden angle in radians
            angle += 2.4f;
        }

        Mesh outputMesh = new Mesh();
        outputMesh.name = "Generated Plant (" + seed + ")";

        outputMesh.SetVertices(vertices);
        outputMesh.SetColors(colors);
        outputMesh.SetTriangles(triangles, 0);
        outputMesh.RecalculateNormals();

        UnityEngine.Random.state = oldRandState;
        return outputMesh;
    }

    public static Mesh getMesh(int x, int z, int width)
    {
        int maxX = width / MAX_MESH_WIDTH;
        int xIndex = x / MAX_MESH_WIDTH;
        int zIndex = z / MAX_MESH_WIDTH;
        //Debug.Log("mesh element at: " + (zIndex + (maxX + 1) * xIndex));
        // only one mesh
        return allMeshes[zIndex + (maxX + 1) * xIndex];

    }

    public static NativeArray<float2> getUVs(int x, int z, int width)
    {
        int maxX = width / MAX_MESH_WIDTH;
        int xIndex = x / MAX_MESH_WIDTH;
        int zIndex = z / MAX_MESH_WIDTH;
        //Debug.Log("mesh element at: " + (zIndex + (maxX + 1) * xIndex));
        // only one mesh
        return allUVs[zIndex + (maxX + 1) * xIndex];

    }

    public static int getPosForMesh(int pos)
    {
        if (pos < MAX_MESH_WIDTH)
        {
            return pos;
        }
        else
        {
            //Debug.Log("pos for mesh is: " + (pos - (pos / MAX_MESH_WIDTH) * MAX_MESH_WIDTH));
            return pos - (pos / MAX_MESH_WIDTH) * MAX_MESH_WIDTH;
        }
    }

    public static int getMeshWidth(Mesh mesh, int x, int z, int width)
    {
        int meshCount = mesh.vertexCount / 4;
        //Debug.Log("mesh count is : " + meshCount);
        if (meshCount == MAX_MESH_WIDTH * MAX_MESH_WIDTH)
        {
            return MAX_MESH_WIDTH;
        }
        else if (width < MAX_MESH_WIDTH)
        {
            return width;
        }
        else
        {
            // this is on an edge: x, y, or both
            if (z >= (MAX_MESH_WIDTH * (width / MAX_MESH_WIDTH)))
            {
                //Debug.Log("mesh width 2 : " + (width - MAX_MESH_WIDTH * (width / MAX_MESH_WIDTH)));
                return width - MAX_MESH_WIDTH * (width / MAX_MESH_WIDTH);

            }
            else
            {
                //Debug.Log("mesh width 3 is : " + (MAX_MESH_WIDTH));
                return MAX_MESH_WIDTH;
            }

        }
    }

}

