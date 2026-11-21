using System.Collections;
using System.Collections.Generic;
using CardGame.Core;
using CardGame.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardGame.UI
{
    public sealed class ArcaneSceneNavigation : MonoBehaviour
    {
        private GameObject confirmation;
        private bool leaving;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded-=SceneLoaded;
            SceneManager.sceneLoaded+=SceneLoaded;
        }
        private static void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!=ArcaneMainMenu.DuelSceneName)return;
            var go=new GameObject("SceneNavigation");go.AddComponent<ArcaneSceneNavigation>();
        }
        private void Awake()
        {
            var canvas=ArcaneMainMenu.CreateCanvas("NavigationCanvas",50);canvas.SetParent(transform,false);canvas=ArcaneUIFactory.SafeContent(canvas);
            var menuButton=ArcaneMainMenu.Action("MainMenuButton",canvas,"ANA MENÜ",.86f,.003f,.985f,.052f,()=>confirmation.SetActive(true));
            var menuLabel=menuButton.GetComponentInChildren<TMPro.TMP_Text>();menuLabel.fontSizeMax=22;menuLabel.fontSizeMin=16;
            confirmation=ArcaneMainMenu.Box("LeaveMatchConfirmation",canvas,0,0,1,1,new Color(0,0,0,.86f)).gameObject;
            confirmation.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var panel=ArcaneMainMenu.Box("Dialog",confirmation.transform,.25f,.30f,.75f,.70f,ArcaneMainMenu.Panel);
            ArcaneMainMenu.Label("Title",panel,"ANA MENÜYE DÖN?",.07f,.65f,.93f,.90f,30,ArcaneMainMenu.Gold);
            ArcaneMainMenu.Label("Description",panel,"Devam eden maç sona erecek.",.07f,.40f,.93f,.65f,20,Color.white);
            ArcaneMainMenu.Action("CancelReturnButton",panel,"VAZGEÇ",.07f,.10f,.48f,.32f,()=>confirmation.SetActive(false));
            ArcaneMainMenu.Action("ConfirmReturnButton",panel,"ANA MENÜ",.52f,.10f,.93f,.32f,ReturnToMenu);
            confirmation.SetActive(false);
        }
        public void ReturnToMenu() { if(!leaving){leaving=true;StartCoroutine(Return());} }
        private IEnumerator Return()
        {
            // Existing managers persist across scenes. Dispose this match before a fresh duel.
            var managers=new HashSet<GameObject>();
            if(GameManager.Instance!=null)managers.Add(GameManager.Instance.gameObject);
            if(CardDatabaseManager.Instance!=null)managers.Add(CardDatabaseManager.Instance.gameObject);
            foreach(var manager in managers)Destroy(manager);
            yield return null;
            SceneManager.LoadSceneAsync(ArcaneMainMenu.SceneName);
        }
    }
}
