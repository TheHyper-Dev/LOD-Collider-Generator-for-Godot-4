using Godot;
using System;

[GlobalClass, Tool]
public partial class LOD_Collider_Generator : MeshInstance3D
{
	[Export] public int SelectedLOD = 0;
	[Export] public string SavePath = "res://saved_lod.mesh";
	[Export] public string SavePath_col = "res://saved_lod_col.res";
	public enum ColliderType { Convex, Convex_Simplified, Trimesh, Box, Cylinder, Sphere }

	[Export] public ColliderType colliderType = ColliderType.Trimesh;
	[Export]
	public bool Generate
	{
		get => false;
		set
		{
			if (value)
				GenerateAndSave();
		}
	}

	// Called when the node enters the scene tree for the first time.
	public ArrayMesh GenerateAndSave()
	{
		// Prepare a new ArrayMesh to hold our LOD data
		ArrayMesh lodMesh = new ArrayMesh();
		if (Mesh == null)
		{
			GD.PrintErr("No mesh assigned to MeshInstance3D.");
			return lodMesh;
		}

		// Create the ImporterMesh from the existing mesh
		ImporterMesh importerMesh = ImporterMesh.FromMesh(Mesh);
		int surfaceCount = importerMesh.GetSurfaceCount();



		for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
		{
			// Get the base vertex data for this surface (Vertices, Normals, UVs, etc.)
			Godot.Collections.Array arrays = importerMesh.GetSurfaceArrays(surfaceIndex);

			// Check if the requested LOD index actually exists for this surface
			// (Note: GetSurfaceLodCount returns the total number of LODs, so we use > to check bounds)
			if (importerMesh.GetSurfaceLodCount(surfaceIndex) > SelectedLOD)
			{
				// Retrieve the LOD's simplified index buffer (the optimized triangles)
				int[] lodIndices = importerMesh.GetSurfaceLodIndices(surfaceIndex, SelectedLOD);

				// Overwrite the original high-poly index array with the low-poly LOD indices
				arrays[(int)Mesh.ArrayType.Index] = lodIndices;

				GD.Print($"Surface {surfaceIndex}: Successfully applied LOD {SelectedLOD} indices.");
			}
			else
			{
				GD.Print($"Surface {surfaceIndex}: LOD {SelectedLOD} not found. Using base mesh indices.");
			}

			// Reconstruct the surface on the new ArrayMesh using the modified arrays
			lodMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

			// Maintain the original material
			Material originalMaterial = importerMesh.GetSurfaceMaterial(surfaceIndex);
			if (originalMaterial != null)
			{
				lodMesh.SurfaceSetMaterial(surfaceIndex, originalMaterial);
			}
		}

		// Save the resulting ArrayMesh to the file system as a .mesh file
		Error err = ResourceSaver.Save(lodMesh, SavePath);
		MeshInstance3D lod_mesh_instance = new();
		lod_mesh_instance.Mesh = lodMesh;
		AddChild(lod_mesh_instance);
		CollisionShape3D col_shape = new();
		col_shape.Name = $"Collider_LOD_{colliderType.ToString()}_{SelectedLOD}";
		AddSibling(col_shape);
		col_shape.Owner = GetTree().EditedSceneRoot;
		Shape3D col_shape_resource;
		Aabb aabb;
		Vector3 aabb_size;
		switch (colliderType)
		{
			case ColliderType.Convex:
				col_shape_resource = lodMesh.CreateConvexShape();
				GD.Print($"Convex Points Count = {((ConvexPolygonShape3D)col_shape_resource).Points.Length}");
				break;
			case ColliderType.Convex_Simplified:
				col_shape_resource = lodMesh.CreateConvexShape(true, true);
				GD.Print($"Convex_Simplified Points Count = {((ConvexPolygonShape3D)col_shape_resource).Points.Length}");
				break;

			case ColliderType.Trimesh:
				col_shape_resource = lodMesh.CreateTrimeshShape();
				GD.Print($"Concave (Trimesh) Faces Count = {((ConcavePolygonShape3D)col_shape_resource).GetFaces().Length}");
				break;
			case ColliderType.Box:
				BoxShape3D boxShape = new();
				col_shape_resource = boxShape;
				aabb = lodMesh.GetAabb();
				boxShape.Size = aabb.Size;
				col_shape.Position = aabb.GetCenter();
				GD.Print($"Primitive Box Collider");
				break;
			case ColliderType.Cylinder:
				CylinderShape3D cylinderShape = new();
				col_shape_resource = cylinderShape;
				aabb = lodMesh.GetAabb();
				aabb_size = aabb.Size;
				cylinderShape.Height = aabb_size.Y;
				cylinderShape.Radius = MathF.Sqrt(aabb_size.X * aabb_size.X + aabb_size.Z * aabb_size.Z) * 0.5f;
				col_shape.Position = aabb.GetCenter();
				GD.Print($"Primitive Cylinder Collider");
				break;
			case ColliderType.Sphere:
				SphereShape3D sphereShape = new();
				col_shape_resource = sphereShape;
				aabb = lodMesh.GetAabb();
				aabb_size = aabb.Size;
				sphereShape.Radius = MathF.Sqrt(aabb_size.X * aabb_size.X + aabb_size.Z * aabb_size.Z) * 0.5f;
				col_shape.Position = aabb.GetCenter();
				GD.Print($"Primitive Sphere Collider");
				break;

			default: col_shape_resource = new BoxShape3D(); break;
		}
		col_shape.Shape = col_shape_resource;
		col_shape.Shape.TakeOverPath(SavePath_col);
		Error err_COL = ResourceSaver.Save(col_shape.Shape, SavePath_col);
		if (err == Error.Ok)
		{
			GD.Print($"Successfully saved LOD {SelectedLOD} mesh to: {SavePath}");
		}
		else
		{
			GD.PrintErr($"Failed to save mesh. Error: {err}");
		}

		return lodMesh;
	}
}
