using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    public abstract class RegisterDatabase : ScriptableObject
    {
        public abstract void HashRegisters();

#if UNITY_EDITOR
        public abstract void UpdateDatabase();
#endif
    }

    public abstract class RegisterDatabase<T> : RegisterDatabase where T : Register
    {
        public RegisterDatabase<T> Prototype;
        public List<T> Registers = new List<T>();

        private readonly Dictionary<string, T> m_HashedRegisters = new Dictionary<string, T>();

        // --------------------------------------------------------------------

        public override void HashRegisters()
        {
            Debug.Assert(this != Prototype, "RegisterDatabase is referencing at itself as Prototype", this);

            m_HashedRegisters.Clear();

            if (Prototype)
            {
                foreach (T reg in Prototype.Registers)
                {
                    Debug.Assert(reg != null, $"There is an null entry in the database {Prototype.name}");
                    if (reg)
                    {
                        m_HashedRegisters[reg.UniqueId] = reg;
                    }
                }
            }

            foreach (T reg in Registers)
            {
                Debug.Assert(reg != null, $"There is an null entry in the database {this.name}");
                if (reg)
                {
                    m_HashedRegisters[reg.UniqueId] = reg;
                }
            }
        }

        // --------------------------------------------------------------------

        public T GetRegister(string uniqueId)
        {
            return m_HashedRegisters[uniqueId];
        }


        // --------------------------------------------------------------------

#if UNITY_EDITOR
        public override void UpdateDatabase()
        {
            UpdateDatabase(this);
            CheckUniqueIds();
        }

        // --------------------------------------------------------------------

        public static void UpdateDatabase(RegisterDatabase<T> register)
        {
            register.Registers.Clear();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T data = (T)AssetDatabase.LoadAssetAtPath(path, typeof(T));
                register.Registers.Add(data);
            }
            EditorUtility.SetDirty(register);
            register.HashRegisters();
        }

        public void CheckUniqueIds()
        {
            Dictionary<string, T> objs = new Dictionary<string, T>();
            foreach (var r in Registers)
            {
                if (!objs.ContainsKey(r.UniqueId))
                    objs[r.UniqueId] = r;
                else
                {
                    Debug.LogError("DataRegister : Id is already in use " + r.UniqueId + " by : " + objs[r.UniqueId].name + ", duplicated by:" + r.name);
                }
            }
        }

        // --------------------------------------------------------------------

#endif
    }
}