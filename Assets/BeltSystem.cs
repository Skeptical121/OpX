using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;


public struct Belt : IComponentData
{
	public float speed;
}

public struct ResourceInfo
{
	public ResourceType type;
	public Entity renderEntity;
	public float length;
	public float amount;
}

[InternalBufferCapacity(0)]
public struct BeltObject : IBufferElementData
{
	public ResourceInfo resInfo;
	public float pos;
}

[InternalBufferCapacity(2)]
public struct BeltTransfer : IBufferElementData
{
	public Entity beltEntity;
	public bool exporter;
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class BeltSystem : JobComponentSystemWithCallback
{
	public NativeList<Entity> endBelts; // Start with end belts for belt simulation (not as efficient as it could be, (would be better to combine the belts), but it's easier)
	public NativeQueue<Entity> beltObjectRenderersToDestroy;

	protected override void OnCreate()
	{
		base.OnCreate();
		endBelts = new NativeList<Entity>(0, Allocator.Persistent);
		beltObjectRenderersToDestroy = new NativeQueue<Entity>(Allocator.Persistent);
	}

	protected override void OnDestroy()
	{
		endBelts.Dispose();
		beltObjectRenderersToDestroy.Dispose();
		base.OnDestroy();
	}

	[BurstCompile]
	struct BeltExportTick : IJobForEach_BC<BeltTransfer, ResourceStorage>
	{
		[NativeDisableParallelForRestriction] public BufferFromEntity<BeltObject> objects;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Segment> segment;
		public NativeQueue<Entity>.ParallelWriter beltObjectRenderersToDestroy;
		public void Execute([ReadOnly] DynamicBuffer<BeltTransfer> beltTransferBuffer, ref ResourceStorage storage)
		{
			for (int i = 0; i < beltTransferBuffer.Length; i++)
			{
				BeltTransfer beltTransfer = beltTransferBuffer[i];
				// From storage to belt...
				DynamicBuffer<BeltObject> beltObjects = objects[beltTransfer.beltEntity];
				if (beltTransfer.exporter)
				{
					float length = 0.6f;
					float amount = 10f;
					if ((beltObjects.Length == 0 || beltObjects[beltObjects.Length - 1].pos > length) && storage.CanTake(storage.type, amount))
					{

						// UnityEngine.Graphics.DrawMeshInstanced()
						// Add to belt:
						storage.numResources -= amount;
						beltObjects.Add(new BeltObject { pos = 0, resInfo = new ResourceInfo { type = storage.type, amount = amount, length = length, renderEntity = Entity.Null } });
					}
				}
				else
				{
					if (beltObjects.Length > 0 && beltObjects[0].pos >= segment[beltTransfer.beltEntity].distance - beltObjects[0].resInfo.length
						&& storage.CanAdd(beltObjects[0].resInfo.type, beltObjects[0].resInfo.amount))
					{
						storage.numResources += beltObjects[0].resInfo.amount;
						if (beltObjects[i].resInfo.renderEntity != Entity.Null)
						{
							beltObjectRenderersToDestroy.Enqueue(beltObjects[i].resInfo.renderEntity);
						}
						beltObjects.RemoveAt(0);
					}
				}
			}
		}
	}

	[BurstCompile]
	struct BeltTick : IJobParallelFor
	{
		public NativeArray<Entity> endBelts;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<BeltObjectInterp> beltInterp;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Segment> segment;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Belt> belt;
		[NativeDisableParallelForRestriction] public BufferFromEntity<BeltObject> objects;
		public float tickTime;

		public void Execute(int index)
		{
			Entity nextBelt = Entity.Null;
			Entity beltEntity = endBelts[index];
			// Only the end belt can output to something other than a belt

			float next = 0; // Objects should be shorter than any belt

			do
			{
				float speed = belt[beltEntity].speed;
				Segment s = segment[beltEntity];
				DynamicBuffer<BeltObject> beltObjects = objects[beltEntity];
				next += s.distance;
				if (beltObjects.Length > 0)
				{
					int removeAmount = -1;
					for (int i = 0; i < beltObjects.Length; i++)
					{
						BeltObject beltObject = beltObjects[i];
						beltObject.pos += speed * tickTime;

						if (beltObject.pos + beltObject.resInfo.length > next)
						{
							beltObject.pos = next - beltObject.resInfo.length;
						}
						if (beltObject.pos > s.distance)
						{
							beltObject.pos -= s.distance;
							objects[nextBelt].Add(beltObject);
							removeAmount = i;
							BeltObjectRenderSystem.SetParent(beltInterp, beltObject.resInfo.renderEntity, nextBelt, 0, 0);
						}
						else
						{
							beltObjects[i] = beltObject;
						}
						BeltObjectRenderSystem.SetInterp(beltInterp, beltObject.pos, beltObject.resInfo);

						next = beltObjects[i].pos;
					}
					if (removeAmount != -1)
					{
						beltObjects.RemoveRange(0, removeAmount + 1);
					}
				}
				nextBelt = beltEntity;
				beltEntity = s.previous;
				if (beltEntity == endBelts[index]) // Loop
					break;
			} while (beltEntity != Entity.Null);
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		inputDeps = new BeltTick
		{
			endBelts = endBelts,
			beltInterp = GetComponentDataFromEntity<BeltObjectInterp>(),
			segment = GetComponentDataFromEntity<Segment>(),
			belt = GetComponentDataFromEntity<Belt>(),
			objects = GetBufferFromEntity<BeltObject>(),
			tickTime = Game.GetTickTime()
		}.Schedule(endBelts.Length, 4, inputDeps);

		inputDeps = new BeltExportTick
		{
			objects = GetBufferFromEntity<BeltObject>(),
			segment = GetComponentDataFromEntity<Segment>(),
			beltObjectRenderersToDestroy = beltObjectRenderersToDestroy.AsParallelWriter()
		}.Schedule(this, inputDeps);

		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		while (beltObjectRenderersToDestroy.TryDequeue(out Entity entity))
		{
			entity.Destroy(); // Maybe we should remove them all at once?
		}
		beltObjectRenderersToDestroy.Clear();
		UnityEngine.Vector2 offset = new UnityEngine.Vector2(0, 0 - Game.GetGameTime());
		RenderInfo.ConveyorBelt.SetTextureOffset("_MainTex", offset);
	}
}
