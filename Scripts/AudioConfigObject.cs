using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Audio;
using System.Reflection;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

/// <summary>
/// Holds all of the audio clips used by the project
/// </summary>
[CreateAssetMenu(fileName = nameof(AudioConfigObject), menuName = nameof(AudioConfigObject))]
public class AudioConfigObject : ScriptableObject
{
    private static AudioConfigObject _instance;
    public static AudioConfigObject instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = Resources.Load<AudioConfigObject>(nameof(AudioConfigObject));
                AudioManager.Initialize();
            }

            return _instance;
        }
    }

    public AudioMixer audioMixer;
    public float defaultVolume;

    public GameObject audioSourcePrefab;

    public enum MixerGroup { Effects, Music }

    [SerializeField]
    private AudioClipHolder[] audioClips;

    private static Dictionary<string, AudioClipHolder> audioDictionary;

    public static string GetGroupName(MixerGroup group)
    {
        switch(group)
        {
            case MixerGroup.Effects:
                return "Effects";
            case MixerGroup.Music:
                return "Music";
            default:
                return "Master";
        }
    }

    /// <summary>
    /// Get a clip based on name
    /// </summary>
    /// <param name="clipName"></param>
    /// <returns>Random Audio Clip</returns>
    public static AudioClipHolder GetClip(string clipName)
    {
        if (audioDictionary == null || !Application.isPlaying)
        {
            audioDictionary = new Dictionary<string, AudioClipHolder>();
            foreach (AudioClipHolder audioClipHolder in instance.audioClips)
            {
                if (!audioDictionary.ContainsKey(audioClipHolder.clipName))
                {
                    audioDictionary.Add(audioClipHolder.clipName, audioClipHolder);
                }
            }
        }

        if(!audioDictionary.ContainsKey(clipName))
        {
            return audioDictionary["Debug"];
        }

        return audioDictionary[clipName];
    }

    [ContextMenu("duplicate check")]
    private void DuplicateCheck()
    {
        Dictionary<string, AudioClipHolder> audioDictionary = new Dictionary<string, AudioClipHolder>();

        foreach (AudioClipHolder audioClipHolder in audioClips)
        {
            if (audioDictionary.ContainsKey(audioClipHolder.clipName))
            {
                Debug.Log("duplicate key: " + audioClipHolder.clipName);
                continue;
            }

            audioDictionary.Add(audioClipHolder.clipName, audioClipHolder);
        }
    }

    public AudioClipHolder[] GetAudioClips()
    {
        return audioClips;
    }

    public GameObject CreateAudioSource()
    {
        GameObject source = null;

        if (audioSourcePrefab)
        {
            source = GameObject.Instantiate(audioSourcePrefab);
        }
        else
        {
            source = new GameObject();
            source.AddComponent<AudioSource>();
        }

        return source;
    }
}

/// <summary>
/// Holds all of the audio clips associated with a certain effect
/// </summary>
[Serializable]
public class AudioClipHolder
{
    public enum PlayMode { Basic, Overwrite, Wait }

    public string clipName;
    [Range(0, 5)]
    public float volumeScale = 1;
    [Range(0, 2)]
    public float minPitch = 1;
    [Range(0, 2)]
    public float maxPitch = 1;

    [Range(0, 1)]
    public float fadeInPercent;
    [Range(0, 1)]
    public float fadeOutPercent;

    public bool loop;

    public PlayMode playMode = PlayMode.Basic;
    public AudioConfigObject.MixerGroup mixerGroup;

    [SerializeField]
    private AudioClip[] audioClips;

    /// <summary>
    /// Get a random clip from audioClips
    /// </summary>
    /// <returns>Random Audio Clip</returns>
    public AudioClip GetClip()
    {
        return audioClips.Length > 1 ? audioClips[UnityEngine.Random.Range(0, audioClips.Length)] : audioClips[0];
    }

    public AudioClip[] GetClips()
    {
        return audioClips;
    }

    public float GetPitch()
    {
        if(minPitch == maxPitch)
        {
            return minPitch;
        }

        return UnityEngine.Random.Range(minPitch, maxPitch);
    }
}

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(AudioClipHolder))]
public class AudioClipHolderDrawer : PropertyDrawer
{
    private float prevFadeIn;

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement container = new VisualElement();

        Foldout fold = new Foldout();

        fold.value = false;

        container.Add(fold);
        var nameField = new PropertyField(property.FindPropertyRelative("clipName"));

        nameField.label = "";

        nameField.style.left = 12;
        nameField.style.right = 320;
        nameField.style.position = Position.Absolute;

        container.Add(nameField);

        var volField = new PropertyField(property.FindPropertyRelative("volumeScale"));

        volField.label = "";

        volField.style.right = 110;
        volField.style.width = 200;
        volField.style.position = Position.Absolute;

        container.Add(volField);

        string name = property.FindPropertyRelative("clipName").stringValue;

        // Add fields to the container.
        Button playclip = new Button();
        playclip.clicked += () => 
        {
            if(Application.isPlaying)
            {
                AudioManager.PlayClip(name);
            }
            else
            {
                SerializedProperty audioArray = property.FindPropertyRelative("audioClips");

                if(audioArray.arraySize >= 1)
                {
                    int index = UnityEngine.Random.Range(0, audioArray.arraySize);
                    PlayClip(audioArray.GetArrayElementAtIndex(index).objectReferenceValue as AudioClip);
                }
            }
        };

        playclip.style.position = Position.Absolute;

        playclip.text = "Play Clip";
        playclip.style.height = 16;

        playclip.style.right = 0;
        playclip.style.width = 100;
        playclip.style.position = Position.Absolute;

        container.Add(playclip);

        fold.Add(new PropertyField(property.FindPropertyRelative("open")));
        fold.Add(new PropertyField(property.FindPropertyRelative("minPitch")));
        fold.Add(new PropertyField(property.FindPropertyRelative("maxPitch")));
        fold.Add(new PropertyField(property.FindPropertyRelative("playMode")));
        fold.Add(new PropertyField(property.FindPropertyRelative("mixerGroup")));

        fold.Add(new PropertyField(property.FindPropertyRelative("fadeInPercent")));
        fold.Add(new PropertyField(property.FindPropertyRelative("fadeOutPercent")));

        fold.Add(new PropertyField(property.FindPropertyRelative("audioClips")));

        if(property.FindPropertyRelative("fadeInPercent").floatValue + property.FindPropertyRelative("fadeOutPercent").floatValue > 1)
        {
            if (property.FindPropertyRelative("fadeInPercent").floatValue > prevFadeIn)
            {
                property.FindPropertyRelative("fadeOutPercent").floatValue = 1 - property.FindPropertyRelative("fadeInPercent").floatValue;
            }
            else
            {
                property.FindPropertyRelative("fadeInPercent").floatValue = 1 - property.FindPropertyRelative("fadeOutPercent").floatValue;
            }
        }

        prevFadeIn = property.FindPropertyRelative("fadeInPercent").floatValue;

        return container;
    }

    private static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
    {
        Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;

        Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
        MethodInfo method = audioUtilClass.GetMethod(
            "PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null
        );
        method.Invoke(
            null,
            new object[] { clip, startSample, loop }
        );
    }
}


#endif