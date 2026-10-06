using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class ShapeCreationProcess : ScriptableObject
    {
        public abstract GameObject Create(string name, Transform parent, int layer, ShapeData[] shape, Matrix4x4 trs);
    }
}