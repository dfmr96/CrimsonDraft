using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(SavableObjectState))]
    public class SavableAnimator : MonoBehaviour, ISavableObjectStateExtra
    {
        [Serializable]
        private struct AnimatorData
        {
            public int DefaultLayerStateAnimHash;
            public float DefaultLayerStateNormalizedTime;
            public AnimatorParamData[] Params;
        }

        [Serializable]
        private struct AnimatorParamData
        {
            public int Hash;
            public AnimatorControllerParameterType Type;
            public string Value;
        }

        private Animator m_Animator;

        void Awake()
        {
            m_Animator = GetComponent<Animator>();
            
        }

        public string GetSavableData()
        {
            AnimatorData data;
            AnimatorStateInfo stateInfo = m_Animator.GetCurrentAnimatorStateInfo(0);
            data.DefaultLayerStateAnimHash = stateInfo.shortNameHash;
            data.DefaultLayerStateNormalizedTime = stateInfo.normalizedTime;

            AnimatorParamData[] paramsData = new AnimatorParamData[m_Animator.parameterCount];
            for(int i =0; i < m_Animator.parameterCount; ++i)
            {
                AnimatorControllerParameter animParam = m_Animator.GetParameter(i);

                string value = "";
                switch (animParam.type)
                {
                    case AnimatorControllerParameterType.Float:
                        value = m_Animator.GetFloat(animParam.nameHash).ToString();
                        break;
                    case AnimatorControllerParameterType.Int:
                        value = m_Animator.GetInteger(animParam.nameHash).ToString();
                        break;
                    case AnimatorControllerParameterType.Bool:
                        value = m_Animator.GetBool(animParam.nameHash).ToString();
                        break;
                }

                AnimatorParamData paramData = new AnimatorParamData()
                {
                    Hash = animParam.nameHash,
                    Type = animParam.type,
                    Value = value
                };

                paramsData[i] = paramData;
            }

            data.Params = paramsData;
            
            return JsonUtility.ToJson(data);
        }

        public void SetFromSavedData(string savedData)
        {
            AnimatorData data = JsonUtility.FromJson<AnimatorData>(savedData);

            for (int i = 0; i < data.Params.Length; ++i)
            {
                AnimatorParamData paramData = data.Params[i];
                switch (paramData.Type)
                {
                    case AnimatorControllerParameterType.Float:
                        m_Animator.SetFloat(paramData.Hash, (float)Convert.ToDouble(paramData.Value));
                        break;
                    case AnimatorControllerParameterType.Int:
                        m_Animator.SetInteger(paramData.Hash, Convert.ToInt32(paramData.Value));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        m_Animator.SetBool(paramData.Hash, Convert.ToBoolean(paramData.Value));
                        break;
                }
            }

            m_Animator.Play(data.DefaultLayerStateAnimHash, 0, data.DefaultLayerStateNormalizedTime);
        }

    }
}