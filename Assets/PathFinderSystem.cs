using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct PathFindRequest
{
	public PFNode endNode;
	public int endEntity;
	public float3 endPos;
	public PathFindRequest(PFNode endNode, float3 endPos)
	{
		this.endNode = endNode;
		endEntity = -1;
		this.endPos = endPos;
	}
	public PathFindRequest(int endEntity, float3 endPos)
	{
		endNode = PFNode.Invalid;
		this.endEntity = endEntity;
		this.endPos = endPos;
	}
}

public static class PathFinder
{
	private static Entity fakePlacingContainer = Entity.Null;
	public static void BuildRoute<T>(PFNode from, PFNode to, bool fake = false) where T : Constructor, new()
	{
		BuildRoute<T>(new List<PFNode> { from }, new List<PFNode> { to }, to.ConnectionPoint(), fake);
	}

	public static void BuildRoute<T>(List<PFNode> fromBorder, List<PFNode> toBorder, float3 to, bool fake = false) where T : Constructor, new()
	{
		PathFinderSystem.BuildPathFinder.Schedule(fromBorder, toBorder, to, BuildRule.Rail, (path) =>
			{
				if (!fake)
				{
					Entity entity = Entity.Null;
					T railConstructor = ConstructionSystem.GetConstructor<T>();
					for (int i = 0; i < path.Count - 1; i++)
					{
						// Note the path might not still be valid...
						entity = railConstructor.AttemptInitOnTiles(true, new Segment { segment = path[i], to = path[i + 1].from });
						if (entity != Entity.Null)
							entity.Modify((ref Constructing c) => c.progress = -1);
					}
				}
				else
				{
					DestroyFakeContainer();
					fakePlacingContainer = ECSExtensions.EntityManager.CreateEntity(typeof(SubMeshRenderer));
					for (int i = 0; i < path.Count - 1; i++)
					{
						PFSegment segment = path[i];
						fakePlacingContainer.AddSubRenderer(segment.from.ConnectionPoint(), segment.from.dir.Rotation(), PresetHolder.GetPreset(segment.from.pfr, segment.i).mesh, true);
					}
				}
			});
	}

	public static void PersonPathFind(Entity person, PFTile to)
	{
		Game.numPersonPathFinds++;
		PathFinderSystem.BuildPathFinder.Schedule(new List<PFNode> { new PFNode(Map.GetTile(person.Get<SimplePerson>().pos), Dir.Down, PFR.Person) }, 
			new List<PFNode> { new PFNode(to, Dir.Down, PFR.Person) }, Map.WorldPosition(to), BuildRule.Normal, (path) =>
		{
			if (path.Count > 0)
			{
				NativeArray<PersonPathPos> arrayPath = new NativeArray<PersonPathPos>(path.Count - 1, Allocator.Temp);
				for (int i = 1; i < path.Count; i++)
				{
					arrayPath[i - 1] = new PersonPathPos { goal = Map.WorldPosition(path[i].from.tile) };
				}
				person.Buffer<PersonPathPos>().AddRange(arrayPath);
				person.Modify((ref ComplexPerson p) => p.pathfinding = false);
			}
		});
	}

	public static void WallPathFind(PFTile from, PFTile to)
	{
		PathFinderSystem.BuildPathFinder.Schedule(new List<PFNode> { new PFNode(from, Dir.Down, PFR.Wall) },
			new List<PFNode> { new PFNode(to, Dir.Down, PFR.Wall) }, Map.WorldPosition(to), BuildRule.Normal, (path) =>
			{
				for (int i = 0; i < path.Count; i++)
				{
					if (path[i].from.tile.IsValid())
						ConstructionSystem.GetConstructor<WallConstructor>().AttemptAddWallHeight(path[i].from.tile, 8f);
				}
			});
	}

	public static void TrainRoute(Entity train, Entity fromRail, Entity toRail)
	{
		// Using segment.from would underestimate the H (heuristic) value when you are close...
		PathFinderSystem.TrainPathFinder.Schedule(new List<TrainEntity> { new TrainEntity { entity = fromRail } }, 
			new List<TrainEntity> { new TrainEntity { entity = toRail } }, toRail.Get<Segment>().to.ConnectionPoint(), BuildRule.Normal, (path) =>
		{
			if (path.Count > 0)
			{
				train.Buffer<TrainPath>().RemoveAt(train.Buffer<TrainPath>().Length - 1);
				train.Buffer<TrainPath>().AddRange(new NativeArray<TrainEntity>(path.ToArray(), Allocator.Temp).Reinterpret<TrainEntity, TrainPath>());
			}
			// Keep trying?
			train.Modify((ref Train t) => t.pathfinding = false);
		});
	}

	public static void DestroyFakeContainer()
	{
		if (fakePlacingContainer != Entity.Null)
		{
			NativeArray<SubMeshRenderer> renderers = fakePlacingContainer.Buffer<SubMeshRenderer>().ToNativeArray(Allocator.Temp);
			for (int i = 0; i < renderers.Length; i++)
			{
				ECSExtensions.EntityManager.DestroyEntity(renderers[i].renderer);
			}
			ECSExtensions.EntityManager.DestroyEntity(fakePlacingContainer);
			fakePlacingContainer = Entity.Null;
		}
	}
}

public class Request<S, U, T> where S : struct, PathFinderSystem.IPathFindSection<T> where U : struct, IEquatable<U> where T : struct, PathFinderSystem.IPathFind<U, T>
{
	public Action<List<S>> callback;
	public List<T> startNodes;
	public List<T> endNodes;
	public float3 endPos;
	public BuildRule rule;
}



[UpdateInGroup(typeof(MainSimSystemGroup))]
public class PathFinderSystem : JobComponentSystemWithCallback
{
	public class PathFindType<S, U, T> where S : struct, IPathFindSection<T> where U : struct, IEquatable<U> where T : struct, IPathFind<U, T>
	{
		struct PathFindJobBox
		{
			public PathFindJob<S, U, T> pathFindJob;
			public JobHandle jobHandle;
			public Request<S, U, T> request;
		}
		private List<PathFindJob<S, U, T>> jobs = new List<PathFindJob<S, U, T>>();
		private List<PathFindJobBox> pathFindJobs = new List<PathFindJobBox>();


		public Queue<Request<S, U, T>> queuedRequests = new Queue<Request<S, U, T>>();
		const int iterationLimit = 5000;
		const int maxPathFindJobsPerTick = 5;

		public void Schedule(List<T> from, List<T> to, float3 toPos, BuildRule rule, Action<List<S>> callback = null)
		{
			queuedRequests.Enqueue(new Request<S, U, T> { callback = callback, startNodes = from, endNodes = to, endPos = toPos, rule = rule });
		}

		public void Init()
		{
			for (int i = 0; i < maxPathFindJobsPerTick; i++)
			{
				// int numPFNodes = Game.map.sectionStartIndex[Game.map.sectionStartIndex.Length - 1] * MapInfo.MAX_DIRS * (byte)PFR.MAX_CONNECTION_TYPES;
				jobs.Add(new PathFindJob<S, U, T>
				{
					map = Game.map, // Copy map.. but not entirely
					path = new NativeList<S>(Allocator.Persistent),
					gScore = new NativeHashMap<U, float>(10000, Allocator.Persistent),
					cameFrom = new NativeHashMap<U, S>(10000, Allocator.Persistent),
					openSet = new Heap<T>(100000),
					startNodes = new NativeList<T>(Allocator.Persistent),
					endNodes = new NativeList<T>(Allocator.Persistent),
					tilesTaken = new NativeList<PFTile>(Allocator.Persistent),
					outIterationLimit = new NativeArray<int>(1, Allocator.Persistent)
				});
			}
		}

		public void Update(JobComponentSystem parent, NativeList<JobHandle> jobHandles, JobHandle inputDeps)
		{
			for (int i = 0; i < maxPathFindJobsPerTick && queuedRequests.Count > 0; i++)
			{
				Request<S, U, T> request = queuedRequests.Dequeue();
				jobs[i].startNodes.Clear();
				for (int n = 0; n < request.startNodes.Count; n++)
				{
					jobs[i].startNodes.Add(request.startNodes[n]);
				}
				jobs[i].endNodes.Clear();
				for (int n = 0; n < request.endNodes.Count; n++)
				{
					jobs[i].endNodes.Add(request.endNodes[n]);
				}

				PathFindJob<S, U, T> pathFindJob = new PathFindJob<S, U, T>
				{
					iterationLimit = iterationLimit,
					path = jobs[i].path,
					map = Game.map,
					gScore = jobs[i].gScore,
					cameFrom = jobs[i].cameFrom,
					openSet = jobs[i].openSet,
					tilesTaken = jobs[i].tilesTaken,
					rule = request.rule,
					startNodes = jobs[i].startNodes,
					endNodes = jobs[i].endNodes,
					endPos = request.endPos,
					outIterationLimit = jobs[i].outIterationLimit,
					segmentFromEntity = parent.GetComponentDataFromEntity<Segment>(true),
				};
				JobHandle jobHandle = pathFindJob.Schedule(inputDeps);
				pathFindJobs.Add(new PathFindJobBox { pathFindJob = pathFindJob, request = request, jobHandle = jobHandle });
				jobHandles.Add(jobHandle);
			}
		}

		public void CallbackTick()
		{
			for (int i = 0; i < pathFindJobs.Count; i++)
			{
				pathFindJobs[i].jobHandle.Complete();

				// Reverse path here, I guess:
				List<S> path = new List<S>();
				for (int p = pathFindJobs[i].pathFindJob.path.Length - 1; p >= 0; p--)
				{
					path.Add(pathFindJobs[i].pathFindJob.path[p]);
				}

				pathFindJobs[i].request.callback?.Invoke(path);
				// Debug.Log("Iterations = " + (iterationLimit - jobCollection[i].outIterationLimit[0]) + " in time " + (Game.NanoTime() - startedPathFindingAt[i]) / 1000000f + "ms for path of length " + path.Count);
			}
			pathFindJobs.Clear();
		}

		public void Destroy()
		{
			for (int i = 0; i < maxPathFindJobsPerTick; i++)
			{
				jobs[i].path.Dispose();
				jobs[i].gScore.Dispose();
				jobs[i].cameFrom.Dispose();
				jobs[i].openSet.items.Dispose();
				jobs[i].startNodes.Dispose();
				jobs[i].endNodes.Dispose();
				jobs[i].tilesTaken.Dispose();
				jobs[i].outIterationLimit.Dispose();
			}
		}
	}

	public static PathFindType<PFSegment, ulong, PFNode> BuildPathFinder;
	public static PathFindType<TrainEntity, Entity, TrainEntity> TrainPathFinder;

	protected override void OnCreate()
	{
		BuildPathFinder = new PathFindType<PFSegment, ulong, PFNode>();
		BuildPathFinder.Init();
		TrainPathFinder = new PathFindType<TrainEntity, Entity, TrainEntity>();
		TrainPathFinder.Init();
		base.OnCreate();
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(Allocator.Temp);
		BuildPathFinder.Update(this, jobHandles, inputDeps);
		TrainPathFinder.Update(this, jobHandles, inputDeps);
		return base.OnUpdate(JobHandle.CombineDependencies(jobHandles));
	}

	public override void MainThreadSimulationCallbackTick()
	{
		BuildPathFinder.CallbackTick();
		TrainPathFinder.CallbackTick();
	}

	protected override void OnDestroy()
	{
		BuildPathFinder.Destroy();
		TrainPathFinder.Destroy();
	}

	public interface IPathFind<U, T>
	{
		U Index();
		byte NumPFNext(ref Map map, bool pathfinding, BufferFromEntity<NextSegment> next, ComponentDataFromEntity<Segment> segment);
		T PFNextNode(byte i, ref Map map, BufferFromEntity<NextSegment> next, ComponentDataFromEntity<Segment> segment);
		bool IsValid(ref Map map);
		bool PFNextTilesTakenCheck(ref Map map, NativeList<PFTile> tilesTaken, byte i, BuildRule rule);
		float PFNextCost(byte i, ref Map map, BufferFromEntity<NextSegment> next, ComponentDataFromEntity<Segment> segment);
		float3 ConnectionPoint(ComponentDataFromEntity<Segment> segment);
	}

	public interface IPathFindSection<T>
	{
		void Init(T node, byte i);
		T GetNode();
	}

	// S = PFSegment, T = PFNode, U = ulong
	[BurstCompile]
	struct PathFindJob<S, U, T> : IJob where S : struct, IPathFindSection<T> where U : struct, IEquatable<U> where T : struct, IPathFind<U, T>
	{
		const float START_COST = -0.000000001f; // This is to distinguish the starting nodes from the undefined nodes...
		const float END_COST = -10f;

		public Map map;

		public NativeHashMap<U, S> cameFrom;
		public NativeHashMap<U, float> gScore;

		public Heap<T> openSet;

		public int iterationLimit; // For determinism / if the path can't be found
		public NativeArray<int> outIterationLimit; // Testing only!

		// Pathfinding request:
		public BuildRule rule;
		public NativeList<T> startNodes;
		public NativeList<T> endNodes;
		public float3 endPos;

		// Return path:
		public NativeList<S> path;

		// Temp stuff
		public NativeList<PFTile> tilesTaken;

		[ReadOnly] public ComponentDataFromEntity<Segment> segmentFromEntity;
		[ReadOnly] public BufferFromEntity<NextSegment> nextFromEntity;

		public void Execute()
		{
			cameFrom.Clear();
			gScore.Clear();
			openSet.Clear();
			path.Clear();
			PathFind();
		}

		private void PathFind()
		{
			for (int i = 0; i < startNodes.Length; i++)
			{
				openSet.Add(new HeapItem<T>(startNodes[i], H(startNodes[i])));
				gScore[startNodes[i].Index()] = START_COST;
			}

			for (int i = 0; i < endNodes.Length; i++)
			{
				U index = endNodes[i].Index();
				if (gScore.ContainsKey(index))
				{
					gScore[endNodes[i].Index()] = END_COST;
					ReconstructFinishedPath(endNodes[i], true);
					return;
				}
				gScore[endNodes[i].Index()] = END_COST;
			}

			while (iterationLimit > 0 && openSet.currentItemCount > 0)
			{
				T pfn = openSet.RemoveFirst();
				U pfnIndex = pfn.Index();
				float initialCost = gScore[pfnIndex];
				if (initialCost <= END_COST)
				{
					ReconstructFinishedPath(pfn, false);
					return;
				}

				byte numPFNext = pfn.NumPFNext(ref map, true, nextFromEntity, segmentFromEntity);
				for (byte i = 0; i < numPFNext; i++)
				{
					T node = pfn.PFNextNode(i, ref map, nextFromEntity, segmentFromEntity);
					if (!node.IsValid(ref map))
						continue;

					if (!pfn.PFNextTilesTakenCheck(ref map, tilesTaken, i, rule))
						continue;
					
					float cost = pfn.PFNextCost(i, ref map, nextFromEntity, segmentFromEntity);

					if (float.IsInfinity(cost))
						continue;

					U nodeIndex = node.Index();
					float newCost = initialCost + cost;
					float oldCost = 0;
					if (gScore.TryGetValue(nodeIndex, out float item))
						oldCost = item;

					if (oldCost == 0 || newCost < oldCost || oldCost == END_COST || newCost < END_COST - oldCost) // Is it undefined or is the cost lower? Then add it:
					{
						// Duplicates can exist in this model.. but that is fine?
						if (oldCost <= END_COST)
							gScore[nodeIndex] = END_COST - newCost;
						else
							gScore[nodeIndex] = newCost;
						S segment = new S();
						segment.Init(pfn, i);
						cameFrom[nodeIndex] = segment;

						float expectedCost = newCost + H(node);
						openSet.Add(new HeapItem<T>(node, expectedCost));
					}
				}
				iterationLimit--; // Count invalid tiles
			}
			outIterationLimit[0] = iterationLimit; // Path not found
		}

		private float H(T from)
		{
			return math.distance(from.ConnectionPoint(segmentFromEntity), endPos); // - MapInfo.TILE_LENGTH * 5;
		}

		private void ReconstructFinishedPath(T current, bool oneLong)
		{
			S end = new S();
			end.Init(current, byte.MaxValue);
			path.Add(end);
			U currentIndex = current.Index();
			gScore[currentIndex] = END_COST - gScore[currentIndex] - START_COST; // Cost was negative to signify it was the end node
			if (!oneLong)
			{
				while (gScore[currentIndex] > 0 && path.Length < 2000) // If somehow we reach an undefined node, end this while loop as well
				{ // The negative gScore is the starting location
					S segment = cameFrom[currentIndex];
					currentIndex = segment.GetNode().Index();
					path.Add(segment);
				}
			}
			outIterationLimit[0] = iterationLimit;
			// Done
		}
	}
}

public struct HeapItem<T>
{
	// public int HeapIndex;
	public T pfn;
	public float fScore;
	public HeapItem(T pfn, float fScore)
	{
		// this.HeapIndex = 0;
		this.pfn = pfn;
		this.fScore = fScore;
	}
}

public struct Heap<T>
{
	public NativeArray<HeapItem<T>> items;
	public int currentItemCount;

	public Heap(int maxHeapSize)
	{
		items = new NativeArray<HeapItem<T>>(maxHeapSize, Allocator.Persistent);
		currentItemCount = 0;
	}

	public void Clear()
	{
		currentItemCount = 0;
	}

	public void Add(HeapItem<T> item)
	{
		// item.HeapIndex = currentItemCount;
		items[currentItemCount] = item;
		SortUp(item, currentItemCount);
		currentItemCount++;
	}

	public T RemoveFirst()
	{
		T pfn = items[0].pfn;
		currentItemCount--;
		HeapItem<T> lastItem = items[currentItemCount];
		// lastItem.HeapIndex = 0;
		items[0] = lastItem;
		SortDown(items[0], 0);
		return pfn;
	}

	void SortDown(HeapItem<T> item, int itemHeapIndex)
	{
		while (true)
		{
			int childIndexLeft = itemHeapIndex * 2 + 1;
			int childIndexRight = itemHeapIndex * 2 + 2;

			if (childIndexLeft < currentItemCount)
			{
				int swapIndex = childIndexLeft;

				if (childIndexRight < currentItemCount)
				{
					if (items[childIndexLeft].fScore > items[childIndexRight].fScore)
					{
						swapIndex = childIndexRight;
					}
				}

				if (item.fScore > items[swapIndex].fScore)
				{
					Swap(item, itemHeapIndex, items[swapIndex], swapIndex);
					// swapIndex is not used after this...
					itemHeapIndex = swapIndex;
				}
				else
				{
					return;
				}

			}
			else
			{
				return;
			}

		}
	}

	void SortUp(HeapItem<T> item, int itemHeapIndex)
	{
		int parentIndex = (itemHeapIndex - 1) / 2;

		while (true)
		{
			HeapItem<T> parentItem = items[parentIndex];
			if (item.fScore < parentItem.fScore)
			{
				Swap(item, itemHeapIndex, parentItem, parentIndex);
				int save = parentIndex;
				parentIndex = itemHeapIndex;
				itemHeapIndex = save;
			}
			else
			{
				break;
			}

			parentIndex = (itemHeapIndex - 1) / 2;
		}
	}

	void Swap(HeapItem<T> itemA, int itemAHeapIndex, HeapItem<T> itemB, int itemBHeapIndex)
	{
		items[itemAHeapIndex] = itemB;
		items[itemBHeapIndex] = itemA;
	}
}