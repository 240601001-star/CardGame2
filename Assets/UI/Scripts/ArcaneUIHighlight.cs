using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>Presentation only: never decides whether an action is legal.</summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneUIHighlight : MonoBehaviour
    {
        public enum Kind { None, Playable, Ready, Selected, Target }
        public Kind State { get; private set; }
        private Outline outline;
        private Color tint;
        private GameObject corners;
        private Image[] cornerImages;
        public static void Show(GameObject target,Kind state)
        {
            var view=target.GetComponent<ArcaneUIHighlight>();
            if(view==null)view=target.AddComponent<ArcaneUIHighlight>();
            view.Set(state);
        }
        public void Set(Kind state)
        {
            State=state;
            if(outline==null){outline=gameObject.AddComponent<Outline>();outline.useGraphicAlpha=true;}
            bool strong=state==Kind.Selected||state==Kind.Target;
            outline.enabled=state!=Kind.None&&!strong;
            if(strong&&corners==null)CreateCorners();
            if(corners!=null)corners.SetActive(strong);
            tint=state==Kind.Target?new Color(.35f,.90f,1f):state==Kind.Selected?ArcaneUIFactory.Gold:new Color(.35f,.85f,.55f);
            outline.effectDistance=state==Kind.Selected||state==Kind.Target?new Vector2(3,3):new Vector2(1.5f,1.5f);
            Apply();
        }
        private void CreateCorners()
        {
            corners=new GameObject("FeedbackCorners",typeof(RectTransform));corners.transform.SetParent(transform,false);
            var root=corners.GetComponent<RectTransform>();ArcaneUIFactory.Rect(root,0,0,1,1);root.offsetMin=new Vector2(-4,-4);root.offsetMax=new Vector2(4,4);
            cornerImages=new Image[8];
            for(int i=0;i<8;i++)
            {
                int corner=i/2;bool right=(corner&1)!=0,top=(corner&2)!=0;
                var go=new GameObject("Corner",typeof(RectTransform),typeof(Image));go.transform.SetParent(root,false);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(right?1:0,top?1:0);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=i%2==0?new Vector2(20,3):new Vector2(3,20);
                cornerImages[i]=go.GetComponent<Image>();cornerImages[i].raycastTarget=false;
            }
        }
        private void Update(){if(State!=Kind.None)Apply();}
        private void Apply()
        {
            if(outline==null)return;
            var color=tint;
            color.a=State==Kind.Selected||State==Kind.Target?.7f+.3f*Mathf.Sin(Time.unscaledTime*6):.55f;
            outline.effectColor=color;
            if(cornerImages!=null)foreach(var image in cornerImages)image.color=color;
        }
    }
}
