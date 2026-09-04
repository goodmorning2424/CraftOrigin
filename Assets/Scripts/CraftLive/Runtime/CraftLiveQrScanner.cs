using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveQrScanner : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField, Min(0.1f)] private float callbackCooldownSeconds =
            0.75f;
        [SerializeField] private UnityEvent<string> onScanError;
        [SerializeField] private UnityEvent onScanCancelled;

        private float nextScanTime;
        private bool callbackHandled;

        public bool IsScanning { get; private set; }
        public event Action<string, bool> ScanCompleted;
        public event Action<string> ScanFailed;
        public event Action ScanCancelled;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CraftLiveStartQrScanner(
            string gameObjectName);

        [DllImport("__Internal")]
        private static extern void CraftLiveStopQrScanner();
#endif

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }
        }

        private void OnDisable()
        {
            if (IsScanning)
            {
                StopScan();
            }
        }

        public void StartScan()
        {
            if (IsScanning || Time.unscaledTime < nextScanTime)
            {
                return;
            }

            IsScanning = true;
            callbackHandled = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                CraftLiveStartQrScanner(
                    gameObject.name);
            }
            catch (Exception exception)
            {
                RaiseError($"QR読み取りを開始できません: {exception.Message}");
            }
#else
            RaiseError(
                "QR読み取りはWebGLビルドをHTTPSで開いたときに使用できます。");
#endif
        }

        public void StopScan()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CraftLiveStopQrScanner();
#endif
            IsScanning = false;
        }

        public void OnQrScanResult(string rawValue)
        {
            if (!AcceptCallback())
            {
                return;
            }

            string materialId = ParseMaterialId(rawValue);
            IsScanning = false;
            if (string.IsNullOrWhiteSpace(materialId) ||
                session == null)
            {
                RaiseError("素材IDを読み取れませんでした。");
                return;
            }

            bool wasRegistered =
                session.State != null &&
                session.State.HasMaterialRegistered(materialId);
            session.UnlockMaterialId(materialId);
            bool isRegistered =
                session.State != null &&
                session.State.HasMaterialRegistered(materialId);
            if (!isRegistered)
            {
                RaiseError(
                    "このQRコードに対応する素材は登録されていません。");
                return;
            }

            ScanCompleted?.Invoke(materialId, !wasRegistered);
        }

        public void OnQrScanError(string message)
        {
            if (!AcceptCallback())
            {
                return;
            }

            RaiseError(message);
        }

        public void OnQrScanCancelled(string unused)
        {
            if (!AcceptCallback())
            {
                return;
            }

            IsScanning = false;
            onScanCancelled?.Invoke();
            ScanCancelled?.Invoke();
        }

        public void Configure(CraftLiveSession targetSession)
        {
            session = targetSession;
        }

        private bool AcceptCallback()
        {
            if (!IsScanning || callbackHandled)
            {
                return false;
            }

            callbackHandled = true;
            nextScanTime =
                Time.unscaledTime + callbackCooldownSeconds;
            return true;
        }

        private void RaiseError(string message)
        {
            IsScanning = false;
            message = string.IsNullOrWhiteSpace(message)
                ? "QR読み取りに失敗しました。"
                : message.Trim();
            onScanError?.Invoke(message);
            ScanFailed?.Invoke(message);
        }

        public static string ParseMaterialId(string rawValue)
        {
            string raw = (rawValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            const string prefix = "craftlive:material:";
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return raw.Substring(prefix.Length).Trim();
            }

            int materialKey = raw.IndexOf(
                "\"materialId\"",
                StringComparison.OrdinalIgnoreCase);
            if (materialKey >= 0)
            {
                int colon = raw.IndexOf(':', materialKey);
                int firstQuote = colon >= 0
                    ? raw.IndexOf('"', colon + 1)
                    : -1;
                int secondQuote = firstQuote >= 0
                    ? raw.IndexOf('"', firstQuote + 1)
                    : -1;
                if (firstQuote >= 0 && secondQuote > firstQuote)
                {
                    return raw.Substring(
                            firstQuote + 1,
                            secondQuote - firstQuote - 1)
                        .Trim();
                }
            }

            if (Uri.TryCreate(raw, UriKind.Absolute, out Uri uri))
            {
                string query = uri.Query.TrimStart('?');
                foreach (string part in query.Split('&'))
                {
                    string[] pair = part.Split(new[] { '=' }, 2);
                    if (pair.Length == 2 &&
                        (string.Equals(
                             pair[0],
                             "material",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             pair[0],
                             "materialId",
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        return Uri.UnescapeDataString(pair[1]).Trim();
                    }
                }
            }

            return raw;
        }
    }
}
