using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public struct StatusEffectSaveData
    {
        public string UniqueId;
        public float RemainingTime;
    }

    public class StatusEffectHandler : MonoBehaviour, ISavableObjectStateExtra
    {
        private Dictionary<StatusEffect, StatusEffect> m_HashedEffects = new Dictionary<StatusEffect, StatusEffect>();

        private List<StatusEffect> m_EffectsToRemove = new List<StatusEffect>();

        // As a consecuence of one of the StatusEffect ticks ClearEffects can be called, but we can't do this during the update.
        // These variables help to sync the clear
        private bool m_IsUpdating;
        private bool m_WasClearCalledDuringUpdate;

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            ClearAllStatusEffects();
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            UpdateEffects(false);
        }

        // --------------------------------------------------------------------

        private void FixedUpdate()
        {
            UpdateEffects(true);
        }

        // --------------------------------------------------------------------

        void UpdateEffects(bool fixedTime)
        {
            m_EffectsToRemove.Clear();

            m_IsUpdating = true;
            m_WasClearCalledDuringUpdate = false;

            foreach (var effectPair in m_HashedEffects)
            {
                var effect = effectPair.Value;

                if (effect.ShouldTick() && !effect.HasFinished)
                {
                    if (fixedTime)
                        effect.FixedTimeTick();
                    else
                        effect.Tick();
                }

                if (effect.HasFinished)
                {
                    effect.EndEffect();
                    if (!m_EffectsToRemove.Contains(effectPair.Key))
                        m_EffectsToRemove.Add(effectPair.Key);
                }
            }

            

            m_IsUpdating = false;

            if (m_WasClearCalledDuringUpdate)
            {
                ClearAllStatusEffects();
            }
            else
            {
                foreach (var effect in m_EffectsToRemove)
                {   
                    RemoveStatusEffect(effect);
                }
            }

        }

        // --------------------------------------------------------------------

        public StatusEffect AddStatusEffect(StatusEffect effect)
        {
            if (effect == null || m_HashedEffects.ContainsKey(effect))
            {
                return null;
            }

            // The SO needs to be instantiated here so we don't change its asset data
            var effectInstance = Instantiate(effect);
            effectInstance.StartEffect(this);
            m_HashedEffects.Add(effect, effectInstance);

            enabled = true;

            return effectInstance;
        }

        // --------------------------------------------------------------------

        public void RemoveStatusEffect(StatusEffect effectToRemove)
        {
            if (!m_IsUpdating)
            {
                m_HashedEffects.Remove(effectToRemove);

                if (m_HashedEffects.Count == 0)
                    enabled = false;
            }
            else
            {
                if (!m_EffectsToRemove.Contains(effectToRemove))
                    m_EffectsToRemove.Add(effectToRemove);
            }
        }

        // --------------------------------------------------------------------

        public void ClearAllStatusEffects()
        {
            if (!m_IsUpdating)
            {
                foreach (var effect in m_HashedEffects)
                {
                    effect.Value.EndEffect();
                }

                m_HashedEffects.Clear();
                enabled = false;
            }
            else
            {
                m_WasClearCalledDuringUpdate = true;
            }
        }


        //-----------------------------------------------------
        // ISavable implementation
        //-----------------------------------------------------

        public string GetSavableData()
        {

            List<StatusEffectSaveData> effects = new List<StatusEffectSaveData>();

            foreach (var effectPair in m_HashedEffects)
            {
                StatusEffect effect = effectPair.Key;

                StatusEffectSaveData effectSavedData = new StatusEffectSaveData
                {
                    UniqueId = effect.UniqueId,
                    RemainingTime = effect.RemainingTime
                };
                effects.Add(effectSavedData);
            }


            string saveData = JsonUtility.ToJson(effects);

            return saveData;
        }

        public void SetFromSavedData(string savedData)
        {
            List<StatusEffectSaveData> statusEffectData = JsonUtility.FromJson<List<StatusEffectSaveData>>(savedData);

            ClearAllStatusEffects();

            if (statusEffectData != null)
            {
                var effectsDB = GameManager.Instance.GetDatabase<StatusEffectDatabase>();
                if (effectsDB)
                {
                    foreach (var savedEffect in statusEffectData)
                    {
                        var effect = effectsDB.GetRegister(savedEffect.UniqueId);
                        if (effect != null)
                        {
                            var effectInstance = AddStatusEffect(effect);
                            effectInstance.RemainingTime = savedEffect.RemainingTime;
                        }
                        else
                        {
                            Debug.LogError("Status effect couldn't be loaded. Make sure it exists in the database");
                        }
                    }
                }
            }
        }

    }
}
