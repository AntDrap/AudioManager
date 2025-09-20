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
    private static Dictionary<string, AudioClipHolder> audioDictionary;
    private static Dictionary<string, float> defaultVolumeDictionary;

    private static AudioConfigObject _instance;
    public static AudioConfigObject instance
    {
        get
        {
            if(!Application.isPlaying)
            {
                return Resources.Load<AudioConfigObject>(nameof(AudioConfigObject));
            }

            if(_instance == null)
            {
                _instance = Resources.Load<AudioConfigObject>(nameof(AudioConfigObject));
                AudioManager.Initialize();
            }

            return _instance;
        }
    }

    public AudioMixer audioMixer;

    [SerializeField, Range(0, 20)] private float maxDecibals = 5;
    [SerializeField] private GameObject audioSourcePrefab;
    [SerializeField] private List<DefaultVolume> defaultVolumes;
    [SerializeField] private AudioClipHolder[] audioClips;

    [Serializable]
    private struct DefaultVolume
    {
        [HideInInspector]
        public string name;
        [Range(0,1)]
        public float volume;
    }

/*    private void OnValidate()
    {
        List<DefaultVolume> volumes = new List<DefaultVolume>();
        List<string> mixerGroups = GetMixerGroupNames();

        foreach(DefaultVolume defaultVolume in defaultVolumes)
        {
            if(mixerGroups.Contains(defaultVolume.name))
            {
                mixerGroups.Remove(defaultVolume.name);
                volumes.Add(defaultVolume);
            }
        }

        foreach (string group in mixerGroups)
        {
            volumes.Add(new DefaultVolume()
            {
                name = group,
                volume = 0.625f
            });
        }

        defaultVolumes = volumes;
    }*/

    public static float TranslateVolume(float volume)
    {
        float convertedValue = Mathf.Lerp(Mathf.Epsilon, 1 + (6 * (instance.maxDecibals / 20)), volume);
        convertedValue = Mathf.Log10(convertedValue) * 20;

        return convertedValue;
    }

    public static float GetDefaultVolume(string name)
    {
        if(defaultVolumeDictionary == null)
        {
            List<DefaultVolume> volumes = instance.defaultVolumes;
            defaultVolumeDictionary = new Dictionary<string, float>();

            foreach(DefaultVolume defaultVolume in volumes)
            {
                defaultVolumeDictionary.Add(defaultVolume.name, defaultVolume.volume);
            }
        }

        return defaultVolumeDictionary[name];
    }

    public static List<string> GetMixerGroupNames()
    {
        List<string> list = new List<string>();

        if(instance.audioMixer != null)
        {
            foreach (AudioMixerGroup audioMixerGroup in instance.audioMixer.FindMatchingGroups("Master"))
            {
                list.Add(audioMixerGroup.name);
            }
        }

        return list;
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
    public enum FadeMode { None, Percent, Time }

    public string clipName;
    [Range(0, 5)]
    public float volumeScale = 1;
    [Range(0, 2)]
    public float minPitch = 1;
    [Range(0, 2)]
    public float maxPitch = 1;

    public FadeMode fadeMode;
    public float fadeIn;
    public float fadeOut;

    public bool loop;

    public PlayMode playMode = PlayMode.Basic;
    public string mixerGroup;

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

    public float GetClipTime(AudioClip clip)
    {
        return clip.length - (GetFadeInTime(clip) + GetFadeOutTime(clip));
    }

    public float GetFadeInTime(AudioClip clip)
    {
        switch(fadeMode)
        {
            case FadeMode.Percent:
                return Mathf.Clamp(fadeIn, 0, 1) * clip.length;
            case FadeMode.Time:
                if(fadeIn > clip.length)
                {
                    return clip.length;
                }

                return fadeIn;
        }

        return 0;
    }

    public float GetFadeOutTime(AudioClip clip)
    {
        switch (fadeMode)
        {
            case FadeMode.Percent:
                return Mathf.Clamp(fadeOut, 0, (1 - fadeIn)) * clip.length;
            case FadeMode.Time:
                if (fadeIn + fadeOut > clip.length)
                {
                    return Mathf.Clamp(clip.length - fadeIn, 0, clip.length);
                }
                else
                {
                    return fadeIn;
                }
        }

        return 0;
    }
}

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(AudioClipHolder))]
public class AudioClipHolderDrawer : PropertyDrawer
{
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

        List<string> groups = AudioConfigObject.GetMixerGroupNames();

        if(groups.Count > 0)
        {
            SerializedProperty mixerGroup = property.FindPropertyRelative("mixerGroup");

            if (!groups.Contains(mixerGroup.stringValue))
            {
                mixerGroup.stringValue = groups[0];
                property.serializedObject.ApplyModifiedProperties();
            }

            DropdownField dropdown = new DropdownField(groups, groups.IndexOf(mixerGroup.stringValue));
            dropdown.label = "Mixer Group";

            dropdown.RegisterValueChangedCallback(evt =>
            {
                mixerGroup.stringValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });

            fold.Add(dropdown);
        }
        else
        {
            fold.Add(new Label("No Audio Mixer / No Audio Mixer Groups"));
        }



        fold.Add(new PropertyField(property.FindPropertyRelative("open")));
        fold.Add(new PropertyField(property.FindPropertyRelative("minPitch")));
        fold.Add(new PropertyField(property.FindPropertyRelative("maxPitch")));
        fold.Add(new PropertyField(property.FindPropertyRelative("playMode")));


        fold.Add(new PropertyField(property.FindPropertyRelative("loop")));

        fold.Add(new PropertyField(property.FindPropertyRelative("fadeMode")));

        fold.Add(new PropertyField(property.FindPropertyRelative("fadeIn")));
        fold.Add(new PropertyField(property.FindPropertyRelative("fadeOut")));

        fold.Add(new PropertyField(property.FindPropertyRelative("audioClips")));

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