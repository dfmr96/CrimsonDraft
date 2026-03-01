#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "CrimsonDraft/Combat/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField] private string enemyId = string.Empty;
        [SerializeField] private Sprite sprite  = null!;

        public string EnemyId => this.enemyId;
        public Sprite Sprite   => this.sprite;
    }
}
