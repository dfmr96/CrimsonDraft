using System;
using UnityEngine;

namespace HorrorEngine
{
    public class ShaderGlobalFloatSetterOnUpdate : ShaderGlobalFloatSetter
    {
        void Update()
        {
            Shader.SetGlobalFloat(m_PropertyHash, m_Value);
        }
    }
}