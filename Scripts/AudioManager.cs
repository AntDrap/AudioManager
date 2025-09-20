using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using static Unity.VisualScripting.Member;
using static UnityEngine.Rendering.DebugUI;
using Random = UnityEngine.Random;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    private static Dictionary<string, List<AudioSource>> playingClips = new Dictionary<string, List<AudioSource>>();

    private static Dictionary<string, AudioMixerGroup> mixerGroups = new Dictionary<string, AudioMixerGroup>();
    private Dictionary<AudioSource, AudioCoroutine> audioSourceCoroutine = new Dictionary<AudioSource, AudioCoroutine>();

    [SerializeField] private List<AudioSource> audioSourcePool = new List<AudioSource>();
    private int totalAudioCount;

    private class AudioCoroutine
    {
        public float timeLeft;
        public bool loop;

        public AudioCoroutine(AudioClipHolder audioClipHolder, AudioClip audioClip)
        {
            this.timeLeft = audioClipHolder.GetClipTime(audioClip);
            this.loop = audioClipHolder.loop;
        }

        public void StopCoroutine()
        {
            timeLeft = 0;
            loop = false;
        }
    }

    private AudioSource GetSource()
    {
        if (audioSourcePool.Count == 0)
        {
            AddNewSource();
        }

        AudioSource source = audioSourcePool[0];
        audioSourcePool.RemoveAt(0);

        source.gameObject.SetActive(true);

        return source;
    }

    private void AddNewSource()
    {
        GameObject source = AudioConfigObject.instance.CreateAudioSource();

        source.name = "AudioSource_" + totalAudioCount;
        source.transform.parent = transform;

        audioSourcePool.Add(source.GetComponent<AudioSource>());

        totalAudioCount++;
    }

    private void ReturnSource(string clipName, AudioSource audioSource)
    {
        audioSource.Stop();
        audioSourcePool.Add(audioSource);

        GetPlayingSources(clipName).Remove(audioSource);

        if (audioSourceCoroutine.ContainsKey(audioSource))
        {
            audioSourceCoroutine.Remove(audioSource);
        }

        audioSource.gameObject.SetActive(false);
    }

    public static void Initialize()
    {
        GameObject audio = new GameObject("AudioSourceHolder");

        instance = audio.AddComponent<AudioManager>();

        GameObject.DontDestroyOnLoad(instance);

        foreach (AudioMixerGroup audioMixerGroup in AudioConfigObject.instance.audioMixer.FindMatchingGroups("Master"))
        {
            float volume = GetVolume(audioMixerGroup.name);
            AudioConfigObject.instance.audioMixer.SetFloat(audioMixerGroup.name + "_Volume", AudioConfigObject.TranslateVolume(volume));
            mixerGroups.Add(audioMixerGroup.name, audioMixerGroup);
        }
    }

    public static void ResetAudioToDefault()
    {
        foreach (KeyValuePair<string, AudioMixerGroup> audioMixerGroups in mixerGroups)
        {
            SetVolume(audioMixerGroups.Key, AudioConfigObject.GetDefaultVolume(audioMixerGroups.Key));
        }
    }

    public static float GetVolume(string groupName)
    {
        return SaveManager.GetOptions().GetVolume(groupName);
    }

    public static void SetVolume(string groupName, float volume)
    {
        AudioConfigObject.instance.audioMixer.SetFloat(groupName + "_Volume", AudioConfigObject.TranslateVolume(volume));
        SaveManager.GetOptions().SetVolume(groupName, volume);
    }

    private static List<AudioSource> GetPlayingSources(string clipName)
    {
        if(playingClips ==  null)
        {
            playingClips = new Dictionary<string, List<AudioSource>>();
        }

        if(!playingClips.ContainsKey(clipName))
        {
            playingClips.Add(clipName, new List<AudioSource>());
        }

        return playingClips[clipName];
    }


    public static bool StopClip(string clipName)
    {
        List<AudioSource> playingSources = new List<AudioSource>(GetPlayingSources(clipName));

        if(playingSources.Count > 0)
        {
            foreach (AudioSource source in playingSources)
            {
                if (instance.audioSourceCoroutine.ContainsKey(source))
                {
                    instance.audioSourceCoroutine[source].StopCoroutine();
                    instance.audioSourceCoroutine.Remove(source);
                }
            }

            return true;
        }

        return false;
    }

    private static IEnumerator WaitForClipToLoad(AudioClipHolder clipHolder, AudioClip clip, float volumeOverride, float pitchOverride)
    {
        clip.LoadAudioData();
        yield return new WaitUntil(() => clip.loadState == AudioDataLoadState.Loaded);
        PlayClip(clipHolder, clip, volumeOverride );
    }

    private static float PlayClip(AudioClipHolder clipHolder, AudioClip clip, float volumeOverride = 1, float pitchOverride = -1)
    {
        try
        {
            List<AudioSource> playingSources = GetPlayingSources(clipHolder.clipName);

            switch (clipHolder.playMode)
            {
                case AudioClipHolder.PlayMode.Overwrite:

                    if(playingSources.Count > 0)
                    {
                        instance.ReturnSource(clipHolder.clipName, playingSources[0]);
                    }

                    break;
                case AudioClipHolder.PlayMode.Wait:
                    if (playingSources.Count > 0)
                    {
                        return 0;
                    }
                    break;
            }

            AudioSource source = instance.GetSource();

            GetPlayingSources(clipHolder.clipName).Add(source);

            source.clip = clip;
            source.pitch = pitchOverride >= 0 ? pitchOverride : clipHolder.GetPitch();
            source.outputAudioMixerGroup = mixerGroups[clipHolder.mixerGroup];

            source.Play();

            Coroutine coroutine = instance.StartCoroutine(SourceCoroutine(clipHolder, source, source.clip, Mathf.Log10(1 + (clipHolder.volumeScale * volumeOverride))));

            return source.clip.length;
        }
        catch (Exception e)
        {
            Debug.Log("error with clip " + clipHolder.clipName + "\n" + e);

            return 0;
        }
    }

    public static float PlayClip(string clipName, float volumeOverride = 1, float pitchOverride = -1)
    {
        try
        {
            AudioClipHolder audio = AudioConfigObject.GetClip(clipName);

            AudioClip clip = audio.GetClip();

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                instance.StartCoroutine(WaitForClipToLoad(audio, clip, volumeOverride, pitchOverride));
                return clip.length + 0.25f;
            }

            return PlayClip(audio, clip, volumeOverride, pitchOverride);          
        }
        catch (Exception e)
        {
            Debug.Log("error with clip " + clipName + "\n" + e);

            return 0;
        }
    }

    private static IEnumerator SourceCoroutine(AudioClipHolder clipHolder, AudioSource audioSource, AudioClip clip, float volume)
    {
        AudioCoroutine audioCoroutine = new AudioCoroutine(clipHolder, clip);
        instance.audioSourceCoroutine.Add(audioSource, audioCoroutine);

        float fadeIn = clipHolder.GetFadeInTime(clip);
        float fadeOut = clipHolder.GetFadeOutTime(clip);

        if (fadeIn > 0)
        {
            float t = 0;

            while(t < 1)
            {
                t += Time.deltaTime / fadeIn;
                audioSource.volume = Mathf.Lerp(0, volume, t);

                yield return new WaitForEndOfFrame();
            }
        }

        audioSource.volume = volume;

        while(audioCoroutine.timeLeft > 0)
        {
            audioCoroutine.timeLeft -= Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }

        if(audioCoroutine.loop)
        {
            PlayClip(clipHolder.clipName);
        }

        if (fadeOut > 0)
        {
            float t = 0;

            while (t < 1)
            {
                t += Time.deltaTime / fadeOut;
                audioSource.volume = Mathf.Lerp(volume, 0, t);

                yield return new WaitForEndOfFrame();
            }
        }

        instance.ReturnSource(clipHolder.clipName, audioSource);
    }
}