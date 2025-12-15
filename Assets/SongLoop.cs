using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SongLoop : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<AudioClip> clipList = new();
    private AudioSource _audioSource;
    private int loop;

    void Start()
    {
        loop = 0;
        _audioSource = GetComponent<AudioSource>();
        _audioSource.loop = false;
        _audioSource.ignoreListenerPause = true;

        _audioSource.clip = clipList[loop];
        _audioSource.Play();
        Invoke(nameof(OnSongEnd), _audioSource.clip.length);
    }

    void OnSongEnd()
    {
        loop = (loop + 1) % clipList.Count;
        _audioSource.clip = clipList[loop];
        _audioSource.Play();
        Invoke(nameof(OnSongEnd), _audioSource.clip.length);
    }

    // Update is called once per frame
    void Update()
    {
    }
}
