using System;
using TMPro;
using UnityEngine;

namespace CardGame.UI
{
    [CreateAssetMenu(menuName="Arcane Duel/UI Theme")]
    public sealed class ArcaneUITheme : ScriptableObject
    {
        [Serializable] public struct Artwork { public string key; public Sprite sprite; }
        public Artwork[] artwork;
        public TMP_FontAsset bodyFont,titleFont;
        public GameObject panelPrefab,buttonPrefab,textPrefab,hpPrefab,scorePrefab,cardPrefab,resultPrefab,scannerPrefab,brandPrefab;
        private static ArcaneUITheme current;
        public static ArcaneUITheme Current => current!=null?current:current=Resources.Load<ArcaneUITheme>("UI/ArcaneTheme");
        public static void Use(ArcaneUITheme theme){current=theme;}
        public Sprite Get(string key)
        {
            foreach(var entry in artwork)if(entry.key==key)return entry.sprite;
            throw new InvalidOperationException("Missing final UI sprite: "+key);
        }
    }
}
