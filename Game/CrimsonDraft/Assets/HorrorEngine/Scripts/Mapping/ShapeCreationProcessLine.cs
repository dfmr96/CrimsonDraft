using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "ShapeCreationProcessLine", menuName = "Horror Engine/Mapping/ShapeCreationLine")]
    public class ShapeCreationProcessLine : ShapeCreationProcess
    {
        public Material Material;
        public float Thickness;
        public bool Loop;

        public override GameObject Create(string name, Transform parent, int layer, ShapeData[] shapes, Matrix4x4 trs)
        {
            GameObject go = new GameObject(name + "_Line");
            go.transform.parent = parent;
            go.transform.position = Vector3.zero;
            go.layer = layer;

            foreach (var shape in shapes)
            {
                Vector3[] vertices = new Vector3[shape.Points.Length];
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 pos = trs.MultiplyPoint(shape.Points[i]);
                    vertices[i] = go.transform.InverseTransformPoint(pos);
                }

                // Walls
                LineRenderer lineRenderer = go.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.positionCount = vertices.Length;
                lineRenderer.SetPositions(vertices);
                lineRenderer.loop = Loop;
                lineRenderer.sharedMaterial = Material;
                lineRenderer.widthMultiplier = Thickness;
            }

            return go;
        }
    }
}