using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "ShapeCreationProcessPolygon", menuName = "Horror Engine/Mapping/ShapeCreationPolygon")]
    public class ShapeCreationProcessPolygon : ShapeCreationProcess
    {
        public Material Material;

        public override GameObject Create(string name, Transform parent, int layer, ShapeData[] shape, Matrix4x4 trs)
        {
            GameObject go = new GameObject(name + "_Mesh");
            go.transform.parent = parent;
            go.transform.position = Vector3.zero;
            go.layer = layer;

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = Material;

            var compShape = new CompositeShape(shape);
            Mesh mesh = Instantiate(compShape.GetMesh());
            Vector3[] vertices = mesh.vertices;

            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = trs.MultiplyPoint(vertices[i]);
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;

            return go;
        }
    }
}