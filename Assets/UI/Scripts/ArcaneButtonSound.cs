using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    [DisallowMultipleComponent]
    public sealed class ArcaneButtonSound : MonoBehaviour
    {
        private Button button;
        private void Awake(){button=GetComponent<Button>();button.onClick.AddListener(Click);}
        private void Click(){if(button!=null&&button.isActiveAndEnabled&&button.IsInteractable())ArcaneUIAudio.Play(ArcaneUIAudio.Cue.Click);}
        private void OnDestroy(){if(button!=null)button.onClick.RemoveListener(Click);}
    }
}
