using UnityEngine;
using UnityEngine.UI;
namespace CardGame.UI {
    /// <summary>Keeps the 1920x1080 safe composition and its text proportional when the viewport changes.</summary>
    [ExecuteAlways,RequireComponent(typeof(CanvasScaler))]
    public sealed class ArcaneCanvasScale : MonoBehaviour {
        private float lastAspect=-1;
        private void OnEnable(){Apply();}
        private void Update(){Apply();}
        private void Apply(){float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);if(Mathf.Approximately(lastAspect,aspect))return;lastAspect=aspect;GetComponent<CanvasScaler>().matchWidthOrHeight=aspect>=16f/9f?1:0;}
    }
}
