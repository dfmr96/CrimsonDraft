using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class HealthDepletedEvent : UnityEvent<Health> { }

    [Serializable]
    public class HealthAlteredEvent : UnityEvent<float, float> { }

    [Serializable]
    public class HealthDecreasedEvent : UnityEvent<float> { }

    public struct HealthSaveData
    {
        public float Value;
    }

    public class Health : MonoBehaviour, IResetable, ISavableObjectStateExtra
    {
        public bool Infinite;
        public bool Invulnerable;
        public float Max;
        public float Min;
        public float Value;
        [Tooltip("Optional value that will used on reset. Leave to 0 to reset to Max")]
        public float InitialValue;

        public HealthAlteredEvent OnHealthAltered = new HealthAlteredEvent();
        public HealthDecreasedEvent OnHealthDecreased = new HealthDecreasedEvent();
        public HealthDepletedEvent OnDeath = new HealthDepletedEvent();
        public UnityEvent OnLoadedDead;

        public Damageable LastDamageableHit { get; private set; }
        public AttackBase LastInstigatorAttack { get; private set; }
        public float Normalized { get { return Value / Max; } }
        public bool IsDead { get { return Value <= 0; } }
        

        // --------------------------------------------------------------------

        public void OnReset()
        {
            Value = InitialValue > 0 ? InitialValue : Max;
        }

        // --------------------------------------------------------------------

        public void Kill()
        {
            SetHealth(0);
        }

        // --------------------------------------------------------------------

        public void TakeDamage(float amount, Damageable damageable = null, AttackBase instigator = null)
        {
            LastInstigatorAttack = instigator;
            LastDamageableHit = damageable;

            if (Invulnerable)
                return;

            if (Infinite)
                Value += amount;

            SetHealth(Value - amount);
        }

        // --------------------------------------------------------------------

        public void Regenerate(float amount)
        {
            SetHealth(Value + amount);
        }

        // --------------------------------------------------------------------

        public void RegenerateAll()
        {
            SetHealth(Max);
        }

        // --------------------------------------------------------------------

        private void SetHealth(float value)
        {
            float prev = Value;
            Value = Mathf.Clamp(value, Min, Max);

            if (prev != Value && OnHealthAltered != null)
                OnHealthAltered?.Invoke(prev, Value);

            if (prev > Value && OnHealthDecreased != null)
                OnHealthDecreased?.Invoke(Value);

            if (IsDead)
                OnDeath?.Invoke(this);
        }

        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------

        string ISavable<string>.GetSavableData()
        {
            return Value.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            Value = (float)Convert.ToDouble(savedData);
            if (Value <= 0)
            {
                OnLoadedDead?.Invoke();
            }
        }


        // --------------------------------------------------------------------


    }
}