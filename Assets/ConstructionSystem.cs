using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Material = UnityEngine.Material;

public struct IsInCache : ISystemStateComponentData { }

public struct Constructing : IComponentData
{
	public bool facade;
	public float progress;
	public float progressToComplete;
}

public struct Health : IComponentData
{
	public float health;
}

[InternalBufferCapacity(2)]
public struct SubMeshRenderer : IBufferElementData
{
	public Entity renderer;
}

/*
[InternalBufferCapacity(0)]
public struct OutBorder : IBufferElementData
{
	public PFNode node;
	public static List<PFNode> GetPFNodes(Entity entity, PFR pfr, bool outFacing)
	{
		DynamicBuffer<OutBorder> outBorder = entity.Buffer<OutBorder>();
		List<PFNode> nodes = new List<PFNode>(outBorder.Length);
		for (int i = 0; i < outBorder.Length; i++)
		{
			PFNode node = outBorder[i].node;
			if (outFacing)
			{
				nodes.Add(new PFNode(node.tile, node.dir, pfr));
			}
			else
			{
				nodes.Add(node.Flip());
			}
		}
		return nodes;
	}
}*/

public struct Constructable : IComponentData
{
	public ushort constructableID;
}
/*
[InternalBufferCapacity(0)]
public struct TileTaken : IBufferElementData
{
	public PFTile tile;
}*/

/*public struct Producer : IComponentData
{
	public float ratio;
	public Resource inType;
	public Resource outType;
}*/

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class ConstructionSystem : JobComponentSystemWithCallback
{
	public EntityArchetype facadeArchetype;

	public static EntityArchetype subMeshRenderer;
	private NativeList<Entity> entitiesConstructed;

	private List<Constructor> constructors = new List<Constructor>();
	private static Dictionary<Type, Constructor> constructorDictionary = new Dictionary<Type, Constructor>();

	protected override void OnCreate()
	{
		facadeArchetype = EntityManager.CreateArchetype(
			typeof(Constructable),
			typeof(Constructing),
			typeof(SubMeshRenderer));
		subMeshRenderer = EntityManager.CreateArchetype(
		   typeof(Translation),
		   typeof(Rotation),
		   typeof(RenderMesh),
		   typeof(LocalToWorld));
		entitiesConstructed = new NativeList<Entity>(Allocator.Persistent);

		Constructor.EntityManager = EntityManager;
		byte addIndex = 0;
		foreach (Type type in typeof(Constructor).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Constructor))))
		{
			if (!type.IsAbstract)
			{
				Constructor constructor = (Constructor)Activator.CreateInstance(type);
				constructor.id = addIndex++;
				constructor.AddComponentTypes();
				constructor.components = new ComponentTypes(constructor.types.ToArray());
				constructors.Add(constructor);
				constructorDictionary.Add(constructor.GetType(), constructor);
			}
		}
		base.OnCreate();
	}

	[BurstCompile]
	struct ConstructionJob : IJobForEachWithEntity<Constructing>
	{
		public float tickTime;
		public NativeList<Entity> entitiesConstructed;

		public void Execute(Entity entity, int index, [ReadOnly] ref Constructing constructing)
		{
			// When the entity is a facade, the constructing.progress is set to 1 to indicate that it is done
			if (constructing.progress >= constructing.progressToComplete)
			{
				entitiesConstructed.Add(entity);
			}
			else if (constructing.progress == -1)
			{
				// Set progress to -1 on a facade entity to instantly construct it
				entitiesConstructed.Add(entity);
				entitiesConstructed.Add(entity);
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		JobHandle jobHandle = new ConstructionJob
		{
			tickTime = Game.GetTickTime(),
			entitiesConstructed = entitiesConstructed
		}.ScheduleSingle(this, inputDeps);
		return base.OnUpdate(jobHandle);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		// The jobs are forced to be completed by this point
		for (int i = 0; i < entitiesConstructed.Length; i++)
		{
			Constructor constructor = constructors[entitiesConstructed[i].Get<Constructable>().constructableID];
			if (entitiesConstructed[i].Get<Constructing>().facade)
				constructor.StartConstructing(entitiesConstructed[i]);
			else
				constructor.FinishConstructing(entitiesConstructed[i]);
		}
		entitiesConstructed.Clear(); // It would appear this needs to be done here because the job system does not even enter the OnUpdate method if there is nothing to update
	}

	protected override void OnDestroy()
	{
		entitiesConstructed.Dispose();
		base.OnDestroy();
	}

	public void DestroyEntity(Entity entity)
	{
		constructors[entity.Get<Constructable>().constructableID].Destroy(entity);
	}

	public bool AttemptDamageAtTile(PFTile tile, float damage)
	{
		/*Entity entity = Game.map.GetEntity(tile);
		if (EntityManager.HasComponent<Health>(entity))
		{
			entity.Modify((ref Health h) => h.health -= damage);
			if (entity.Get<Health>().health <= 0)
			{
				constructors[EntityManager.GetComponentData<Constructable>(entity).constructableID].Destroy(entity);
			}
			return true;
		}*/
		return false;
	}

	public static T GetConstructor<T>() where T : Constructor, new()
	{
		return (T)constructorDictionary[typeof(T)];
	}
}