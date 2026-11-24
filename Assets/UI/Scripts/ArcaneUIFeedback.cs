using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>Short, non-blocking effects on a separate, non-interactive UI layer.</summary>
    public sealed class ArcaneUIFeedback : MonoBehaviour
    {
        private RectTransform layer;
        public int ActiveEffects { get {int count=0;if(layer!=null)foreach(Transform child in layer)if(child.gameObject.activeSelf)count++;return count;} }
        private static readonly Color DamageColor=new Color(1,.32f,.25f);
        public static Vector3 Center(Transform target)
        {
            var rect=target as RectTransform;
            return rect!=null?rect.TransformPoint(rect.rect.center):target.position;
        }
        private RectTransform Layer
        {
            get
            {
                if(layer==null)
                {
                    var go=new GameObject("FeedbackLayer",typeof(RectTransform),typeof(CanvasGroup));
                    go.transform.SetParent(transform,false);layer=go.GetComponent<RectTransform>();
                    ArcaneUIFactory.Rect(layer,0,0,1,1);
                    var group=go.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
                }
                layer.SetAsLastSibling();return layer;
            }
        }
        public void Popup(string value,Vector3 world,Color color)
        {
            if(!Application.isPlaying||!isActiveAndEnabled)return;
            var go=Instantiate(ArcaneUITheme.Current.textPrefab,Layer,false);go.name="FeedbackPopup";
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(300,64);rect.position=world;
            var text=go.GetComponent<TMP_Text>();text.text=value;text.font=ArcaneUITheme.Current.bodyFont;text.fontSize=28;text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=28;text.fontStyle=FontStyles.Bold;text.color=color;text.raycastTarget=false;
            var shadow=go.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(2,-2);
            // Keep the complete floating label inside the safe composition, including 4:3 layouts.
            Vector2 position=rect.anchoredPosition;var area=Layer.rect;
            position.x=Mathf.Clamp(position.x,area.xMin+180,area.xMax-180);
            position.y=Mathf.Clamp(position.y,area.yMin+40,area.yMax-90);rect.anchoredPosition=position;
            StartCoroutine(Float(text,rect));
        }
        public void Damage(int amount,Vector3 world,string actor=null)
        {
            if(amount>0&&Application.isPlaying&&isActiveAndEnabled)StartCoroutine(ImpactPopup(amount,world,actor));
        }
        private IEnumerator ImpactPopup(int amount,Vector3 world,string actor)
        {
            yield return new WaitForSecondsRealtime(.2f);
            Popup("-"+amount+(string.IsNullOrEmpty(actor)?"":"\n"+actor),world,DamageColor);
        }
        public void Strike(Vector3 from,Vector3 to)
        {
            if(!Application.isPlaying||!isActiveAndEnabled)return;
            ArcaneUIAudio.Play(ArcaneUIAudio.Cue.Swing);
            var go=new GameObject("AttackTrail",typeof(RectTransform),typeof(Image));go.transform.SetParent(Layer,false);
            var image=go.GetComponent<Image>();image.sprite=ArcaneUITheme.Current.Get("IconAttack");image.raycastTarget=false;image.preserveAspect=true;
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(58,58);rect.position=from;
            StartCoroutine(Travel(image,rect,Layer.InverseTransformPoint(to)));
        }
        public void Played(Vector3 world){ArcaneUIAudio.Play(ArcaneUIAudio.Cue.Card);Popup("KART OYNANDI",world,ArcaneUIFactory.Gold);}
        public void Clear()
        {
            StopAllCoroutines();
            if(layer==null)return;
            for(int i=layer.childCount-1;i>=0;i--)
            {
                var go=layer.GetChild(i).gameObject;go.SetActive(false);
                if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);
            }
        }
        private IEnumerator Float(TMP_Text text,RectTransform rect)
        {
            Vector2 start=rect.anchoredPosition;Color color=text.color;
            for(float t=0;t<.8f;t+=Time.unscaledDeltaTime)
            {
                float p=t/.8f;rect.anchoredPosition=start+Vector2.up*(48*p);rect.localScale=Vector3.one*(1+.14f*Mathf.Sin(p*Mathf.PI));
                text.color=new Color(color.r,color.g,color.b,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,p)));yield return null;
            }
            Destroy(rect.gameObject);
        }
        private IEnumerator Travel(Image image,RectTransform rect,Vector3 destination)
        {
            Vector3 start=rect.localPosition;
            for(float t=0;t<.24f;t+=Time.unscaledDeltaTime)
            {
                float p=t/.24f;rect.localPosition=Vector3.Lerp(start,destination,Mathf.SmoothStep(0,1,p));
                rect.localScale=Vector3.one*(1+.25f*Mathf.Sin(p*Mathf.PI));yield return null;
            }
            rect.localPosition=destination;
            ArcaneUIAudio.Play(ArcaneUIAudio.Cue.Impact);StartCoroutine(Sparks(rect.position));
            for(float t=0;t<.16f;t+=Time.unscaledDeltaTime)
            {
                float p=t/.16f;rect.localScale=Vector3.one*(1+p*.6f);image.color=new Color(1,.65f,.3f,1-p);yield return null;
            }
            Destroy(rect.gameObject);
        }
        private IEnumerator Sparks(Vector3 world)
        {
            var root=new GameObject("ImpactSparks",typeof(RectTransform));root.transform.SetParent(Layer,false);root.transform.position=world;
            var pieces=new Image[6];
            for(int i=0;i<pieces.Length;i++){
                var go=new GameObject("Spark",typeof(RectTransform),typeof(Image));go.transform.SetParent(root.transform,false);
                pieces[i]=go.GetComponent<Image>();pieces[i].raycastTarget=false;
                var r=go.GetComponent<RectTransform>();r.sizeDelta=new Vector2(3,15);r.localEulerAngles=new Vector3(0,0,i*60-90);
            }
            for(float t=0;t<.25f;t+=Time.unscaledDeltaTime){float p=t/.25f;
                for(int i=0;i<pieces.Length;i++){float angle=i*Mathf.PI/3;pieces[i].rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(8+32*p);pieces[i].color=new Color(1,.75f,.32f,1-p);}
                yield return null;
            }
            Destroy(root);
        }
        private void OnDisable(){Clear();}
    }
}
