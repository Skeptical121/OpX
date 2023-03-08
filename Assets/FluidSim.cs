using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public static class FluidSim
{
	// So... I guess this runs entirely on the GPU

	public const int WALL_PADDING = 7;
	private const float OCEAN_HEIGHT = PFTile.HEIGHT * 14f;

	private static ComputeBuffer prev;
	private static ComputeBuffer next;

	// Mainly so velocity can be interpolated:
	private static ComputeBuffer waterTablePrev;
	private static ComputeBuffer waterTableNext;
	private static float[] ground; // Ground isn't effected by the water simulation

	// RenderTexture vel; // Interpolated? 

	private static RenderTexture minimapBase;
	private static ComputeShader minimapDisplay;
	private static int minimapUpdate;


	private static ComputeShader computeFlow;
	private static ComputeShader waterMesh;
	private static Material fluidShowMat;
	private static int flowUpdate;
	private static int depthUpdate;
	private static int waterMeshUpdate;
	private const int Size = (int)Map.SIZE_X; // Assumes square

	private static ComputeBuffer drawArgs;
	private static ComputeBuffer triangles;

	private static ComputeBuffer flowMap;

	private const int TICKS_PER_TICK = 5;
	private static float lastUpdateTime = 0;

	private static WaterTable[] waterTableBuffer; // Barring directly copying it to a nativearray, we have to use this for the inbetween copy

	private static int Index(int x, int z)
	{
		return x * Size + z;
	}

	public static void Init()
	{
		long startInit = Game.NanoTime();
		fluidShowMat = (Material)Resources.Load("FluidShowMat");

		computeFlow = (ComputeShader)Resources.Load("WaterFlowGen");
		flowUpdate = computeFlow.FindKernel("FlowUpdate");
		depthUpdate = computeFlow.FindKernel("DepthUpdate");

		waterMesh = (ComputeShader)Resources.Load("WaterMesh");
		waterMeshUpdate = waterMesh.FindKernel("CSMain");

		minimapDisplay = (ComputeShader)Resources.Load("MinimapDisplay");
		minimapUpdate = minimapDisplay.FindKernel("CSMain");

		Shader.SetGlobalFloat("_NaN", float.NaN);

		float4[] data = InitData();

		prev = new ComputeBuffer(Size * Size, 16);
		next = new ComputeBuffer(Size * Size, 16);
		prev.SetData(data);
		next.SetData(data);

		waterTablePrev = new ComputeBuffer(Size * Size, 32);
		waterTableNext = new ComputeBuffer(Size * Size, 32);

		minimapBase = new RenderTexture(Size, Size, 0);
		minimapBase.enableRandomWrite = true;
		minimapBase.Create();
		minimapDisplay.SetFloat("_Size", Size);
		GameObject.Find("Minimap").GetComponent<RawImage>().texture = minimapBase;

		flowMap = new ComputeBuffer(Size * Size, 8);

		triangles = new ComputeBuffer(Size * Size * 6, 28); //, ComputeBufferType.Append);

		drawArgs = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
		drawArgs.SetData(new int[] { Size * Size * 6, 1, 0, 0 });

		computeFlow.SetFloat("_TickTime", Game.GetTickTime() * TICKS_PER_TICK);
		computeFlow.SetFloat("_StabilityMultiplier", math.pow(0.95f, Game.GetTickTime() * TICKS_PER_TICK));
		computeFlow.SetInt("_Size", Size);
		computeFlow.SetFloat("_Length", PFTile.LENGTH);
		computeFlow.SetFloat("_OceanHeight", OCEAN_HEIGHT);

		waterMesh.SetInt("_Size", Size);
		waterMesh.SetFloat("_Length", PFTile.LENGTH);
		fluidShowMat.SetFloat("_Size", Size);
		fluidShowMat.SetFloat("_Length", PFTile.LENGTH);
		waterMesh.SetBuffer(waterMeshUpdate, "_FlowMap", flowMap);
		fluidShowMat.SetBuffer("_FlowMap", flowMap);

		waterTableBuffer = new WaterTable[Size * Size];
		DebugDraw.Log("FluidSim: ", startInit);
	}

	private static float4[] InitData()
	{
		ground = new float[Size * Size];
		float4[] data = new float4[Size * Size];
		for (ushort x = 0; x < Size; x++)
		{
			for (ushort z = 0; z < Size; z++)
			{
				PFTile tile = new PFTile(x, 0, z);
				ground[Index(x, z)] = Game.map.GetHeight(tile);
				data[Index(x, z)].x = Game.map.GetHeight(tile);
				if (ground[Index(x, z)] == 0)
				{
					data[Index(x, z)].y = 2f;
				}
				// if (x >= 2 && z >= 2 && x < Map.SIZE_X - 2 && z < Map.SIZE_Z - 2)
				if (x < WALL_PADDING || z < WALL_PADDING || x >= Map.SIZE_X - WALL_PADDING || z >= Map.SIZE_Z - WALL_PADDING)
				{
					data[Index(x, z)].y = OCEAN_HEIGHT - Game.map.GetHeight(tile);
				}
			}
		}
		return data;
	}

	/*struct Vertex
	{
		public float4 pos;
		public float3 normal;
		public float depth;

		public Vertex(int a, int b, int c, int d)
		{
			pos.x = System.BitConverter.ToSingle(System.BitConverter.GetBytes(a), 0);
			pos.y = System.BitConverter.ToSingle(System.BitConverter.GetBytes(b), 0);
			pos.z = System.BitConverter.ToSingle(System.BitConverter.GetBytes(c), 0);
			pos.w = System.BitConverter.ToSingle(System.BitConverter.GetBytes(d), 0);
			normal = new float3();
		}

		public override string ToString()
		{
			return "pos = " + pos + ", normal = " + normal;
		}
	}*/

	public static void Update()
	{
		// Graphics.DrawProceduralIndirect(waterMat, Map.GetBounds(), MeshTopology.Triangles, buffer);

		// material = new Material(waterMeshUpdate);

		waterMesh.SetBuffer(waterMeshUpdate, "_WaterTablePrev", waterTablePrev);
		waterMesh.SetBuffer(waterMeshUpdate, "_WaterTableNext", waterTableNext);
		// Debug.Log((Time.time - lastUpdateTime) / (Game.GetTickTime() * TICKS_PER_TICK));
		waterMesh.SetFloat("_DeltaTime", Time.deltaTime);
		waterMesh.SetFloat("_Interp", math.clamp((Time.time - lastUpdateTime) / (Game.GetTickTime() * TICKS_PER_TICK), 0, 1));

		// triangles.SetCounterValue(0);
		waterMesh.SetBuffer(waterMeshUpdate, "_VertBuffer", triangles);
		waterMesh.SetBuffer(waterMeshUpdate, "_FlowMap", flowMap);
		waterMesh.Dispatch(waterMeshUpdate, Size / 8, Size / 8, 1);
		// m_drawBuffer.SetBuffer("_Buffer", m_meshBuffer);
		// m_drawBuffer.SetPass(0);
		// waterMesh.SetPass(0);

		// Vertex[] drawV = new Vertex[Size * Size * 6 + 1];
		// buffer.GetData(drawV);
		// Graphics.DrawProcedural(waterMat, Map.GetBounds(), MeshTopology.Triangles, drawV.Length);

		// waterMat.SetPass
		// Graphics.Blit()

		// ComputeBuffer.CopyCount(points, drawArgs, 0);

		// ComputeBuffer.CopyCount(triangles, drawArgs, 0);

		// int[] args = { 0, 1, 0, 0 };
		// drawArgs.GetData(args);
		// Debug.Log(args[0] + ", " + args[1] + ", " + args[2] + ", " + args[3]);

		fluidShowMat.SetBuffer("_vertices", triangles);
		fluidShowMat.SetBuffer("_FlowMap", flowMap);
		// fluidShowMat.SetPass(0);
		Graphics.DrawProceduralIndirect(fluidShowMat, new Bounds(new Vector3(0, 0, 0), new Vector3(155050, 155050, 155050))/*Map.GetBounds()*/, MeshTopology.Triangles, drawArgs); // drawV.Length);


		// Minimap only needs to be updated like once a second or something...
		MinimapUpdate();



		// Vertex[] v = new Vertex[Size * Size * 6];
		// points.GetData(v);
		// Graphics.DrawProcedural(fluidShowMat, Map.GetBounds(), MeshTopology.Triangles, Size * Size * 6);


		/*if (Input.GetKeyDown(KeyCode.R))
		{
			Vertex[] vertices = new Vertex[Size * Size * 6];
			points.GetData(vertices);
			string str = "";
			for (int i = 0; i < 6; i++)
			{
				str += vertices[i] + "\n";
			}
			Debug.Log(str);
		}*/

		if (Input.GetKeyDown(KeyCode.F))
		{
			waterSimming = !waterSimming;
			Debug.Log("Simming water = " + waterSimming);
		}
	}

	private static void MinimapUpdate()
	{
		minimapDisplay.SetTexture(minimapUpdate, "_Result", minimapBase);
		minimapDisplay.SetBuffer(minimapUpdate, "_Next", next);
		minimapDisplay.Dispatch(minimapUpdate, Size / 8, Size / 8, 1);
	}

	public static void SetGround(PFTile tile, float groundHeight)
	{
		//float delta = groundHeight - ground[Index(tile.x, tile.z)];
		//if (delta > 0)
		//{
		//	depth[Index(tile.x, tile.z)] = math.max(0, depth[Index(tile.x, tile.z)] - delta); // Technically this removes water..
		//}
		prev.SetData(new float4[] { new float4(groundHeight, 0, 0, 0) }, 0, Index(tile.x, tile.z), 1);
		next.SetData(new float4[] { new float4(groundHeight, 0, 0, 0) }, 0, Index(tile.x, tile.z), 1);
		ground[Index(tile.x, tile.z)] = groundHeight;
	}

	private static bool waterSimming = true;

	public static void Tick()
	{
		if (waterSimming && ECSHandler.tickCount % TICKS_PER_TICK == 0)
		{
			// TODO_EFFICIENCY
			waterTableNext.GetData(waterTableBuffer); // This is slow
			Game.map.fluidMap.waterTable.CopyFrom(waterTableBuffer);

			lastUpdateTime = Time.time; // Game.GetGameTime();
			ComputeBuffer save = waterTablePrev;
			waterTablePrev = waterTableNext;
			waterTableNext = save;

			computeFlow.SetBuffer(flowUpdate, "_Prev", prev);
			computeFlow.SetBuffer(flowUpdate, "_Next", next);
			fluidShowMat.SetBuffer("_vertices", next);
			computeFlow.Dispatch(flowUpdate, Size / 8, Size / 8, 1);
			computeFlow.SetBuffer(depthUpdate, "_Next", prev);
			computeFlow.SetBuffer(depthUpdate, "_Prev", next);
			computeFlow.SetBuffer(depthUpdate, "_PrevWaterTable", waterTablePrev);
			computeFlow.SetBuffer(depthUpdate, "_OutBuffer", waterTableNext);
			computeFlow.Dispatch(depthUpdate, Size / 8, Size / 8, 1);



		}

		// RenderTexture save = prev;
		// prev = next;
		// next = save;
		// }

	}

	public static void Dispose()
	{
		prev.Dispose();
		next.Dispose();

		waterTablePrev.Dispose();
		waterTableNext.Dispose();

		triangles.Dispose();
		drawArgs.Dispose();

		flowMap.Dispose();
	}
}

// Raw copy from GPU memory

/*public struct WaterTable
{
	public float ground;
	public float depth;
	public float flowX;
	public float flowZ;
}*/

public struct WaterTable
{
	public float2x2 height;
	public float2 normalXZ;
	public float2 vel;
};