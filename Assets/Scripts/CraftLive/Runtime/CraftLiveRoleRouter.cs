using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    [DefaultExecutionOrder(-200)]
    public sealed class CraftLiveRoleRouter : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveRole editorRole = CraftLiveRole.WorkbenchPad;
        [SerializeField] private string editorRoomId = "001";
        [SerializeField] private GameObject materialPadRoot;
        [SerializeField] private GameObject workbenchPadRoot;
        [SerializeField] private GameObject qrPadRoot;
        [SerializeField] private GameObject hologramPadRoot;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string CraftLiveGetQueryParameter(string key);
#endif

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<CraftLiveSession>();
            }

            string room = ReadQuery("room", editorRoomId);
            CraftLiveRole resolvedRole = ResolveRole(ReadQuery("screen", string.Empty));
            if (resolvedRole == CraftLiveRole.Auto)
            {
                resolvedRole = editorRole;
            }

            session?.Configure(room, resolvedRole);
            SetRoleRoots(resolvedRole);
        }

        private void SetRoleRoots(CraftLiveRole selectedRole)
        {
            SetActive(materialPadRoot, selectedRole == CraftLiveRole.MaterialPad);
            SetActive(workbenchPadRoot, selectedRole == CraftLiveRole.WorkbenchPad);
            SetActive(qrPadRoot, selectedRole == CraftLiveRole.QrPad);
            SetActive(hologramPadRoot, selectedRole == CraftLiveRole.HologramPad);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static CraftLiveRole ResolveRole(string screen)
        {
            switch ((screen ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "items":
                case "materials":
                case "pad1":
                    return CraftLiveRole.MaterialPad;
                case "craft":
                case "workbench":
                case "pad2":
                    return CraftLiveRole.WorkbenchPad;
                case "status":
                case "qr":
                    return CraftLiveRole.QrPad;
                case "hologram":
                    return CraftLiveRole.HologramPad;
                default:
                    return CraftLiveRole.Auto;
            }
        }

        private static string ReadQuery(string key, string fallback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string value = CraftLiveGetQueryParameter(key);
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch (Exception)
            {
                return fallback;
            }
#else
            return fallback;
#endif
        }
    }
}
