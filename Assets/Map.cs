using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public enum BuildRule
{
	Rail,
	Normal,
	HotFloor
}

// Kinda matches up with dir
[System.Flags]
public enum WalkRule : byte
{
	Forward = 1,
	Right = 2,
	Back = 4,
	Left = 8
}

public struct TileInfo
{
	public byte height; // Height that is completely blocked, and is used in the water table
	public WalkRule walkable;
	public Entity pfRect;
}

public struct Map
{
	[ReadOnly] public NativeArray<TileInfo> tileInfo; // Unless you are on an entity like a bridge

	public FluidMap fluidMap;

	// [ReadOnly] public NativeArray<float> depth;
	// [ReadOnly] public NativeArray<float2> velocityPipes;

	// Indexed by tile
	[ReadOnly] private NativeHashMap<uint, Entity> entities;
	[ReadOnly] private NativeMultiHashMap<uint, Entity> rails;

	// Indexed by node
	[ReadOnly] private NativeHashMap<ulong, Entity> connection;
	[ReadOnly] private NativeHashMap<ulong, Entity> railConnections;

	// [ReadOnly] public uint3 size;
	public const uint SIZE_X = 128; // 512 seems reasonable, actually
	public const uint SIZE_Y = 32; // hmm
	public const uint SIZE_Z = 128;

	/*public float GetDepth(float3 pos)
	{
		ushort x = (ushort)((pos.x - 0.5f * PFTile.LENGTH) / PFTile.LENGTH);
		ushort z = (ushort)((pos.z - 0.5f * PFTile.LENGTH) / PFTile.LENGTH);
		float2 lerp = new float2(((pos.x + 0.5f * PFTile.LENGTH) % PFTile.LENGTH) / PFTile.LENGTH, ((pos.z + 0.5f * PFTile.LENGTH) % PFTile.LENGTH) / PFTile.LENGTH); // Probably doesn't work correctly at 0
		return math.lerp(math.lerp(GetDepth(x, z), GetDepth((ushort)(x + 1), z), lerp.x), math.lerp(GetDepth(x, (ushort)(z + 1)), GetDepth((ushort)(x + 1), (ushort)(z + 1)), lerp.x), lerp.y);
	}*/

	public void SetWalkRules()
	{
		for (ushort x = 0; x < SIZE_X; x++)
		{
			for (ushort z = 0; z < SIZE_Z; z++)
			{
				PFTile tile = new PFTile(x, 0, z);
				TileInfo info = tileInfo[tile.HorizontalIndex()];
				// t.walkable = WalkRule.
				for (byte dir = 0; dir < 4; dir++)
				{
					PFTile next = tile.GetToTile((Dir)dir);
					if (next.IsValid() && GetHeightIndex(next) == GetHeightIndex(tile))
					{
						info.walkable |= (WalkRule)(1 << dir);
					}
				}
				tileInfo[tile.HorizontalIndex()] = info;
			}
		}
	}

	public void SetWalkable(PFTile tile, Dir dir, bool walkable)
	{
		SetWalkRule(tile, dir, walkable);
		SetWalkRule(tile.GetToTile(dir), dir.Flip(), walkable);
	}

	private void SetWalkRule(PFTile tile, Dir dir, bool walkable)
	{
		if (tile.IsValid())
		{
			TileInfo info = Game.map.tileInfo[tile.HorizontalIndex()];
			if (walkable)
				info.walkable |= (WalkRule)(1 << (byte)dir);
			else
				info.walkable &= ~(WalkRule)(1 << (byte)dir);
			Game.map.tileInfo[tile.HorizontalIndex()] = info;
		}
	}
	
	public static int GetHorizontalIndex(int x, int z)
	{
		return (int)(x * SIZE_Z + z);
	}

	// Depth interpolation is a little problematic...
	public static UnityEngine.Bounds GetBounds()
	{
		return new UnityEngine.Bounds(WorldPosition(SIZE_X / 2, SIZE_Y / 2, SIZE_Z / 2), new UnityEngine.Vector3(SIZE_X * PFTile.LENGTH, SIZE_Y * PFTile.HEIGHT, SIZE_Z * PFTile.LENGTH));
	}

	public void BuildPFRects()
	{
		long startInit = Game.NanoTime();
		EntityArchetype pfRectArchetype = ECSExtensions.EntityManager.CreateArchetype(typeof(PFRect), typeof(PFRectConnection), typeof(FloorHeaterRef), typeof(HotFloorRef), typeof(ResourcesForSaversRef));

		NativeList<Entity> pfRects = new NativeList<Entity>(Allocator.Temp);
		NativeArray<bool> tilesRected = new NativeArray<bool>((int)(SIZE_X * SIZE_Z), Allocator.Temp);

		int maxSize = 10; // Keep rectangles fairly small..

		for (ushort xStart = 0; xStart < SIZE_X; xStart++)
		{
			for (ushort zStart = 0; zStart < SIZE_Z; zStart++)
			{
				PFTile min = new PFTile(xStart, 0, zStart);
				if (GetHeightIndex(min) > 0 && !tilesRected[(int)(xStart * SIZE_Z + zStart)])
				{
					min.y = GetHeightIndex(min);
					ushort x = (ushort)(xStart + 1);
					for (; x <= SIZE_X; x++)
					{
						PFTile next = new PFTile(x, 0, zStart);
						if (x == SIZE_X || x - xStart >= maxSize || GetHeightIndex(next) != min.y || tilesRected[(int)(x * SIZE_Z + zStart)])
						{
							ushort z = (ushort)(zStart + 1);
							for (; z <= SIZE_Z; z++)
							{
								bool good = z < SIZE_Z && (z - zStart < maxSize);
								if (good)
								{
									for (x = min.x; x < next.x; x++)
									{
										PFTile test = new PFTile(x, 0, z);
										if (GetHeightIndex(test) != min.y || tilesRected[(int)(x * SIZE_Z + z)])
										{
											good = false;
											break;
										}
									}
								}
								if (!good)
								{
									PFTile max = new PFTile((ushort)(next.x - 1), min.y, (ushort)(z - 1));
									PFRect rect = new PFRect { min = min, max = max };
									Entity entity = ECSExtensions.EntityManager.CreateEntity(pfRectArchetype);
									entity.SetData(rect);
									pfRects.Add(entity);
									for (ushort xRect = rect.min.x; xRect <= rect.max.x; xRect++)
									{
										for (ushort zRect = rect.min.z; zRect <= rect.max.z; zRect++)
										{
											tilesRected[(int)(xRect * SIZE_Z + zRect)] = true;
											PFTile tileIndexer = new PFTile(xRect, 0, zRect);
											TileInfo tileInf = tileInfo[(int)tileIndexer.Index()];
											tileInf.pfRect = entity;
											tileInfo[(int)tileIndexer.Index()] = tileInf;
										}
									}
									break;
								}
							}
							break;
						}
					}
				}
			}
		}

		// Now we have to connect the rects...
		for (int i = 0; i < pfRects.Length; i++)
		{
			// UnityEngine.GameObject debugBox = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEngine.Resources.Load("DebugBox"));
			// debugBox.transform.position = new float3(0, 0.5f, 0) + (WorldPosition(pfRects[i].Get<PFRect>().min) + WorldPosition(pfRects[i].Get<PFRect>().max)) / 2f;
			// debugBox.transform.localScale = new float3(pfRects[i].Get<PFRect>().max.x - pfRects[i].Get<PFRect>().min.x + 0.6f, 1f, pfRects[i].Get<PFRect>().max.z - pfRects[i].Get<PFRect>().min.z + 0.6f);

			PFRect rect = pfRects[i].Get<PFRect>();

			// We only have to check the less number
			ushort start = 0;
			Entity current = Entity.Null;
			for (ushort x = rect.min.x; x <= rect.max.x + 1; x++)
			{
				ushort z = (ushort)(rect.min.z - 1);
				if (rect.min.z > 0)
				{
					Entity entity = (x == rect.max.x + 1 || GetHeightIndex(new PFTile(x, 0, z)) != rect.min.y) ? Entity.Null : GetPFRect(new PFTile(x, 0, z));
					MergeTest(pfRects[i], entity, ref current, rect, ref start, x, true);
				}
			}
			start = 0;
			current = Entity.Null;
			for (ushort z = rect.min.z; z <= rect.max.z + 1; z++)
			{
				ushort x = (ushort)(rect.min.x - 1);
				if (rect.min.x > 0)
				{
					Entity entity = (z == rect.max.z + 1 || GetHeightIndex(new PFTile(x, 0, z)) != rect.min.y) ? Entity.Null : GetPFRect(new PFTile(x, 0, z));
					MergeTest(pfRects[i], entity, ref current, rect, ref start, z, false);
				}
			}
		}

		DebugDraw.Log("PFRects: ", startInit);
	}


	public void SpawnMesh()
	{
		long start = Game.NanoTime();

		int splits = 8;
		for (int j = 0; j < splits; j++)
		{
			for (int k = 0; k < splits; k++)
			{
				// List<Vector3[]> rectangles = new List<Vector3[]>();
				List<Vector3> rectangles = new List<Vector3>(); // (int)(Map.SIZE_X / splits * Map.SIZE_X / splits));
				List<Vector3> normals = new List<Vector3>(); // (int)(Map.SIZE_X / splits * Map.SIZE_X / splits));

				MeshCreator.AddRectangle(rectangles, normals, Vector3.up,
					WorldPosition(j * SIZE_X / splits, 0, k * SIZE_Z / splits), WorldPosition(j * SIZE_X / splits, 0, (k + 1) * SIZE_Z / splits),
					WorldPosition((j + 1) * SIZE_X / splits, 0, k * SIZE_Z / splits), WorldPosition((j + 1) * SIZE_X / splits, 0, (k + 1) * SIZE_Z / splits));

				// List<Vector3> rectNormals = new List<Vector3>();
				for (ushort x = (ushort)(j * SIZE_X / splits); x < (j + 1) * SIZE_X / splits; x++)
				{
					for (ushort z = (ushort)(k * SIZE_Z / splits); z < (k + 1) * SIZE_Z / splits; z++)
					{
						byte y = Game.map.GetHeightIndex(new PFTile(x, 0, z));
						if (y > 0)
						{
							MeshCreator.AddRectangle(rectangles, normals, Vector3.up,
								WorldPosition(x, y, z), WorldPosition(x, y, z + 1), WorldPosition(x + 1, y, z), WorldPosition(x + 1, y, z + 1));
						}
						PFTile adj = new PFTile((ushort)(x + 1), 0, z);
						if (x < SIZE_X - 1 && Game.map.GetHeightIndex(adj) != y)
						{
							byte newY = Game.map.GetHeightIndex(adj);
							MeshCreator.AddRectangle(rectangles, normals, newY > y ? Vector3.left : Vector3.right,
								WorldPosition(x + 1, y, z), WorldPosition(x + 1, y, z + 1), WorldPosition(x + 1, newY, z), WorldPosition(x + 1, newY, z + 1));
						}
						adj = new PFTile(x, 0, (ushort)(z + 1));
						if (z < SIZE_Z - 1 && Game.map.GetHeightIndex(adj) != y)
						{
							byte newY = Game.map.GetHeightIndex(adj);
							MeshCreator.AddRectangle(rectangles, normals, newY > y ? Vector3.back : Vector3.forward,
								WorldPosition(x, y, z + 1), WorldPosition(x, newY, z + 1), WorldPosition(x + 1, y, z + 1), WorldPosition(x + 1, newY, z + 1));
						}
					}
				}

				int[] triangles = new int[rectangles.Count * 6 / 4];
				for (int i = 0; i < triangles.Length / 6; i++)
				{
					triangles[i * 6 + 0] = i * 4 + 0; triangles[i * 6 + 1] = i * 4 + 1; triangles[i * 6 + 2] = i * 4 + 2;
					triangles[i * 6 + 3] = i * 4 + 2; triangles[i * 6 + 4] = i * 4 + 1; triangles[i * 6 + 5] = i * 4 + 3;
				}


				Mesh mesh = new Mesh
				{
					vertices = rectangles.ToArray(),
					normals = normals.ToArray(),
					triangles = triangles
				};
				Entity entity = ECSExtensions.EntityManager.CreateEntity(
					typeof(Translation), typeof(Rotation), typeof(RenderMesh), typeof(LocalToWorld), typeof(PhysicsCollider));
				entity.SetSharedData(new RenderMesh { mesh = mesh, material = RenderInfo.Ground });

				BlobAssetReference<Unity.Physics.Collider> collider =
					Unity.Physics.MeshCollider.Create(
					new NativeArray<float3>(System.Array.ConvertAll(mesh.vertices, (item) => (float3)item), Allocator.Temp),
					new NativeArray<int>(mesh.triangles, Allocator.Temp));

				entity.SetData(new PhysicsCollider { Value = collider });
			}
		}
		DebugDraw.Log("SpawnMeshAndCollider: ", start);
	}

	private void MergeTest(Entity pfRect, Entity entity, ref Entity current, PFRect rect, ref ushort start, ushort x, bool isX)
	{
		if (entity != current)
		{
			if (current != Entity.Null)
			{
				if (isX)
					Merge(pfRect, current, WorldPosition(start, rect.min.y, rect.min.z), WorldPosition(x, rect.min.y, rect.min.z));
				else
					Merge(pfRect, current, WorldPosition(rect.min.x, rect.min.y, start), WorldPosition(rect.min.x, rect.min.y, x));
				current = Entity.Null;
			}
			if (entity != Entity.Null)
			{
				start = x;
				current = entity;
			}
		}
	}

	/*private void TestForRectConnection(Entity aEntity, Entity bEntity)
	{
		PFRect a = aEntity.Get<PFRect>();
		PFRect b = bEntity.Get<PFRect>();
		if (aEntity != bEntity && a.min.y == b.min.y)
		{
			// One of the corners must be adjacent to the other pfRect's side, (for both pfRects)
			for (int isX = 0; isX < 2; isX++)
			{
				for (int xDiff = -1; xDiff <= 1; xDiff += 2)
				{
					for (int zDiff = -1; zDiff <= 1; zDiff += 2)
					{
						if (IsCornerConnected(aEntity, bEntity, a, b, isX == 0, xDiff, zDiff))
						{
							return;
						}
					}
				}
			}
		}
	}*/

	private void Merge(Entity a, Entity b, float3 from, float3 to)
	{
		a.Buffer<PFRectConnection>().Add(new PFRectConnection { other = b, from = from, to = to });
		b.Buffer<PFRectConnection>().Add(new PFRectConnection { other = a, from = from, to = to });
		// UnityEngine.GameObject debugBox = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEngine.Resources.Load("DebugBox"));
		// debugBox.transform.position = new float3(0, 1f, 0) + (from + to) / 2f;
		// debugBox.transform.rotation = UnityEngine.Quaternion.LookRotation(to - from);
		// debugBox.transform.localScale = new float3(1, 1, math.distance(from, to));
	}

	// diff = -1 or 1
	/*private bool IsCornerConnected(Entity aEntity, Entity bEntity, PFRect a, PFRect b, bool isX, int xDiff, int zDiff)
	{
		PFTile corner = new PFTile(xDiff == 1 ? a.max.x : a.min.x, a.min.y, zDiff == 1 ? a.max.z : a.min.z);
		if (isX)
		{
			if (corner.x + xDiff == b.min.x && corner.z >= b.min.z && corner.z <= b.max.z)
			{
				Merge(aEntity, bEntity, WorldPosition(corner.x + xDiff, corner.y, zDiff == 1 ? math.max((float)a.min.z, b.min.z) : math.min((float)a.max.z, b.max.z)), WorldPosition(corner.x + xDiff, corner.y, corner.z + zDiff));
				return true;
			}
		}
		else
		{
			if (corner.z + zDiff == b.min.z && corner.x >= b.min.x && corner.x <= b.max.x)
			{
				Merge(aEntity, bEntity, WorldPosition(xDiff == 1 ? math.max((float)a.min.x, b.min.x) : math.min((float)a.max.x, b.max.x), corner.y, corner.z + zDiff), WorldPosition(corner.x + xDiff, corner.y, corner.z + zDiff));
				return true;
			}
		}
		return false;
	}*/


	public Entity GetPFRect(PFTile tile)
	{
		return tileInfo[tile.HorizontalIndex()].pfRect;
	}

	public void AddOrRemoveEntity<TRef>(Entity entity, bool add) where TRef : struct, IBufferElementData, IBufferEntity
	{
		DynamicBuffer<TRef> refs = GetPFRect(entity.Get<BasicPosition>().tile).Buffer<TRef>();
		if (add)
		{
			refs.Add(new TRef { Entity = entity });
		}
		else
		{
			for (int i = 0; i < refs.Length; i++)
			{
				if (refs[i].Entity == entity)
				{
					refs.RemoveAt(i);
					break;
				}
			}
		}
	}

	struct EntityAndPos
	{
		public Entity entity;
		public float totalPreviousCost;
		public float3 pos;
	}

	private float3 NearestPointOnLine(float3 point, float3 from, float3 to)
	{
		float3 fromToPoint = point - from;
		float3 fromToTo = to - from;
		float t = math.clamp(math.dot(fromToPoint, fromToTo) / math.distancesq(from, to), 0f, 1f);
		return from + fromToTo * t;
	}

	public Entity FindNearest<T>(ComponentDataFromEntity<BasicPosition> basicPosition, BufferFromEntity<PFRectConnection> pfRectConnectionBuffer, BufferFromEntity<T> buffer, float3 fromPos, float maxDist) where T : struct, IBufferElementData, IBufferEntity
	{
		Entity closestEntity = Entity.Null;
		float closest = maxDist;

		PFTile fromTile = GetTile(fromPos);

		NativeHashMap<Entity, bool> allChecked = new NativeHashMap<Entity, bool>(1, Allocator.Temp);
		NativeList<EntityAndPos> openList = new NativeList<EntityAndPos>(Allocator.Temp);
		// Search.. to max distance

		openList.Add(new EntityAndPos { entity = GetPFRect(fromTile), pos = fromPos, totalPreviousCost = 0 });
		if (openList[0].entity == Entity.Null)
			return Entity.Null;
		allChecked[openList[0].entity] = true;

		while (openList.Length > 0)
		{
			EntityAndPos eap = openList[0];
			openList.RemoveAtSwapBack(0);

			DynamicBuffer<T> pfBuffer = buffer[eap.entity];
			for (int i = 0; i < pfBuffer.Length; i++)
			{
				if (basicPosition[pfBuffer[i].Entity].IsValid())
				{
					float dist = math.distance(eap.pos, WorldPosition(basicPosition[pfBuffer[i].Entity].tile));
					if (eap.totalPreviousCost + dist < closest)
					{
						closestEntity = pfBuffer[i].Entity;
						closest = dist;
					}
				}
			}

			DynamicBuffer<PFRectConnection> pfRectConnections = pfRectConnectionBuffer[eap.entity];
			for (int i = 0; i < pfRectConnections.Length; i++)
			{
				if (!allChecked.ContainsKey(pfRectConnections[i].other))
				{
					float3 nextPos = NearestPointOnLine(eap.pos, pfRectConnections[i].from, pfRectConnections[i].to);
					float nextCost = eap.totalPreviousCost + math.distance(eap.pos, nextPos);
					if (nextCost < closest)
					{
						EntityAndPos next = new EntityAndPos { entity = pfRectConnections[i].other, totalPreviousCost = nextCost, pos = nextPos };
						openList.Add(next);
						allChecked[next.entity] = true;
					}
				}
			}
		}

		/*uint2 pos = new uint2(fromTile.x / LARGE_TILE_SIZE, fromTile.z / LARGE_TILE_SIZE);
		for (uint r = 0; r < 2 + (int)(maxDist / LARGE_TILE_SIZE); r++)
		{
			if (closestEntity != Entity.Null)
				return closestEntity;
			for (uint x = math.max(0, pos.x - r); x <= math.min(Map.SIZE_X / LARGE_TILE_SIZE - 1, pos.x + r); x++)
			{
				for (uint z = math.max(0, pos.y - r); z <= math.min(Map.SIZE_Z / LARGE_TILE_SIZE - 1, pos.y + r); z++)
				{
					if (math.abs(x - (int)pos.x) == r || math.abs(z - (int)pos.y) == r)
					{
						Entity largeTile = GetLargeTile(x, z);
						for (int i = 0; i < buffer[largeTile].Length; i++)
						{
							if (basicPosition[buffer[largeTile][i].Entity].IsValid())
							{
								float dist = math.distancesq(fromPos, Map.WorldPosition(basicPosition[buffer[largeTile][i].Entity].tile));
								if (dist < closestSquared)
								{
									closestEntity = buffer[largeTile][i].Entity;
									closestSquared = dist;
								}
							}
						}
					}
				}
			}
		}*/
		return closestEntity;
	}

	public bool IsBuildable(PFTile tile, BuildRule rule)
	{
		if (!tile.IsValid())
			return false;
		if ((rule == BuildRule.HotFloor && tile.y != GetHeightIndex(tile) - 1) || (rule != BuildRule.HotFloor && tile.y < GetHeightIndex(tile)))
			return false;
		if (rule == BuildRule.Rail)
			return !entities.ContainsKey(tile.Index());
		else
			return !entities.ContainsKey(tile.Index()) && !rails.ContainsKey(tile.Index());
	}

	public void SetWalkingHeight(PFTile tile, byte height)
	{
		tile.y = 0;
		TileInfo tileInf = tileInfo[(int)tile.Index()];
		tileInf.height = height;
		tileInfo[(int)tile.Index()] = tileInf;
	}
	public float GetHeight(float2 pos)
	{
		return GetHeight(GetTile(pos));
	}
	public float GetHeight(PFTile tile)
	{
		return GetHeightIndex(tile) * PFTile.HEIGHT;
	}
	public float GetHeight(ComponentDataFromEntity<WalkInfo> walkInfo, float3 pos)
	{
		pos.y += PFTile.HEIGHT * 1f;
		PFTile tile = GetTile(pos);
		float height = GetHeight(tile);
		if (height > pos.y)
			return height;


		Entity tileEntity = GetEntity(tile);
		if (tileEntity != Entity.Null)
		{
			if (walkInfo.HasComponent(tileEntity))
				return walkInfo[tileEntity].GetHeight(pos);
			return (tile.y + 1) * PFTile.HEIGHT;
		}

		pos.y -= PFTile.HEIGHT;
		PFTile below = GetTile(pos);
		Entity belowEntity = GetEntity(below);
		if (belowEntity != Entity.Null)
		{
			if (walkInfo.HasComponent(belowEntity))
				return walkInfo[belowEntity].GetHeight(pos);
			return (below.y + 1) * PFTile.HEIGHT;
		}

		return height;
	}
	public byte GetHeightIndex(PFTile tile)
	{
		tile.y = 0;
		return tileInfo[(int)tile.Index()].height;
	}

	public Entity GetEntity(PFTile tile, bool canBeFacadeOrConstructing = false)
	{
		if (entities.TryGetValue(tile.Index(), out Entity entity)) // && (canBeFacadeOrConstructing || !entity.Has<Constructing>()))
			return entity;
		else
			return Entity.Null;
	}

	public void SetEntity(PFTile tile, Entity entity, BuildRule rule)
	{
		if (rule == BuildRule.Rail)
			rails.Add(tile.Index(), entity);
		else
			entities[tile.Index()] = entity;
	}

	public void SetRailConnection(PFNode node, Entity entity, bool set)
	{
		// Because of the nature of PFR, this is fine...
		// Rails do not have direction so they PFNode's simply always point outwards
		NextSegment nextSegment = new NextSegment { segment = entity };
		if (set)
		{
			if (connection.ContainsKey(node.Index()))
			{
				connection[node.Index()].Buffer<NextSegment>().Add(nextSegment);
			}
			else
			{
				Entity bufferEntity = ECSExtensions.EntityManager.CreateEntity(typeof(NextSegment));
				bufferEntity.Buffer<NextSegment>().Add(nextSegment);
				connection[node.Index()] = bufferEntity;
			}
		}
		else
		{
			connection[node.Index()].Buffer<NextSegment>().Remove(nextSegment);
		}
	}

	public void SetNodeConnection(PFNode node, Entity entity, bool set, bool isTo)
	{
		if (set)
		{
			if (connection.TryGetValue(node.Index(), out Entity other))
			{
				entity.Modify((ref Segment segment) => segment[isTo] = other);
				other.Modify((ref Segment segment) => segment[!isTo] = entity);
				connection.Remove(node.Index()); // Connection has been formed
			}
			else
			{
				connection[node.Index()] = entity;
			}

		}
		else
		{
			Entity adj = entity.Get<Segment>()[isTo];
			if (adj == Entity.Null)
			{
				// Assume exists..
				Assert.IsEqual(connection[node.Index()], entity, "Connection not set to correct entity!");
				connection.Remove(node.Index());
			}
			else
			{
				// Remove connection...
				Assert.IsTrue(!connection.ContainsKey(node.Index()), "Connection already set?");
				adj.Modify((ref Segment segment) => segment[!isTo] = Entity.Null);
				connection[node.Index()] = adj;
			}
		}
	}

	/*public Entity GetNodeConnection(PFNode node)
	{
		if (connection.TryGetValue(node.Index(), out Entity outEntity))
			return outEntity;
		else
			return Entity.Null;
	}*/

	public DynamicBuffer<NextSegment> GetNodeConnections(BufferFromEntity<NextSegment> next, PFNode node)
	{
		if (connection.TryGetValue(node.Index(), out Entity outEntity))
			return next[outEntity];
		else
			return default;
	}

	public void UnsetEntity(PFTile tile, Entity entity, BuildRule rule)
	{
		if (rule == BuildRule.Rail)
			rails.Remove(tile.Index(), entity);
		else
			entities.Remove(tile.Index());
	}

	public static PFTile GetTile(float2 pos)
	{
		return GetTile(new float3(pos.x, 0, pos.y));
	}

	public static PFTile GetTile(float3 pos)
	{
		if (pos.x < 0 || pos.z < 0)
			return PFTile.Invalid;
		PFTile tile = new PFTile((ushort)(pos.x / PFTile.LENGTH), (byte)(pos.y / PFTile.HEIGHT), (ushort)(pos.z / PFTile.LENGTH));
		if (tile.x >= SIZE_X || tile.z >= SIZE_Z)
			return PFTile.Invalid;
		else
			return tile;
	}

	public static PFNode GetNode(float3 pos, PFR pfr)
	{
		PFTile tile = GetTile(pos);
		float2 mod = new float2(pos.x % PFTile.LENGTH, pos.z % PFTile.LENGTH);
		Dir dir;
		if (mod.x < mod.y)
		{
			dir = (PFTile.LENGTH - mod.x < mod.y) ? Dir.Forward : Dir.Left;
		}
		else
		{
			dir = (PFTile.LENGTH - mod.x < mod.y) ? Dir.Right : Dir.Back;
		}
		return new PFNode(tile, dir, pfr);
	}

	public static float3 WorldPosition(float x, float y, float z)
	{
		return new float3(x * PFTile.LENGTH, y * PFTile.HEIGHT, z * PFTile.LENGTH);
	}

	public static float3 WorldPosition(PFTile tile, PFR pfr = PFR.Default)
	{
		return WorldPosition(tile.x + 0.5f, tile.y, tile.z + 0.5f) + pfr.Offset();
	}

	public float3 CornerPos(PFNode pfn, int c)
	{
		switch (pfn.dir)
		{
			case Dir.Left: return WorldPosition(pfn.tile.x, 0, pfn.tile.z + 1 - c); // Note how order does matter
			case Dir.Forward: return WorldPosition(pfn.tile.x + 1, 0, pfn.tile.z + c);
			case Dir.Right: return WorldPosition(pfn.tile.x + c, 0, pfn.tile.z);
			case Dir.Back: return WorldPosition(pfn.tile.x + 1 - c, 0, pfn.tile.z + 1);
			default: Assert.Fail("Invalid: " + pfn.dir); return new float3(0, 0, 0);
		}
	}

	public bool TileContains(PFTile tile, float3 pos)
	{
		float3 low = WorldPosition(tile.x, tile.y, tile.z);
		float3 high = WorldPosition(tile.x + 1, tile.y + 1, tile.z + 1);
		return pos.x >= low.x && pos.y >= low.y && pos.z >= low.z && pos.x <= high.x && pos.y <= high.y && pos.z <= high.z;
	}

	public void Init()
	{
		// size = new uint3(16384, 16, 16384);
		rails = new NativeMultiHashMap<uint, Entity>(100, Allocator.Persistent);
		entities = new NativeHashMap<uint, Entity>(100, Allocator.Persistent);
		connection = new NativeHashMap<ulong, Entity>(100, Allocator.Persistent);
		railConnections = new NativeHashMap<ulong, Entity>(100, Allocator.Persistent);
		tileInfo = new NativeArray<TileInfo>((int)(SIZE_X * SIZE_Z), Allocator.Persistent);
		fluidMap.waterTable = new NativeArray<WaterTable>((int)(SIZE_X * SIZE_Z), Allocator.Persistent);
	}

	// Only called on main map:
	public void Dispose()
	{
		rails.Dispose();
		entities.Dispose();
		connection.Dispose();
		railConnections.Dispose();
		tileInfo.Dispose();
		fluidMap.waterTable.Dispose();
	}


}