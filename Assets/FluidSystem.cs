using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// https://tutcris.tut.fi/portal/files/4312220/kellomaki_1354.pdf
// Page 50
/*
[UpdateInGroup(typeof(MainSimSystemGroup))]
public class FluidSystem : JobComponentSystemWithCallback
{
	// Fluid system...
	public NativeArray<float> ground;
	public NativeArray<float> depth;
	public NativeArray<float> flowX;
	public NativeArray<float> flowZ;
	public NativeArray<float2> texOffset;
	public Mesh mesh;
	// public Mesh groundMesh;


	bool firstTick = true;
	public const float MIN_DISPLAY_WATER_HEIGHT = 0.01f;
	public const float OCEAN_HEIGHT = 9.5f;
	public const int WALL_PADDING = 3;

	public const int TICKS_PER_TICK = 3636363; // 10;
	public const float LENGTH = 10f;
	public const float DEPTH_MULT = 10f;

	public void Init()
	{
		for (ushort x = 0; x < Map.SIZE_X; x++)
		{
			for (ushort z = 0; z < Map.SIZE_Z; z++)
			{
				PFTile tile = new PFTile(x, 0, z);
				ground[Index(x, z)] = Game.map.GetWalkingHeight(tile) * DEPTH_MULT;
				if (ground[Index(x, z)] == 0)
				{
					depth[Index(x, z)] = 0.5f * DEPTH_MULT;
				}
				// if (x >= 2 && z >= 2 && x < Map.SIZE_X - 2 && z < Map.SIZE_Z - 2)
				if (x < WALL_PADDING || z < WALL_PADDING || x >= Map.SIZE_X - WALL_PADDING || z >= Map.SIZE_Z - WALL_PADDING)
				{
					depth[Index(x, z)] = (OCEAN_HEIGHT - Game.map.GetWalkingHeight(tile)) * DEPTH_MULT;
				}

				texOffset[Index(x, z)] = new float2(x * LENGTH, z * LENGTH);

			}
		}
	}

	public void SetGround(PFTile tile, float groundHeight)
	{
		groundHeight *= DEPTH_MULT;
		float delta = groundHeight - ground[Index(tile.x, tile.z)];
		if (delta > 0)
		{
			depth[Index(tile.x, tile.z)] = math.max(0, depth[Index(tile.x, tile.z)] - delta); // Technically this removes water..
		}
		ground[Index(tile.x, tile.z)] = groundHeight;
	}

	protected override void OnCreate()
	{
		base.OnCreate();

		int numFluidCells = (int)(Map.SIZE_X * Map.SIZE_Z);

		ground = new NativeArray<float>(numFluidCells, Allocator.Persistent);
		depth = new NativeArray<float>(numFluidCells, Allocator.Persistent);
		flowX = new NativeArray<float>(numFluidCells, Allocator.Persistent);
		flowZ = new NativeArray<float>(numFluidCells, Allocator.Persistent);
		texOffset = new NativeArray<float2>(numFluidCells, Allocator.Persistent);

		// waterExists = new NativeList<int2>(100, Allocator.Persistent);


		// for (ushort x = 0; x < Map.SIZE_X; x++)
		// {
		// 	for (ushort z = 0; z < Map.SIZE_Z; z++)
		// 	{
		// 		depth[(int)(x * Map.SIZE_Z + z)] = x < 32 ? 0 : 3;
		// 		ground[(int)(x * Map.SIZE_Z + z)] = x == 30 || x == 31 ? 4 : 1;
		// 	}
		// }
		// ground[(int)(30 * Map.SIZE_Z + 30)] = 2.5f;
		// ground[(int)(31 * Map.SIZE_Z + 30)] = 2.5f;
		// ground[(int)(30 * Map.SIZE_Z + 31)] = 2.5f;
		// ground[(int)(31 * Map.SIZE_Z + 31)] = 2.5f;
		// depth[(int)(60 * Map.SIZE_Z + 60)] = 500f;

		// Vector3[] vertices = new Vector3[Map.SIZE_X * Map.SIZE_Z * 4];// new Vector3[(Map.SIZE_X + 1) * (Map.SIZE_Z + 1)];
		// Vector3[] normals = new Vector3[Map.SIZE_X * Map.SIZE_Z * 4];// new Vector3[(Map.SIZE_X + 1) * (Map.SIZE_Z + 1)];
		// for (int i = 0; i < normals.Length; i++)
		// {
		// 	normals[i] = Vector3.up;
		// }

		//for (ushort x = 0; x < (Map.SIZE_X + 1); x++)
		//{
		//for (ushort z = 0; z < (Map.SIZE_Z + 1); z++)
		//{
		// float highestGround = math.max(math.max(ground[Index(math.max(0, x - 1), math.max(0, z - 1))], ground[Index(math.max(0, x - 1), math.min((int)(Map.SIZE_Z - 1), z))]),
		//	   math.max(ground[Index(math.min((int)(Map.SIZE_X - 1), x), math.max(0, z - 1))], ground[Index(math.max((int)(Map.SIZE_X - 1), x), math.max((int)(Map.SIZE_Z - 1), z))]));
		// vertices[x * (Map.SIZE_Z + 1) + z] = Map.WorldPosition(x, highestGround, z);
		//normals[x * (Map.SIZE_Z + 1) + z] = Vector3.up;
		//}
		//}

		// int[][] triangles = new int[(Map.SIZE_X - 1) * (Map.SIZE_Z - 1)][];
		// int i = 0;
		// for (ushort x = 0; x < Map.SIZE_X - 1; x++)
		// {
		// 	for (ushort z = 0; z < Map.SIZE_Z - 1; z++)
		// 	{
		//		triangles[i++] = MeshCreator.RectTri((int)(x * Map.SIZE_Z + z), (int)(x * Map.SIZE_Z + z + 1), (int)((x + 1) * Map.SIZE_Z + z), (int)((x + 1) * Map.SIZE_Z + z + 1));
		//	}
		//}

		// triangles set in update..

		mesh = new Mesh();
		//{
		//	vertices = vertices,
		//	normals = normals,
		//	triangles = new int[0]
		//};

		//groundMesh = new Mesh
		//{
		//	vertices = v,
		//	normals = normals,
		//	triangles = MeshCreator.MergeArrays(triangles)
		//};
	}

	protected override void OnDestroy()
	{
		ground.Dispose();
		depth.Dispose();
		flowX.Dispose();
		flowZ.Dispose();
		texOffset.Dispose();
		base.OnDestroy();
	}

	private static int Index(int x, int z)
	{
		return (int)(x * Map.SIZE_X + z);
	}

	//struct FluidSimulation2 : IJob
	//{
	//	public NativeArray<float> ground;
	//	public NativeArray<float> depth;
	//
	//	public NativeArray<float2> vel;
	//
	//	public void Execute()
	//	{
	//		// Depth integration
	//		for (int x = 0; x < Map.SIZE_X; x++)
	//		{
	//			for (int z = 0; z < Map.SIZE_Z; z++)
	//			{
	//				// Velocity must be < LENGTH / tickTime
	//				depth[Index(x, z)] += (vel[Index(x, z)].x) / LENGTH;
	//				float2 negVel = new float2(vel[Index(x - 1, z)].x, vel[Index(x, z - 1)].y);
	//				float2 posVel = vel[Index(x, z)];
	//
	//				float change = (GetUpwindDepth(x, z, negVel.x) * negVel.x)
	//			}
	//		}

			// Velocity advection


			// Velocity integration
		//}

		//private float GetUpwindDepth(int x, int z, float vel)
		//{
			//if (vel > 0) return depth[Index(x, z)]; else return depth2;
		//}
	//}

	[BurstCompile]
	struct FluidSimulation : IJob
	{

		public float tickTime;
		public float gravity;

		public NativeArray<float> ground;
		public NativeArray<float> depth;

		// In positive direction
		public NativeArray<float> flowX;
		public NativeArray<float> flowZ;

		private float WaterHeight(int x, int z)
		{
			return ground[Index(x, z)] + depth[Index(x, z)];
		}

		public void Execute()
		{
			// Update depths:
			for (int x = 0; x < Map.SIZE_X; x++)
			{
				for (int z = 0; z < Map.SIZE_Z; z++)
				{
					int index = Index(x, z);

					if (x == 0 || z == 0 || x == Map.SIZE_X - 1 || z == Map.SIZE_Z - 1)
					{
						depth[Index(x, z)] = OCEAN_HEIGHT * DEPTH_MULT - ground[Index(x, z)];
					}
					else
					{
						depth[index] = NewDepth(index, x, z);
					}

					//if (depth[index] >= 0.01f)
					//{
					// waterExists.Add(new int2(x, z));
					//}
				}
			}

			// Flow update:
			for (int x = 0; x < Map.SIZE_X; x++)
			{
				for (int z = 0; z < Map.SIZE_Z; z++)
				{
					int index = Index(x, z);

					if (x < Map.SIZE_X - 1)
					{
						float deltaHeight = WaterHeight(x + 1, z) - WaterHeight(x, z);
						if (flowX[Index(x, z)] == 0)
							flowX[Index(x, z)] = deltaHeight > 0 ? 0.00000001f : -0.00000001f;
						float upwindDepthX = 1f; // GetUpwindDepth(depth[Index(x, z)], depth[Index(x + 1, z)], -flowX[Index(x, z)]);//1f;//math.max(depth[Index(x + 1, z)], depth[index]);// math.max(1f, (flowX[index] <= 0 ? depth[Index(x + 1, z)] : depth[index]));
						flowX[index] = (flowX[index] + upwindDepthX * LENGTH * (gravity / LENGTH) * deltaHeight * tickTime) * 0.995f; // Some friction
					}

					if (z < Map.SIZE_Z - 1)
					{
						float deltaHeight = WaterHeight(x, z + 1) - WaterHeight(x, z);
						if (flowZ[Index(x, z)] == 0)
							flowZ[Index(x, z)] = deltaHeight > 0 ? 0.00000001f : -0.00000001f;
						float upwindDepthZ = 1f; // GetUpwindDepth(depth[Index(x, z)], depth[Index(x, z + 1)], -flowZ[Index(x, z)]);//1f;//math.max(depth[Index(x, z + 1)], depth[index]);// math.max(1f, (flowZ[index] <= 0 ? depth[Index(x, z + 1)] : depth[index]));
						flowZ[index] = (flowZ[index] + upwindDepthZ * LENGTH * (gravity / LENGTH) * deltaHeight * tickTime) * 0.995f;
					}
				}
			}

			// Limiting step:
			for (int x = 0; x < Map.SIZE_X; x++)
			{
				for (int z = 0; z < Map.SIZE_Z; z++)
				{
					LimitRecursively(x, z);
				}
			}

			
		}

		private void LimitRecursively(int x, int z)
		{
			int index = Index(x, z);
			float newDepth = NewDepth(index, x, z);
			if (newDepth < 0)
			{
				// Limit flow for negative flows only


				float scale = 0.9999f * depth[index] / (depth[index] - newDepth);
				if (flowX[index] < 0)
					flowX[index] *= scale;
				if (flowZ[index] < 0)
					flowZ[index] *= scale;
				if (x > 0 && flowX[Index(x - 1, z)] > 0)
				{
					flowX[Index(x - 1, z)] *= scale;
					LimitRecursively(x - 1, z);
				}
				if (z > 0 && flowZ[Index(x, z - 1)] > 0)
				{
					flowZ[Index(x, z - 1)] *= scale;
					LimitRecursively(x, z - 1);
				}
			}
		}

		private float NewDepth(int index, int x, int z)
		{
			float extra = (x == 0 ? 0 : flowX[Index(x - 1, z)]) + (z == 0 ? 0 : flowZ[Index(x, z - 1)]);

			return depth[index] + tickTime * (flowX[index] + flowZ[index] - extra) / (LENGTH * LENGTH);
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		inputDeps = new FluidSimulation
		{
			tickTime = Game.GetTickTime() * TICKS_PER_TICK,
			gravity = 9.8f,
			ground = ground,
			depth = depth,
			flowX = flowX,
			flowZ = flowZ
		}.Schedule(inputDeps);


		return base.OnUpdate(inputDeps);
	}


	private float2 GetAverageDepth(float heightPlusDepth, int x, int z)
	{
		float depthSum = 0;
		float groundSum = 0;
		int count = 0;
		for (int x2 = math.max(0, x - 1); x2 < x + 1; x2++)
		{
			for (int z2 = math.max(0, z - 1); z2 < z + 1; z2++)
			{
				if (x2 < Map.SIZE_X && z2 < Map.SIZE_Z && heightPlusDepth >= ground[Index(x2, z2)])
				{
					depthSum += depth[Index(x2, z2)];
					groundSum += ground[Index(x2, z2)];
					count++;
				}
			}
		}
		return new float2(depthSum / count, groundSum / count);
		// return (depth[Index(math.max(0, x - 1), math.max(0, z - 1))] + depth[Index(math.max(0, x - 1), math.min((int)(Map.SIZE_Z - 1), z))] +
		// 			depth[Index(math.min((int)(Map.SIZE_X - 1), x), math.max(0, z - 1))] + depth[Index(math.min((int)(Map.SIZE_X - 1), x), math.min((int)(Map.SIZE_Z - 1), z))]) / 4f;
	}

	private float GetHighestGround(ushort x, ushort z)
	{
		return math.max(math.max(ground[Index(math.max(0, x - 1), math.max(0, z - 1))], ground[Index(math.max(0, x - 1), math.min((int)(Map.SIZE_Z - 1), z))]),
					math.max(ground[Index(math.min((int)(Map.SIZE_X - 1), x), math.max(0, z - 1))], ground[Index(math.min((int)(Map.SIZE_X - 1), x), math.min((int)(Map.SIZE_Z - 1), z))]));
	}

	private float WaterHeight(int x, int z)
	{
		return ground[Index(x, z)] + depth[Index(x, z)];
	}

	private float3 WaterPos(int x, int z)
	{
		return Map.WorldPosition(x, WaterHeight(x, z), z);
	}

	// Upwind = where the water is coming from
	private static float GetUpwindDepth(float depth1, float depth2, float flow1to2)
	{
		if (flow1to2 > 0) return depth1; else return depth2;
	}

	public override void MainThreadSimulationCallbackTick()
	{
		if (firstTick)
		{
			GameObject.Find("Water").GetComponent<MeshFilter>().sharedMesh = mesh;
			// GameObject.Find("Ground").GetComponent<MeshFilter>().sharedMesh = groundMesh;
			firstTick = false;
		}

		if (ECSHandler.tickCount % TICKS_PER_TICK == 0)
		{
			NativeArray<float>.Copy(depth, Game.map.depth);
			// Don't update flow for back left edge... easier

			// NativeArray<float>.Copy(flowX, Game.map.flowX);
			// NativeArray<float>.Copy(flowZ, Game.map.flowZ);
			//for (ushort x = 1; x < Map.SIZE_X; x++)
			//{
			//	for (ushort z = 1; z < Map.SIZE_Z; z++)
			//	{
			//		Game.map.velocity[Index(x, z)] = new float2(-flowX[Index(x - 1, z)] - flowX[Index(x, z)], -flowZ[Index(x, z - 1)] - flowZ[Index(x, z)]) / (LENGTH * depth[Index(x, z)] * 2);
			//	}
			//}
			for (ushort x = 0; x < Map.SIZE_X - 1; x++)
			{
				for (ushort z = 0; z < Map.SIZE_Z - 1; z++)
				{
					float depthX = GetUpwindDepth(depth[Index(x, z)], depth[Index(x + 1, z)], -flowX[Index(x, z)]);
					float depthZ = GetUpwindDepth(depth[Index(x, z)], depth[Index(x, z + 1)], -flowZ[Index(x, z)]);
					if (depthX > 0 && depthZ > 0)
					{
						Game.map.velocityPipes[Index(x, z)] =
							new float2(-flowX[Index(x, z)] / (LENGTH * depthX),
							-flowZ[Index(x, z)] / (LENGTH * depthZ));
					}

				}
			}
			for (ushort x = 0; x < Map.SIZE_X; x++)
			{
				for (ushort z = 0; z < Map.SIZE_Z; z++)
				{

					//if (depth[Index(x, z)] > 0)
					//	texOffset[Index(x, z)] -= Game.map.velocityPipes[Index(x, z)] * Game.GetTickTime() * TICKS_PER_TICK;
					texOffset[Index(x, z)] += new float2(0.1f, 0.1f); // Game.map.GetVelocity(Map.WorldPosition(new PFTile(x, 0, z))) * 0.01f;
				}
			}


			// Vector3[] vertices = mesh.vertices;
			List<Vector3> vertices = new List<Vector3>();
			List<Vector2> uv = new List<Vector2>();
			List<int> triangles = new List<int>();*/

			/*List<float3x4> rects = new List<float3x4>();

			NativeHashMap<int2, int> v = new NativeHashMap<int2, int>(100, Allocator.Temp);
			// Populate previousVertices:

			for (ushort x = 0; x < Map.SIZE_X - 1; x++)
			{
				for (ushort z = 0; z < Map.SIZE_Z - 1; z++)
				{
					if (depth[Index(x, z)] >= MIN_DISPLAY_WATER_HEIGHT)
					{
						// Display vertex:
						v[new int2(x * 2, z * 2)] = vertices.Count;
						vertices.Add(WaterPos(x, z));

						float3 a = WaterPos(x, z);

						// Check line:
						if (depth[Index(x + 1, z)] < MIN_DISPLAY_WATER_HEIGHT)
						{
							float3 b = Map.WorldPosition(x + 0.5f, WaterHeight(x, z), z);
							


						}
						else
						{
							float mid = (ground[Index(x, z)] + depth[Index(x + 1, z)]) / 2f;
							if (mid <= math.max(ground[Index(x, z)], ground[Index(x + 1, z)]))
							{
								float3 b = Map.WorldPosition(x + 0.5f, WaterHeight(x, z), z);
								float3 a2 = Map.WorldPosition(x + 0.5f, WaterHeight(x + 1, z), z);
								float3 b2 = WaterPos(x + 1, z);
							}
							else
							{
								float3 b = WaterPos(x + 1, z);
							}
						}
					}
				}
			}

			for (ushort x = 0; x < Map.SIZE_X - 1; x++)
			{
				for (ushort z = 0; z < Map.SIZE_Z - 1; z++)
				{
					if (v.ContainsKey(new int2(x * 2, z * 2)))
					{
						if (v.ContainsKey(new int2(x * 2 + 1, z * 2)))
						{
							
						}
					}
				}
			}*/



			/*for (ushort x = 0; x < Map.SIZE_X; x++)
			{
				for (ushort z = 0; z < Map.SIZE_Z; z++)
				{
					bool isValid = false;
					for (int x2 = x - 1; x2 < x + 2; x2++)
					{
						for (int z2 = z - 1; z2 < z + 2; z2++)
						{
							if (!isValid && x2 >= 0 && z2 >= 0 && x2 < Map.SIZE_X && z2 < Map.SIZE_Z && 
								depth[Index(x2, z2)] >= MIN_DISPLAY_WATER_HEIGHT && ground[Index(x2, z2)] + depth[Index(x2, z2)] >= ground[Index(x, z)] + MIN_DISPLAY_WATER_HEIGHT)
							{
								isValid = true;
								break;
							}
						}
					}

					if (isValid)
					{
						MeshCreator.RectTri(triangles, vertices.Count, vertices.Count + 1, vertices.Count + 2, vertices.Count + 3);
						for (ushort x2 = x; x2 < x + 2; x2++)
						{
							for (ushort z2 = z; z2 < z + 2; z2++)
							{
								// Obviously this needs to be adjusted in terms of height for other cases
								float2 depthGround = GetAverageDepth(ground[Index(x, z)] + depth[Index(x, z)], x2, z2) / DEPTH_MULT;
								vertices.Add(Map.WorldPosition(x2, math.max(ground[Index(x, z)] / DEPTH_MULT + MIN_DISPLAY_WATER_HEIGHT, depthGround.x + depthGround.y), z2));
								uv.Add(texOffset[Index(math.min((int)Map.SIZE_X - 1, x2), math.min((int)Map.SIZE_Z - 1, z2))] / 25f);
							}
						}
					}

				}
			}

			Vector3[] normals = new Vector3[vertices.Count];
			for (int i = 0; i < normals.Length; i++)
			{
				normals[i] = Vector3.up;
			}

			//List<int> triangles = new List<int>();
			//for (ushort x = 0; x < Map.SIZE_X; x++)
			//{
			//	for (ushort z = 0; z < Map.SIZE_Z; z++)
			//	{
			//		if (depth[Index(x, z)] >= 0.01f)
			//		{
			//			MeshCreator.RectTri(triangles, (int)(x * (Map.SIZE_Z + 1) + z), (int)(x * (Map.SIZE_Z + 1) + z + 1), (int)((x + 1) * (Map.SIZE_Z + 1) + z), (int)((x + 1) * (Map.SIZE_Z + 1) + z + 1));
			//		}
			//	}
			//}

			if (vertices.Count < mesh.vertexCount)
			{
				mesh.triangles = new int[0];
			}
			mesh.vertices = vertices.ToArray();
			mesh.normals = normals;
			mesh.triangles = triangles.ToArray();
			mesh.uv = uv.ToArray();

			//int[] triangles = new int[waterExists.Length * 6];
			//
			//for (int i = 0; i < waterExists.Length; i++)
			//{
			//	int x = waterExists[i].x;
			//	int z = waterExists[i].y;
			//	MeshCreator.RectTri(triangles, i * 6, (int)(x * Map.SIZE_Z + z), (int) (x * Map.SIZE_Z + z + 1), (int) ((x + 1) * Map.SIZE_Z + z), (int) ((x + 1) * Map.SIZE_Z + z + 1));
			//}


			mesh.RecalculateBounds();

			if (Input.GetKey(KeyCode.X))
			{
				Debug.Log("X Pressed: " + 3.0f / 0.0f + ", " + 0.0f / float.MinValue + ", " + 0.0f + ", " + depth[0] + ", " + ground[0] + ", " + vertices[0] + ", " + flowX[0] + ", " + flowZ[0]);
			}
		}

		// waterExists.Clear();

		
	}
}
*/