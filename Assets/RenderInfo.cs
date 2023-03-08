using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;
using System.Linq;

public class RenderInfo : MonoBehaviour
{
	public static RenderInfo self = null;

	// Name the materials the same thing as the type, with postfix "Mat"
	// public static Material[] BeltObject;

	public static Mesh tileObject;
	public static Mesh warningObject;
	public Mesh buildingObject;
	public Mesh trainObject;
	public Mesh trainResourceDisplayObject;
	public Mesh personObject;
	public Mesh trainStationObject;
	public Mesh resourceDisplayObject;
	public Mesh hotFloorObject;
	public Mesh floorHeaterObject;
	public Mesh wallObject;
	public Mesh beltObject;
	public Mesh foodServicerObject;
	public Mesh stairsObject;
	public static Material ConveyorBelt;
	public static Material Building;
	public static Material Facade;
	public static Dictionary<Type, Material> Person = new Dictionary<Type, Material>();
	public static Material Train;
	public static Material HotFloor;
	public static Material Ground;
	public static Material Warning;

	public static Material[] HotterFloors = new Material[26];

	public void Init()
	{
		self = this;
		FieldInfo[] fields = typeof(RenderInfo).GetFields(BindingFlags.Static | BindingFlags.Public);
		foreach (FieldInfo field in fields)
		{
			if (field.FieldType.Equals(typeof(Material)))
			{
				string str = field.Name;
				Material mat = (Material)Resources.Load(str + "Mat");
				field.SetValue(null, mat);
			}
		}
		var types = Assembly.GetExecutingAssembly().GetTypes().Where(m => m.IsValueType && m.GetInterface("IPerson") != null);
		foreach (Type type in types)
		{
			Material mat = (Material)Resources.Load(type.Name + "Mat");
			Person[type] = mat;
		}

		for (int i = 0; i < HotterFloors.Length; i++)
		{
			Material mat = new Material(HotFloor);
			float reduce = 0.035f;
			mat.color = new Color(mat.color.r - i * reduce, mat.color.g - i * reduce, mat.color.b);
			HotterFloors[i] = mat;
		}

		// CUSTOM MESHES:
		tileObject = new Mesh
		{
			vertices = new Vector3[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f) },
			triangles = MeshCreator.RectTri(0, 1, 2, 3),
			normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
			uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) }
		};

		warningObject = new Mesh
		{
			vertices = new Vector3[] { new Vector3(-0.25f, -0.5f, 0), new Vector3(0.25f, -0.5f, 0), new Vector3(-0.25f, 0.5f, 0), new Vector3(0.25f, 0.5f, 0) },
			triangles = MeshCreator.RectTri(0, 1, 2, 3),
			normals = new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward },
			uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) }
		};
	}
}

public class PresetHolder
{
	public static PresetMesh GetPreset(PFR pfr, PFNext i)
	{
		return presets[pfr][(byte)i];
	}

	private static Dictionary<PFR, List<PresetMesh>> presets;
	public static void Init()
	{
		presets = new Dictionary<PFR, List<PresetMesh>>();
		foreach (PFR pfr in Enum.GetValues(typeof(PFR)))
		{
			PFNode from = new PFNode(new PFTile(10, 4, 10), 0, pfr);
			List<PresetMesh> preset = new List<PresetMesh>();
			for (byte i = 0; i < from.NumPFNext(ref Game.map, false); i++)
			{
				preset.Add(new PresetMesh(from, from.PFNextNode(i, ref Game.map)));
			}
			presets.Add(pfr, preset);
		}
	}

	public static void Dispose()
	{
		/*foreach (List<PresetMesh> presetList in presets.Values)
		{
			foreach (PresetMesh preset in presetList)
			{
				preset.route.Dispose();
			}
		}*/
	}
}

public class PresetMesh
{
	public Mesh mesh;
	public PosRotRoute[] route;

	public PresetMesh(PFNode from, PFNode to)
	{
		route = BezierCurve.GetRep(CreateConveyorBelt(from, to), 16 * Math.Abs(to.tile.y - from.tile.y) + GetCurvePrecision(from.ConnectionNormal(), to.ConnectionNormal()));
		for (int i = 0; i < route.Length; i++)
		{
			PosRotRoute prr = route[i];
			prr.posRot.pos -= from.ConnectionPoint();
			route[i] = prr;
		}
		mesh = CreateMesh(route);
	}

	private float3[] CreateConveyorBelt(PFNode from, PFNode to)
	{
		float distance = math.distance(from.ConnectionPoint(), to.ConnectionPoint());
		return new float3[]{ from.ConnectionPoint(),
		from.ConnectionPoint() + from.ConnectionNormal() * distance / 3f,
		to.ConnectionPoint() - to.ConnectionNormal() * distance / 3f,
		to.ConnectionPoint() };
	}

	private int GetCurvePrecision(float3 dir1, float3 dir2)
	{
		float extra = VectorMath.GetAngleMultiplier(dir1, dir2); // num is from 0 - 2, 0 being straight, 2 being directly backwards
		return 1 + (int)(extra * 24f); // extra = 1 when angle is 90 degrees..
	}


	private Mesh CreateMesh(PosRotRoute[] points)
	{
		Vector2[] offsets = { new Vector2(-PFTile.LENGTH * 0.3f, 0), new Vector2(PFTile.LENGTH * 0.3f, 0) };
		float[] uv = { 0f, 1f };
		Vector2[] normals = { new Vector2(0, 1) };
		Mesh mesh1 = BezierCurve.CreateRenderMesh(points, offsets, uv, new float3(0, 1, 0), false, true, normals);

		offsets = new Vector2[] {
			new Vector2(PFTile.LENGTH * 0.3f, 0),
			new Vector2(PFTile.LENGTH * 0.3f, PFTile.LENGTH * 0.1f),
			new Vector2(PFTile.LENGTH * 0.35f, PFTile.LENGTH * 0.1f),
			new Vector2(PFTile.LENGTH * 0.35f, -PFTile.LENGTH * 0.1f),
			new Vector2(-PFTile.LENGTH * 0.35f, -PFTile.LENGTH * 0.1f),
			new Vector2(-PFTile.LENGTH * 0.35f, PFTile.LENGTH * 0.1f),
			new Vector2(-PFTile.LENGTH * 0.3f, PFTile.LENGTH * 0.1f),
			new Vector2(-PFTile.LENGTH * 0.3f, 0) };
		uv = new float[] { 0f, 0.1f, 0.15f, 0.35f, 1.05f, 1.25f, 1.3f, 1.4f };
		normals = new Vector2[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down, Vector2.left, Vector2.up, Vector2.right };
		Mesh mesh2 = BezierCurve.CreateRenderMesh(points, offsets, uv, new float3(0, 1, 0), false, true, normals);
		return MeshCreator.CombineMeshes(false, mesh1, mesh2);
	}
}