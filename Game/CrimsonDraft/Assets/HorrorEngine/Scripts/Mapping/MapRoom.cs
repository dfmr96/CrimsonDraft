using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    // Keep this sorted in completion order
    public enum MapRoomState
    {
        Unknown,
        NotVisited,
        Visited,
        Completed
    }

    [RequireComponent(typeof(Shape))]
    public class MapRoom : MapElement, ISavableObjectStateExtra
    {
        public string Name;
        public bool AutoLinkChildren = true;
        public List<MapElement> LinkedElements;
        public MapRoomCompletionStep[] CompletionSteps;

        private CompositeShape m_CompShape;
        private MapController m_MapCtrl;

        private SavableObjectState m_Savable;

        public MapRoomState State { get; private set; }

        
        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Savable = GetComponent<SavableObjectState>();
            m_MapCtrl = GetComponentInParent<MapController>();
            Debug.Assert(m_MapCtrl, "MapRoom should be a child of an object with a MapController component");

            var childElements = this.GetComponentsInChildren<MapElement>();
            foreach(var element in childElements)
            {
                if (element != this && !LinkedElements.Contains(element))
                    LinkedElements.Add(element);
            }
        }

        
        // --------------------------------------------------------------------

        public Bounds GetBounds()
        {
            Bounds bounds = new Bounds();
            bounds.center = transform.position;

            var shapes = GetComponentsInChildren<Shape>();
            foreach (var shape in shapes)
            {
                foreach (var p in shape.Points)
                    bounds.Encapsulate(shape.transform.TransformPoint(p));
            }

            return bounds;
        }

        // --------------------------------------------------------------------

        public bool ContainsWorldPosition(Vector3 worldPos)
        {
            if (m_CompShape == null)
            {
                Shape shape = GetComponent<Shape>();
                if (shape)
                {
                    Shape[] shapes = { shape };
                    m_CompShape = new CompositeShape(shapes);
                    m_CompShape.GetMesh();
                }
            }

            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            return m_CompShape.ContainsPoint(localPos.ToXZ());
        }

        // --------------------------------------------------------------------

        public void TryMarkAs(MapRoomState state)
        {
            if (State < state)
            {
                State = state;

                m_Savable.SaveState(); // Force an immediate save of the room state so the map reflects the change
            }
        }

        // --------------------------------------------------------------------

        public bool CheckIfCompleted()
        {
            if (State < MapRoomState.Visited)
                return false;

            foreach (var step in CompletionSteps)
                if (!step.IsCompleted)
                    return false;

            return true;
        }


        // --------------------------------------------------------------------

        public void OnDrawGizmosSelected()
        {
            GizmoUtils.DrawArrow(transform.position, transform.forward, 1.0f, 1.0f);
        }

        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------

        public string GetSavableData()
        {
            return State.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            State = Enum.Parse<MapRoomState>(savedData);
        }
    }
}