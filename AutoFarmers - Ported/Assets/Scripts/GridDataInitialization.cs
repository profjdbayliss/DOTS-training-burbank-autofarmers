using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;


public class GridDataInitialization : MonoBehaviour
{
	[Header("Grid Parameters")]
	 int gridWidth;
	 int gridHeight;
	public int rockSpawnAttempts;
	public int storeCount;

	[Header("Grid Objects")]
	public GameObject GridGeneratorPrefab;
	public GameObject RockPrefab;
	public GameObject StorePrefab;

	EntityManager em;
	Entity rockEntity;

	void Start()
	{
		
		gridWidth = GridData.width;
        gridHeight = GridData.width;
        GenerateGrid();
	}

	void GenerateGrid()
	{
		// Generate Entities
		em = World.Active.EntityManager;
		rockEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(RockPrefab, World.Active);
		em.AddComponentData(rockEntity, new RockTag { });


		// Spawn Grid Tiles
		GridGeneratorPrefab.GetComponent<SpawnGridAuthoring>().ColumnCount = gridWidth;
		GridGeneratorPrefab.GetComponent<SpawnGridAuthoring>().RowCount = gridHeight;
		Instantiate(GridGeneratorPrefab);

		// Spawn Stores
		int spawnedStores = 0;

		while (spawnedStores < storeCount)
		{
			int x = Random.Range(0, gridWidth);
			int y = Random.Range(0, gridHeight);

			int cellValue;
			GridData.gridStatus.TryGetValue(GridData.ConvertToHash(x, y), out cellValue);
			if (cellValue != 4)
			{
				GridData.gridStatus.TryAdd(GridData.ConvertToHash(x, y), GridData.ConvertDataValue(4, 0));
				Instantiate(StorePrefab, new Vector3(x, 0, y), Quaternion.identity);
				spawnedStores++;
			}
		}

		for (int i = 0; i < rockSpawnAttempts; i++)
		{
			TrySpawnRock();
		}
	}

	void TrySpawnRock()
	{
		int width = Random.Range(0, 4);
		int height = Random.Range(0, 4);
		int rockX = Random.Range(0, gridWidth - width);
		int rockY = Random.Range(0, gridHeight - height);
		RectInt rect = new RectInt(rockX, rockY, width, height);

		bool blocked = false;
		for (int x = rockX; x <= rockX + width; x++)
		{
			for (int y = rockY; y <= rockY + height; y++)
			{
				int tileValue;
				GridData.gridStatus.TryGetValue(GridData.ConvertToHash(x, y), out tileValue);

				if (tileValue != 0)
				{
					blocked = true;
					break;
				}
			}
			if (blocked) break;
		}
		if (blocked == false)
		{
			//Rock rock = new Rock(rect);
			//rocks.Add(rock);
			//TODO: Combine rocks into groups

			for (int x = rockX; x <= rockX + width; x++)
			{
				for (int y = rockY; y <= rockY + height; y++)
				{
					GridData.gridStatus.TryAdd(GridData.ConvertToHash(x, y), GridData.ConvertDataValue(1, 0));
					em.SetComponentData(rockEntity, new Translation() { Value = new Unity.Mathematics.float3(x,0,y) });
					em.Instantiate(rockEntity);				
				}
			}
		}
	}
}
