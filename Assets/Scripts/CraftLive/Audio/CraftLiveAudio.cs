using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public enum CraftLiveSound
    {
        Select,
        MaterialSelect,
        Description,
        Confirm,
        Cancel,
        HammerStrike,
        FireMagic,
        IceMagic,
        StoneImpact,
        MetalImpact,
        CrystalImpact,
        RareReveal,
        TransferWhoosh,
        SpringCompress,
        PaintingImpact,
        WallSlide,
        HeartbeatWarning,
        WeaponReveal
    }

    /// <summary>
    /// Runtime audio access for UI created dynamically by the Craft-live pads.
    /// Clips live in Resources so generated buttons do not require scene refs.
    /// </summary>
    public static class CraftLiveAudio
    {
        private const string AudioRoot = "Audio/CraftLive/";
        private const float DuplicateGuardSeconds = 0.06f;

        private static readonly Dictionary<CraftLiveSound, AudioClip> Clips =
            new Dictionary<CraftLiveSound, AudioClip>();
        private static readonly Dictionary<CraftLiveSound, float> LastPlayedAt =
            new Dictionary<CraftLiveSound, float>();

        private static GameObject audioRoot;
        private static AudioSource uiSource;
        private static AudioSource effectSource;
        private static AudioSource loopSource;
        private static AudioSource ambienceSource;
        private static AudioSource musicSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clips.Clear();
            LastPlayedAt.Clear();
            audioRoot = null;
            uiSource = null;
            effectSource = null;
            loopSource = null;
            ambienceSource = null;
            musicSource = null;
        }

        public static void Play(CraftLiveSound sound, float volume = 1f)
        {
            if (!Application.isPlaying || IsDuplicate(sound) ||
                !EnsureSources())
            {
                return;
            }

            AudioClip clip = Load(sound);
            if (clip == null)
            {
                return;
            }

            AudioSource source = IsUiSound(sound) ? uiSource : effectSource;
            if (IsUiSound(sound))
            {
                source.Stop();
                source.pitch = 1f;
                source.clip = clip;
                source.volume = Mathf.Clamp01(volume);
                source.Play();
            }
            else
            {
                source.pitch = sound == CraftLiveSound.HammerStrike
                    ? Random.Range(0.96f, 1.045f)
                    : 1f;
                source.PlayOneShot(clip, Mathf.Clamp01(volume));
            }

            LastPlayedAt[sound] = Time.unscaledTime;
        }

        public static void PlayMaterialLanding(
            CraftLiveMaterialDefinition material,
            AudioSource assignedSource = null)
        {
            if (material == null)
            {
                return;
            }

            if (assignedSource != null && material.LandingAudioClip != null)
            {
                assignedSource.PlayOneShot(material.LandingAudioClip);
                return;
            }

            string id = material.MaterialId ?? string.Empty;
            if (id.Contains("fire"))
            {
                Play(CraftLiveSound.FireMagic, 0.9f);
            }
            else if (id.Contains("freeze") || id.Contains("ice"))
            {
                Play(CraftLiveSound.IceMagic, 0.9f);
            }
            else if (material.MaterialForm == CraftLiveMaterialForm.Gem ||
                     material.MaterialForm == CraftLiveMaterialForm.Charm ||
                     material.MaterialForm == CraftLiveMaterialForm.Spirit)
            {
                Play(CraftLiveSound.CrystalImpact, 0.62f);
            }
            else if (material.MaterialForm == CraftLiveMaterialForm.Ore)
            {
                Play(CraftLiveSound.StoneImpact, 0.75f);
            }
            else
            {
                Play(CraftLiveSound.MetalImpact, 0.65f);
            }
        }

        public static void PlayForgeComplete()
        {
            Play(CraftLiveSound.MetalImpact, 0.95f);
            Play(CraftLiveSound.WeaponReveal, 0.7f);
        }

        public static void StartBackground(CraftLiveRole role)
        {
            if (!Application.isPlaying || !EnsureSources())
            {
                return;
            }

            if (role != CraftLiveRole.WorkbenchPad)
            {
                StopBackground();
                return;
            }

            StartLoop(musicSource, "ForgeBgm", 0.16f);
            StartLoop(ambienceSource, "ForgeFireLoop", 0.16f);
        }

        public static void StopBackground()
        {
            StopLoop(musicSource);
            StopLoop(ambienceSource);
        }

        public static void StartSynthesisLoop(float volume = 0.42f)
        {
            if (!Application.isPlaying || !EnsureSources())
            {
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>(
                AudioRoot + "SynthesisLoop");
            if (clip == null)
            {
                return;
            }

            if (loopSource.isPlaying && loopSource.clip == clip)
            {
                return;
            }

            loopSource.Stop();
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.volume = Mathf.Clamp01(volume);
            loopSource.Play();
        }

        public static void StopSynthesisLoop()
        {
            if (loopSource == null)
            {
                return;
            }

            loopSource.Stop();
            loopSource.clip = null;
        }

        private static void StartLoop(
            AudioSource source,
            string resourceName,
            float volume)
        {
            AudioClip clip = Resources.Load<AudioClip>(AudioRoot + resourceName);
            if (source == null || clip == null ||
                source.isPlaying && source.clip == clip)
            {
                return;
            }

            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        private static void StopLoop(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
        }

        private static bool EnsureSources()
        {
            if (audioRoot != null && uiSource != null &&
                effectSource != null && loopSource != null &&
                ambienceSource != null && musicSource != null)
            {
                return true;
            }

            audioRoot = new GameObject("CraftLiveAudio");
            Object.DontDestroyOnLoad(audioRoot);
            uiSource = CreateSource("UI");
            effectSource = CreateSource("Effects");
            loopSource = CreateSource("SynthesisLoop");
            ambienceSource = CreateSource("ForgeAmbience");
            musicSource = CreateSource("Music");
            return true;
        }

        private static AudioSource CreateSource(string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(audioRoot.transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static AudioClip Load(CraftLiveSound sound)
        {
            if (Clips.TryGetValue(sound, out AudioClip cached))
            {
                return cached;
            }

            AudioClip loaded = Resources.Load<AudioClip>(
                AudioRoot + FileName(sound));
            Clips[sound] = loaded;
            return loaded;
        }

        private static string FileName(CraftLiveSound sound)
        {
            switch (sound)
            {
                case CraftLiveSound.MaterialSelect:
                    return "MaterialSelect";
                case CraftLiveSound.Description:
                    return "Description";
                case CraftLiveSound.Confirm:
                    return "Confirm";
                case CraftLiveSound.Cancel:
                    return "Cancel";
                case CraftLiveSound.HammerStrike:
                    return "HammerStrike";
                case CraftLiveSound.FireMagic:
                    return "FireMagic";
                case CraftLiveSound.IceMagic:
                    return "IceMagic";
                case CraftLiveSound.StoneImpact:
                    return "StoneImpact";
                case CraftLiveSound.MetalImpact:
                    return "MetalImpact";
                case CraftLiveSound.CrystalImpact:
                case CraftLiveSound.RareReveal:
                    return "RareReveal";
                case CraftLiveSound.TransferWhoosh:
                    return "TransferWhooshHeavy";
                case CraftLiveSound.SpringCompress:
                    return "SpringCompress";
                case CraftLiveSound.PaintingImpact:
                    return "PaintingImpact";
                case CraftLiveSound.WallSlide:
                    return "WallSlide";
                case CraftLiveSound.HeartbeatWarning:
                    return "HeartbeatWarning";
                case CraftLiveSound.WeaponReveal:
                    return "WeaponReveal";
                default:
                    return "Select";
            }
        }

        private static bool IsUiSound(CraftLiveSound sound)
        {
            return sound == CraftLiveSound.Select ||
                   sound == CraftLiveSound.MaterialSelect ||
                   sound == CraftLiveSound.Confirm ||
                   sound == CraftLiveSound.Cancel;
        }

        private static bool IsDuplicate(CraftLiveSound sound)
        {
            return LastPlayedAt.TryGetValue(sound, out float lastPlayed) &&
                   Time.unscaledTime - lastPlayed < DuplicateGuardSeconds;
        }
    }
}
