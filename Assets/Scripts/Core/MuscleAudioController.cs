using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>
    /// Unified audio manager for MuscleAR.
    /// Manages:
    /// 1. Voice pronunciations/narrations for the 6 target muscles (independent 2D AudioSource).
    /// 2. Global UI button click sounds (debounced, non-overlapping).
    /// 3. Quiz answer evaluation feedback sounds (correct/wrong).
    /// Ensures AudioListener presence and Android compatibility.
    /// </summary>
    public class MuscleAudioController : MonoBehaviour
    {
        public static MuscleAudioController Instance { get; private set; }

        AudioSource narrationSource;
        AudioSource sfxSource;

        readonly Dictionary<MuscleId, AudioClip> narrationCache = new Dictionary<MuscleId, AudioClip>();
        AudioClip clickClip;
        AudioClip correctClip;
        AudioClip wrongClip;

        MuscleId? currentMuscle;
        float lastClickTime = -1f;
        Coroutine feedbackRoutine;

        static readonly HashSet<Button> BoundButtons = new HashSet<Button>();

        public event Action<MuscleId?, bool> OnPlaybackStateChanged;

        static readonly Dictionary<MuscleId, string[]> AudioResourcePaths = new Dictionary<MuscleId, string[]>
        {
            { MuscleId.PectoralisMajor, new[] { "Audio/PectoralisMajor", "Audio/Pectoralis Major" } },
            { MuscleId.Deltoid, new[] { "Audio/Deltoid" } },
            { MuscleId.RectusAbdominis, new[] { "Audio/RectusAbdominis", "Audio/Rectus Abdominis" } },
            { MuscleId.BicepsBrachii, new[] { "Audio/BicepsBrachii", "Audio/Biceps Brachii" } },
            { MuscleId.Gastrocnemius, new[] { "Audio/Gastrocnemius" } },
            { MuscleId.QuadricepsFemoris, new[] { "Audio/QuadricepsFemoris", "Audio/Quadriceps Femoris" } }
        };

        public static MuscleAudioController Ensure(GameObject host)
        {
            var comp = host.GetComponent<MuscleAudioController>();
            if (comp == null) comp = host.AddComponent<MuscleAudioController>();
            if (comp.narrationSource == null) comp.Awake();
            return comp;
        }

        internal void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // Ensure AudioListener is active in the scene so audio routes to Android device speaker
            var existingListener = FindAnyObjectByType<AudioListener>();
            if (existingListener == null || !existingListener.gameObject.activeInHierarchy || !existingListener.enabled)
            {
                var myListener = GetComponent<AudioListener>();
                if (myListener == null) myListener = gameObject.AddComponent<AudioListener>();
                myListener.enabled = true;
                Debug.Log("[MuscleAudioController] Created active AudioListener on " + gameObject.name);
            }
            AudioListener.volume = 1f;
            AudioListener.pause = false;

            // Setup independent narration audio source
            narrationSource = gameObject.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.loop = false;
            narrationSource.spatialBlend = 0f; // 2D full clarity
            narrationSource.volume = 1f;

            // Setup independent SFX audio source for UI & quiz sounds
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 1f;

            LoadSfxClips();
        }

        void Start()
        {
            ScanAndBindButtons();
        }

        void LoadSfxClips()
        {
            clickClip = Resources.Load<AudioClip>("Audio/click");
            if (clickClip == null) clickClip = Resources.Load<AudioClip>("click");
            if (clickClip == null) Debug.LogWarning("[MuscleAudioController] Missing click.mp3 in Resources/Audio/");

            correctClip = Resources.Load<AudioClip>("Audio/correct");
            if (correctClip == null) correctClip = Resources.Load<AudioClip>("correct");
            if (correctClip == null) Debug.LogWarning("[MuscleAudioController] Missing correct.mp3 in Resources/Audio/");

            wrongClip = Resources.Load<AudioClip>("Audio/wrong");
            if (wrongClip == null) wrongClip = Resources.Load<AudioClip>("wrong");
            if (wrongClip == null) Debug.LogWarning("[MuscleAudioController] Missing wrong.wav in Resources/Audio/");
        }

        // =================================================================
        // GLOBAL BUTTON CLICK SOUND
        // =================================================================

        /// <summary>
        /// Plays click.mp3 with a 0.06s debounce to prevent double-firing.
        /// </summary>
        public void PlayClick()
        {
            if (Time.unscaledTime - lastClickTime < 0.06f) return;
            lastClickTime = Time.unscaledTime;

            if (clickClip == null) LoadSfxClips();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
                sfxSource.volume = 1f;
            }

            if (clickClip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clickClip, 1f);
                Debug.Log("[MuscleAudioController] PlayClick");
            }
        }

        /// <summary>
        /// Binds the click sound to a button without replacing its existing onClick listeners.
        /// Ensures only one click listener is attached per button.
        /// </summary>
        public static void AttachClickSound(Button button)
        {
            if (button == null) return;
            if (BoundButtons.Contains(button)) return;
            BoundButtons.Add(button);

            button.onClick.AddListener(() =>
            {
                if (Instance != null) Instance.PlayClick();
            });
        }

        /// <summary>
        /// Automatically finds and binds all buttons in the active scenes and hierarchies.
        /// </summary>
        public void ScanAndBindButtons()
        {
            var buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                AttachClickSound(buttons[i]);
            }
        }

        // =================================================================
        // QUIZ ANSWER FEEDBACK SOUNDS
        // =================================================================

        /// <summary>
        /// Evaluates and plays the quiz feedback sound (correct.mp3 or wrong.wav).
        /// Schedules after a tiny delay (0.12s) so the button click sound and answer sound
        /// are both distinctly audible without clashing or clipping.
        /// </summary>
        public void PlayAnswerFeedback(bool isCorrect)
        {
            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(AnswerFeedbackRoutine(isCorrect));
        }

        IEnumerator AnswerFeedbackRoutine(bool isCorrect)
        {
            yield return new WaitForSecondsRealtime(0.12f);

            AudioClip clip = isCorrect ? correctClip : wrongClip;
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, 1f);
                Debug.Log("[MuscleAudioController] Played feedback sound: " + (isCorrect ? "Correct" : "Wrong"));
            }
            feedbackRoutine = null;
        }

        // =================================================================
        // MUSCLE NARRATION SOUNDS
        // =================================================================

        public AudioClip GetClip(MuscleId id)
        {
            if (narrationCache.TryGetValue(id, out var cached) && cached != null)
                return cached;

            if (AudioResourcePaths.TryGetValue(id, out var paths))
            {
                foreach (var p in paths)
                {
                    var clip = Resources.Load<AudioClip>(p);
                    if (clip != null)
                    {
                        narrationCache[id] = clip;
                        return clip;
                    }
                }
            }

            // Fallback generic searches
            string clean = id.ToString().Replace(" ", "").Replace("_", "");
            var genericClip = Resources.Load<AudioClip>("Audio/" + clean);
            if (genericClip != null)
            {
                narrationCache[id] = genericClip;
                return genericClip;
            }

            Debug.LogWarning("[MuscleAudioController] Could not find or load narration audio clip for: " + id);
            return null;
        }

        public bool IsPlaying(MuscleId id)
        {
            return narrationSource != null && narrationSource.isPlaying && currentMuscle == id;
        }

        public bool IsAnyPlaying => narrationSource != null && narrationSource.isPlaying;
        public MuscleId? CurrentMuscle => currentMuscle;

        public void Play(MuscleId id)
        {
            Stop();

            var clip = GetClip(id);
            if (clip == null)
            {
                Debug.LogWarning("[MuscleAudioController] Cannot play: audio clip is missing for " + id);
                return;
            }

            currentMuscle = id;
            narrationSource.clip = clip;
            narrationSource.Play();
            Debug.Log("[MuscleAudioController] Playing narration for: " + id + " (" + clip.length.ToString("F1") + "s)");
            OnPlaybackStateChanged?.Invoke(currentMuscle, true);
        }

        public void Stop()
        {
            if (narrationSource != null && (narrationSource.isPlaying || currentMuscle != null))
            {
                var prev = currentMuscle;
                narrationSource.Stop();
                currentMuscle = null;
                Debug.Log("[MuscleAudioController] Narration audio stopped.");
                OnPlaybackStateChanged?.Invoke(prev, false);
            }
        }

        public void Toggle(MuscleId id)
        {
            if (IsPlaying(id))
            {
                Stop();
            }
            else
            {
                Play(id);
            }
        }

        float scanTimer;

        void Update()
        {
            // Detect when voice narration finishes naturally
            if (currentMuscle != null && narrationSource != null && !narrationSource.isPlaying)
            {
                var prev = currentMuscle;
                currentMuscle = null;
                OnPlaybackStateChanged?.Invoke(prev, false);
            }

            // Periodically discover dynamically created buttons (e.g. when changing screens)
            scanTimer += Time.unscaledDeltaTime;
            if (scanTimer >= 0.5f)
            {
                scanTimer = 0f;
                ScanAndBindButtons();
            }
        }

        void OnDisable()
        {
            Stop();
        }

        void OnDestroy()
        {
            Stop();
            if (Instance == this) Instance = null;
        }
    }
}
