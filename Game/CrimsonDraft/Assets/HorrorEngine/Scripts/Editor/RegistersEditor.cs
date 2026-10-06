using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    public class RegistersEditor : MonoBehaviour
    {
        [MenuItem("Horror Engine/Registers/Update All")]
        private static void UpdateAllRegisters()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(RegisterDatabase).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RegisterDatabase dataRegister = (RegisterDatabase)AssetDatabase.LoadAssetAtPath(path, typeof(RegisterDatabase));
                dataRegister.UpdateDatabase();
            }
        }
    }
}