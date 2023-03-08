using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(RenderSystemGroup))]
public class WarningSystem : JobComponentSystemWithCallback
{
	List<Matrix4x4> warningMatrices;
	NativeQueue<Warning> warnings;
	Texture tex;
	// const int TICKS_PER_TICK = 5;

	bool warningsTick = true;
	const int MAX_K = 20;
	int wI = 0;

	NativeList<Warning> KMeans_warnings;
	NativeList<int> KMeans_closestCentroids;

	NativeArray<Warning> KMeans_centroids;
	NativeArray<Warning> KMeans_newCentroids;
	NativeArray<int> KMeans_centroidCounts;

	protected override void OnCreate()
	{
		base.OnCreate();
		warningMatrices = new List<Matrix4x4>();
		warnings = new NativeQueue<Warning>(Allocator.Persistent);
		// quad = UnityEngine.P.Quad

		KMeans_warnings = new NativeList<Warning>(Allocator.Persistent);
		KMeans_closestCentroids = new NativeList<int>(Allocator.Persistent);
		KMeans_centroids = new NativeArray<Warning>(MAX_K, Allocator.Persistent);
		KMeans_newCentroids = new NativeArray<Warning>(MAX_K, Allocator.Persistent);
		KMeans_centroidCounts = new NativeArray<int>(MAX_K, Allocator.Persistent);
	}

	public override void FirstUpdate()
	{
		base.FirstUpdate();
		tex = RenderInfo.Warning.mainTexture;
	}

	protected override void OnDestroy()
	{
		warnings.Dispose();
		KMeans_warnings.Dispose();
		KMeans_closestCentroids.Dispose();
		KMeans_centroids.Dispose();
		KMeans_newCentroids.Dispose();
		KMeans_centroidCounts.Dispose();
		base.OnDestroy();
	}

	public override bool IsRenderUpdate()
	{
		return true;
	}


	public struct Warning
	{
		public enum WarningType : byte
		{
			Hunger
		}

		public float3 pos;
		public float severity;
		public WarningType type;

		public Warning Add(Warning other)
		{
			pos += other.pos;
			severity += other.severity;
			return this;
		}
	}

	// Replaces old warnings...
	[BurstCompile]
	struct GetWarnings : IJobForEach<SimplePerson>
	{
		public NativeQueue<Warning>.ParallelWriter warnings;

		public void Execute([ReadOnly] ref SimplePerson person)
		{
			if (person.hunger > 75)
			{
				Warning warning = new Warning { pos = person.pos, type = Warning.WarningType.Hunger, severity = person.hunger };
				warnings.Enqueue(warning);
			}
		}
	}

	// [BurstCompile]
	struct KMeans : IJob
	{
		public NativeQueue<Warning> warningsQueue;

		public int k;
		public NativeArray<Warning> warnings;
		public NativeArray<int> closestCentroids;

		public NativeArray<Warning> centroids;
		public NativeArray<Warning> newCentroids;
		public NativeArray<int> centroidCounts;

		// Assumes k <= pos
		public void Execute()
		{
			// Unpack from queue...
			int index = 0;
			while (warningsQueue.TryDequeue(out Warning warning))
			{
				warnings[index++] = warning;
			}

			for (int i = 0; i < k; i++)
			{
				centroids[i] = warnings[i];
			}

			// 20 chances to converge...
			for (int iter = 0; iter < 20; iter++)
			{
				// Update who's closest...
				for (int p = 0; p < warnings.Length; p++)
				{
					float bestDistSqr = float.MaxValue;
					for (int i = 0; i < k; i++)
					{
						float distSqr = math.distancesq(warnings[p].pos, centroids[i].pos);
						if (distSqr < bestDistSqr)
						{
							bestDistSqr = distSqr;
							closestCentroids[p] = i;
						}
					}
				}

				// Update centroids
				for (int p = 0; p < warnings.Length; p++)
				{
					newCentroids[closestCentroids[p]] = newCentroids[closestCentroids[p]].Add(warnings[p]);
					centroidCounts[closestCentroids[p]]++;
				}

				bool allSame = true;
				for (int i = 0; i < k; i++)
				{
					Warning w = newCentroids[i];
					w.pos /= centroidCounts[i]; // At least one should be closest?
					newCentroids[i] = w;
					// Can divide severity if we want that to be averaged...
					if (!newCentroids[i].pos.Equals(centroids[i].pos))
						allSame = false;

					centroids[i] = new Warning { type = w.type };
					centroidCounts[i] = 0;
				}
				NativeArray<Warning> save = centroids;
				centroids = newCentroids;
				newCentroids = save;

				if (allSame)
					break; // Done
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		if ((wI++) % 50 == 0)
		{
			warnings.Clear();
			inputDeps = new GetWarnings
			{
				warnings = warnings.AsParallelWriter()
			}.Schedule(this, inputDeps);
		}
		else
		{
			int k = math.min(MAX_K, warnings.Count);
			KMeans_warnings.ResizeUninitialized(warnings.Count);
			KMeans_closestCentroids.ResizeUninitialized(warnings.Count);
			inputDeps = new KMeans
			{
				k = k,
				warningsQueue = warnings,
				warnings = KMeans_warnings,
				closestCentroids = KMeans_closestCentroids,
				centroids = KMeans_centroids,
				newCentroids = KMeans_newCentroids,
				centroidCounts = KMeans_centroidCounts
			}.Schedule(inputDeps);
		}
		warningsTick = !warningsTick;
		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
	}

	public void OnPostRender()
	{

		// warningMatrices.Clear();
		Camera camera = Camera.main; // TODO_EFFICIENCY remove references to Camera.main
		Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);


		GL.PushMatrix();
		GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

		float width = 10;
		float height = 20;

		// while (warnings.TryDequeue(out Warning warning))
		// {
		//if (warningMatrices.Count > 1000)
		//	break;

		for (int i = 0; i < KMeans_centroids.Length; i++)
		{
			Warning warning = KMeans_centroids[i];
			float3 warningPos = warning.pos + new float3(0, 2.5f, 0);
			Vector3 screenPos = camera.WorldToScreenPoint(warningPos);
			if (screenPos.z > 0 && screenPos.x >= -width * 0.5f && screenPos.y >= -height * 0.5f && screenPos.x <= Screen.width + width * 0.5f && screenPos.y <= Screen.height + height * 0.5f)
				Graphics.DrawTexture(new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height), tex);
		}
			/*Vector3 delta = camera.transform.position - (Vector3)warningPos;
			delta.y = 0;
			Matrix4x4 matrix = Matrix4x4.TRS(warningPos, Quaternion.LookRotation(delta), new Vector3(2, 2, 2));
			if (GeometryUtility.TestPlanesAABB(planes, GeometryUtility.CalculateBounds(RenderInfo.warningObject.vertices, matrix)))
				warningMatrices.Add(matrix);*/
		// }
		GL.PopMatrix();
		// Graphics.DrawMeshInstanced(RenderInfo.warningObject, 0, RenderInfo.Warning, warningMatrices);
		// warnings.Clear();
	}
}
