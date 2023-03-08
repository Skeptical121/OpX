using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using Unity.Physics;
using Ray = Unity.Physics.Ray;
using RaycastHit = Unity.Physics.RaycastHit;
using Unity.Physics.Systems;

public class Game : MonoBehaviour
{

	public static Map map;
	public static PersonMap personMap;
	public static DisasterMap disasterMap;
	public static NativeMultiHashMap<Entity, PFTile> tilesTaken;
	public static Texture2D tex;
	public static int numDeaths;
	public static int numPersonPathFinds;
	public static Op1Co op1Co;

	public static float GetTickTime()
	{
		return Time.fixedDeltaTime;
	}

	public static float GetGameTime()
	{
		return ECSHandler.gameTime;
	}

	public static long NanoTime()
	{
		long nano = 10000L * System.Diagnostics.Stopwatch.GetTimestamp();
		nano /= TimeSpan.TicksPerMillisecond;
		nano *= 100L;
		return nano;
	}

	// How to place the power plant?

	// It should be as close to as many buildings that are not close to a power plant...
	// It's not optimal, but I think it actually might be optimal for achieving what I want out of this- things to be spread out

	private void OnApplicationQuit()
	{
		map.Dispose();
		personMap.Dispose();
		disasterMap.Dispose();
		PresetHolder.Dispose();
		tilesTaken.Dispose();
		FluidSim.Dispose();
	}

	private static Entity[] trainStop = new Entity[10];

	public UnityEngine.Material dirFlow;
	void Start()
	{
		Variance.Init();


		tex = new Texture2D((int)Map.SIZE_X, (int)Map.SIZE_Z)
		{
			filterMode = FilterMode.Point
		};
		GetComponent<RenderInfo>().Init();

		op1Co = new Op1Co();

		map.Init();
		personMap.Init();
		disasterMap.Init();


		FluidSim.Init();

		tilesTaken = new NativeMultiHashMap<Entity, PFTile>(100, Allocator.Persistent);

		PresetHolder.Init();

		long start = NanoTime();

		for (ushort x = 0; x < Map.SIZE_X; x++)
		{
			for (ushort z = 0; z < Map.SIZE_Z; z++)
			{
				if ((x == FluidSim.WALL_PADDING || z == FluidSim.WALL_PADDING || x == Map.SIZE_X - FluidSim.WALL_PADDING - 1 || z == Map.SIZE_Z - FluidSim.WALL_PADDING - 1) && 
					x >= FluidSim.WALL_PADDING && z >= FluidSim.WALL_PADDING && x < Map.SIZE_X - FluidSim.WALL_PADDING && z < Map.SIZE_Z - FluidSim.WALL_PADDING)
				{
					ConstructionSystem.GetConstructor<WallConstructor>().AttemptSetWallHeight(new PFTile(x, 0, z), (z == 29 || z == 30 ? PFTile.HEIGHT * 13 : PFTile.HEIGHT * 15) - map.GetHeight(new PFTile(x, 0, z)));
				}
			}
		}

		DebugDraw.Log("Walls: ", start);

		/*for (int x = 0; x < 1; x++)
		{
			PFTile randomTile = new PFTile(10, 0, 10); // new PFTile((ushort)Variance.NextInt(map.size.x), 0, (ushort)Variance.NextInt(map.size.z));

			for (int i = 0; i < 2; i++)
			{
				randomTile.z += 7;
				PFNode node = new PFNode(randomTile, Dir.Back, PFR.RailNormal);
				trainStop[i] = ConstructionSystem.GetConstructor<TrainStationConstructor>().AttemptInitOnTiles(true,
					new Segment { segment = new PFSegment { from = node.PFNextNode(3), i = 3 }, to = node.PFNextNode(3).PFNextNode(3) }, new TrainStation { trainAtStop = Entity.Null }, new ResourceStorage { numResources = 0, maxResources = 100 });
				if (trainStop[i] != Entity.Null)
					trainStop[i].Modify((ref Constructing c) => c.progress = -1);
			}
		}*/

		for (ushort x = FluidSim.WALL_PADDING + 1; x < Map.SIZE_X - FluidSim.WALL_PADDING - 1; x++)
		{
			for (ushort z = FluidSim.WALL_PADDING + 1; z < Map.SIZE_Z - FluidSim.WALL_PADDING - 1; z++)
			{
				// World.Active.GetExistingSystem<NewPersonSystem>().SpawnPeople(new PFTile(x, map.GetHeightIndex(new PFTile(x, 0, z)), z), 10);

				if (Variance.Chance(0.1f) && map.GetHeightIndex(new PFTile(x, 0, z)) > 0)
					World.Active.GetExistingSystem<PersonSystem>().SpawnPeople(new PFTile(x, map.GetHeightIndex(new PFTile(x, 0, z)), z), Variance.Range(1, 9), new Savee { });
				else if (Variance.Chance(0.01f) && map.GetHeightIndex(new PFTile(x, 0, z)) > 0)
					World.Active.GetExistingSystem<PersonSystem>().SpawnPeople(new PFTile(x, map.GetHeightIndex(new PFTile(x, 0, z)), z), Variance.Range(1, 5), new Saver { state = Saver.State.Init });
				else if (Variance.Chance(0.01f) && map.GetHeightIndex(new PFTile(x, 0, z)) > 0)
					World.Active.GetExistingSystem<PersonSystem>().SpawnPeople(new PFTile(x, map.GetHeightIndex(new PFTile(x, 0, z)), z), Variance.Range(1, 5), new CameraOperator { });
			}
		}


		NativeArray<Color32> colorData = tex.GetRawTextureData<Color32>();
		for (ushort x = 0; x < Map.SIZE_X; x++)
		{
			for (ushort z = 0; z < Map.SIZE_Z; z++)
			{
				disasterMap.SetColor(colorData, new PFTile(x, 0, z));
			}
		}
		tex.Apply();
		((UnityEngine.Material)Resources.Load("MapMat")).mainTexture = tex;

		map.SetWalkRules();
		personMap.TestInit();

		GetComponent<ECSHandler>().FirstUpdate();
	}

	private void Update()
	{
		FluidSim.Update();
		op1Co.Update(transform);

		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Time.timeScale /= 2;
			Debug.Log("Time Scale: " + Time.timeScale);
		}
		if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			Time.timeScale *= 2;
			Debug.Log("Time Scale: " + Time.timeScale);
		}

		ObjectPlacer.Update();
	}

	private void OnPostRender()
	{
		World.Active.GetExistingSystem<WarningSystem>().OnPostRender();
	}

	private void FixedUpdate()
	{
		FluidSim.Tick();

		if (ECSHandler.tickCount % Mathf.RoundToInt(1 / GetTickTime()) == 0)
		{
			op1Co.SecondTick();
		}

		ObjectPlacer.FixedUpdate();
	}
}

public class Variance
{
	public static Unity.Mathematics.Random rand;
	// There are 
	private static bool init = false;

	public static void Init()
	{
		if (!init)
		{
			init = true;
			rand = new Unity.Mathematics.Random(833);
		}
	}

	// Random number from 0 <= r < max
	public static int NextInt(int max)
	{
		// return Math.Abs(rand.NextInt()) % max;
		return rand.NextInt(max);
	}

	public static int NextInt(uint max)
	{
		return rand.NextInt((int)max);
	}

	public static int NextInt()
	{
		return rand.NextInt();
	}

	public static double NextDouble()
	{
		return rand.NextDouble();
	}

	public static bool Chance(float chance)
	{
		return Range(0f, 1f) < chance;
	}

	// Inclusive to min, exclusive to max
	public static double Range(double min, double max)
	{
		return min + rand.NextDouble() * (max - min);
	}

	public static float Range(float min, float max)
	{
		return min + (float)rand.NextDouble() * (max - min);
	}

	// Inclusive to min, exclusive to max
	public static int Range(int min, int max)
	{
		return min + NextInt(max - min);
	}

	public static IEnumerable<int> GetRandomOrder(int length)
	{
		return Enumerable.Range(0, length).OrderBy(x => math.abs(rand.NextInt()));
	}
}