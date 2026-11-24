using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>Small authored UI cues, independent of gameplay and controlled by master volume.</summary>
    public sealed class ArcaneUIAudio : MonoBehaviour
    {
        public enum Cue { Click, Card, Scan, Turn, Swing, Impact, Win }
        private static ArcaneUIAudio instance;
        private readonly Dictionary<Cue,AudioClip> clips=new Dictionary<Cue,AudioClip>();
        private readonly Dictionary<Cue,float> played=new Dictionary<Cue,float>();
        private AudioSource[] voices;
        private AudioListener fallbackListener;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if(instance!=null)return;
            var go=new GameObject("Arcane UI Audio");DontDestroyOnLoad(go);instance=go.AddComponent<ArcaneUIAudio>();
        }
        private void Awake()
        {
            voices=new AudioSource[4];
            for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;voices[i].volume=.65f;voices[i].priority=128;}
            foreach(Cue cue in Enum.GetValues(typeof(Cue)))clips[cue]=Resources.Load<AudioClip>("UI/Audio/"+cue);
            fallbackListener=gameObject.AddComponent<AudioListener>();fallbackListener.enabled=false;
            SceneManager.sceneLoaded+=SceneLoaded;SceneManager.sceneUnloaded+=SceneUnloaded;
        }
        private void SceneUnloaded(Scene scene){foreach(var voice in voices)voice.Stop();played.Clear();}
        private void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            bool other=false;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(listener!=fallbackListener&&listener.isActiveAndEnabled)other=true;
            fallbackListener.enabled=!other;
            foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))Bind(button);
        }
        public static void Bind(Button button)
        {
            ArcaneUIFactory.StyleButton(button);
            if(button.GetComponent<ArcaneButtonSound>()==null)button.gameObject.AddComponent<ArcaneButtonSound>();
        }
        public static void Play(Cue cue)
        {
            if(!Application.isPlaying||AudioListener.volume<=0||AudioListener.pause)return;
            if(instance==null)Boot();
            if(!instance.clips.TryGetValue(cue,out var clip)||clip==null)return;
            if(instance.played.TryGetValue(cue,out float last)&&Time.unscaledTime-last<.1f)return;
            AudioSource available=null;
            foreach(var voice in instance.voices)if(!voice.isPlaying){available=voice;break;}
            if(available==null)return; // Keep overlap bounded instead of stacking loud one-shots.
            instance.played[cue]=Time.unscaledTime;available.clip=clip;available.Play();
        }
        private void OnDestroy()
        {SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneUnloaded-=SceneUnloaded;if(instance==this)instance=null;}
    }
}
