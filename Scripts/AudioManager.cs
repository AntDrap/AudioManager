using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using static Unity.VisualScripting.Member;

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

        public AudioCoroutine(AudioClipHolder audioClipHolder)
        {
            this.loop = audioClipHolder.loop;
        }

        public void SetTime(float time)
        {
            timeLeft = time;
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
    }

    public static void Initialize()
    {
        GameObject audio = new GameObject("AudioSourceHolder");

        instance = audio.AddComponent<AudioManager>();
        instance.AddComponent<AudioListener>();

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
        Debug.LogWarning("Volume loading not set up");

        return 1;
    }

    public static void SetVolume(string groupName, float volume)
    {
        AudioConfigObject.instance.audioMixer.SetFloat(groupName + "_Volume", AudioConfigObject.TranslateVolume(volume));

        Debug.LogWarning("Volume saving not set up");
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

    private static IEnumerator WaitForClipToLoad(AudioClipHolder clipHolder, AudioClip clip, Func<float> volumeFunction)
    {
        clip.LoadAudioData();
        yield return new WaitUntil(() => clip.loadState == AudioDataLoadState.Loaded);
        PlayClip(clipHolder, clip, volumeFunction);
    }

    private static float PlayClip(AudioClipHolder clipHolder, AudioClip clip, Func<float> volumeFunction)
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
            source.clip = clipHolder.GetClip();

            playingSources.Add(source);

            Coroutine coroutine = instance.StartCoroutine(SourceCoroutine(clipHolder, source, source.clip, volumeFunction));

            return source.clip.length;
        }
        catch (Exception e)
        {
            Debug.Log("error with clip " + clipHolder.clipName + "\n" + e);

            return 0;
        }
    }

    public static float PlayClip(string clipName, Func<float> volumeFunction = null)
    {
        try
        {
            AudioClipHolder audio = AudioConfigObject.GetClip(clipName);

            AudioClip clip = audio.GetClip();

            if(volumeFunction == null)
            {
                volumeFunction = () => audio.volumeScale;
            }
            else
            {
                Func<float> prevFunc = volumeFunction;
                volumeFunction = () => audio.volumeScale * prevFunc();
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                instance.StartCoroutine(WaitForClipToLoad(audio, clip, volumeFunction));
                return clip.length + 0.25f;
            }

            return PlayClip(audio, clip, volumeFunction);
        }
        catch (Exception e)
        {
            Debug.Log("error with clip " + clipName + "\n" + e);

            return 0;
        }
    }

    private static IEnumerator SourceCoroutine(AudioClipHolder clipHolder, AudioSource audioSource, AudioClip clip, Func<float> volumeFunction)
    {
        AudioCoroutine audioCoroutine = new AudioCoroutine(clipHolder);
        instance.audioSourceCoroutine.Add(audioSource, audioCoroutine);

        do
        {
            float fadeIn = clipHolder.GetFadeInTime(clip);
            float fadeOut = clipHolder.GetFadeOutTime(clip);

            audioSource.clip = clip;
            audioSource.pitch = clipHolder.GetPitch();
            audioSource.outputAudioMixerGroup = mixerGroups[clipHolder.mixerGroup];

            audioCoroutine.SetTime(clipHolder.GetClipTime(clip));
            audioSource.Play();

            if (fadeIn > 0)
            {
                float t = 0;

                while (t < 1)
                {
                    t += Time.deltaTime / fadeIn;
                    audioSource.volume = Mathf.Lerp(0, volumeFunction(), t);

                    yield return new WaitForEndOfFrame();
                }
            }

            audioSource.volume = volumeFunction();

            while (audioCoroutine.timeLeft > 0)
            {
                audioCoroutine.timeLeft -= Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            if (fadeOut > 0)
            {
                float t = 0;

                while (t < 1)
                {
                    t += Time.deltaTime / fadeOut;
                    audioSource.volume = Mathf.Lerp(volumeFunction(), 0, t);

                    yield return new WaitForEndOfFrame();
                }
            }

            if (audioCoroutine.loop)
            {
                clip = clipHolder.GetClip();
            }
        }
        while (audioCoroutine.loop);

        instance.ReturnSource(clipHolder.clipName, audioSource);
    }
}