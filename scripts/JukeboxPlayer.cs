using UnityEngine;

/// <summary>Place on the jukebox prefab. Left-click the placed jukebox to open its song menu.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class JukeboxPlayer : MonoBehaviour
{
    [Header("Music Library")]
    public AudioClip[] songs = new AudioClip[0];
    [Min(0)] public int startingSongIndex;
    public bool playOnStart;
    public bool loop = true;

    [Header("Sound")]
    [Range(0f, 1f)] public float volume = 0.75f;
    [Min(1f)] public float maxHearingDistance = 18f;

    [Header("Interaction")]
    [Min(0.5f)] public float interactionDistance = 5f;
    public Camera playerCamera;
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Menu")]
    [Min(260f)] public float menuWidth = 460f;
    [Min(260f)] public float menuHeight = 430f;
    public Texture2D menuBackground;

    public static bool IsAnyOpen => openJukebox != null;
    static JukeboxPlayer openJukebox;

    AudioSource audioSource;
    InventorySystem ownerInventory;
    int currentSong;
    Vector2 scrollPosition;
    bool wasPlayingBeforePause;
    bool savedCursorVisible;
    CursorLockMode savedCursorLock;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1.5f;
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance, maxHearingDistance);
        currentSong = Mathf.Max(0, startingSongIndex);
    }

    void Start()
    {
        if (playOnStart) PlaySong(currentSong);
    }

    void Update()
    {
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance, maxHearingDistance);

        if (openJukebox == this)
        {
            if (Input.GetKeyDown(closeKey)) CloseMenu();
            return;
        }
        if (IsAnyOpen || !Input.GetMouseButtonDown(0)) return;

        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null) return;
        InventorySystem inventory = cam.GetComponentInParent<InventorySystem>();
        if (inventory != null && inventory.IsAnyMenuOpen) return;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward,
            out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore)) return;
        if (hit.collider.GetComponentInParent<JukeboxPlayer>() != this) return;
        OpenMenu(inventory);
    }

    public void OpenMenu(InventorySystem inventory)
    {
        if (IsAnyOpen) return;
        ownerInventory = inventory;
        openJukebox = this;
        scrollPosition = Vector2.zero;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        if (openJukebox != this) return;
        openJukebox = null;
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
        ownerInventory = null;
    }

    public void PlaySong(int index)
    {
        if (songs == null || index < 0 || index >= songs.Length) return;
        AudioClip clip = songs[index];
        if (clip == null) return;
        currentSong = index;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
        wasPlayingBeforePause = false;
    }

    public void TogglePlayPause()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            wasPlayingBeforePause = true;
        }
        else if (wasPlayingBeforePause && audioSource.clip != null)
        {
            audioSource.UnPause();
            wasPlayingBeforePause = false;
        }
        else
        {
            PlaySong(currentSong);
        }
    }

    public void NextSong()
    {
        if (songs == null || songs.Length == 0) return;
        for (int offset = 1; offset <= songs.Length; offset++)
        {
            int index = (currentSong + offset) % songs.Length;
            if (songs[index] != null) { PlaySong(index); return; }
        }
    }

    public void StopSong()
    {
        audioSource.Stop();
        wasPlayingBeforePause = false;
    }

    void OnGUI()
    {
        if (openJukebox != this) return;
        float width = Mathf.Min(Mathf.Max(260f, menuWidth), Mathf.Max(260f, Screen.width - 20f));
        float height = Mathf.Min(Mathf.Max(260f, menuHeight), Mathf.Max(260f, Screen.height - 20f));
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        if (menuBackground != null) GUI.DrawTexture(panel, menuBackground, ScaleMode.StretchToFill);
        else GUI.Box(panel, GUIContent.none);

        GUI.Label(new Rect(panel.x + 15, panel.y + 12, width - 105, 25), "JUKEBOX");
        if (GUI.Button(new Rect(panel.xMax - 88, panel.y + 10, 75, 28), "CLOSE")) { CloseMenu(); return; }

        Rect listRect = new Rect(panel.x + 14, panel.y + 50, width - 28, height - 142);
        int count = songs != null ? songs.Length : 0;
        float contentHeight = Mathf.Max(listRect.height - 4, count * 38f + 6f);
        scrollPosition = GUI.BeginScrollView(listRect, scrollPosition,
            new Rect(0, 0, Mathf.Max(120f, listRect.width - 20f), contentHeight));
        if (count == 0) GUI.Label(new Rect(8, 8, listRect.width - 30, 30), "No songs assigned.");
        for (int i = 0; i < count; i++)
        {
            AudioClip clip = songs[i];
            string title = clip != null ? clip.name : "Empty song slot";
            string prefix = i == currentSong && audioSource.clip == clip && clip != null ? "> " : "";
            GUI.enabled = clip != null;
            if (GUI.Button(new Rect(5, i * 38f + 3f, Mathf.Max(90f, listRect.width - 36f), 32f), prefix + title))
                PlaySong(i);
            GUI.enabled = true;
        }
        GUI.EndScrollView();

        float controlsY = panel.yMax - 82;
        if (GUI.Button(new Rect(panel.x + 14, controlsY, 105, 30), audioSource.isPlaying ? "PAUSE" : "PLAY")) TogglePlayPause();
        if (GUI.Button(new Rect(panel.x + 127, controlsY, 92, 30), "STOP")) StopSong();
        if (GUI.Button(new Rect(panel.x + 227, controlsY, 92, 30), "NEXT")) NextSong();
        GUI.Label(new Rect(panel.x + 14, panel.yMax - 43, 75, 25), "Volume");
        volume = GUI.HorizontalSlider(new Rect(panel.x + 95, panel.yMax - 35, Mathf.Max(70, width - 113), 20), volume, 0f, 1f);
    }

    void OnDisable()
    {
        CloseMenu();
        if (audioSource != null) audioSource.Stop();
    }

    void OnDestroy() { CloseMenu(); }
}
