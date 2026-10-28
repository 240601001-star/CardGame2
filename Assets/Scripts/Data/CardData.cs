using UnityEngine;

namespace CardGame.Data
{
    /// <summary>
    /// ScriptableObject that defines a single card's identity and stats.
    /// Each instance maps 1-to-1 with an ArUco marker ID (0–9).
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "CardGame/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Must match the ArUco marker ID printed on the physical card (0–9).")]
        [Range(0, 9)]
        public int cardID;

        [Tooltip("Display name shown in UI.")]
        public string cardName;

        [Header("Stats")]
        [Min(0)]
        public int manaCost;

        [Min(0)]
        public int attackPower;

        [Min(1)]
        public int health;

        [Header("Visuals")]
        [Tooltip("3D prefab spawned on the board when the card is played.")]
        public GameObject cardPrefab;

        [Tooltip("2D icon displayed in the UI card-info panel.")]
        public Sprite cardIcon;

        public override string ToString()
        {
            return $"[Card #{cardID}] {cardName}  Mana:{manaCost}  ATK:{attackPower}  HP:{health}";
        }
    }
}
