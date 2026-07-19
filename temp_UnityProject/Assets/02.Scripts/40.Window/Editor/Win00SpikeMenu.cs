#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace Icebreaker.Window.Editor
{
    public static class Win00SpikeMenu
    {
        private const string ForceFallbackEnvironmentVariable = "ICEBREAKER_FORCE_WINDOW_FALLBACK";

        [MenuItem("Icebreaker/WIN-00 창 스파이크/강제 폴백 켜기")]
        private static void EnableForceFallback()
        {
            Environment.SetEnvironmentVariable(ForceFallbackEnvironmentVariable, "1");
            Debug.Log("[WIN-00] 강제 폴백을 켰습니다. Play를 눌러 폴백 경로를 확인하세요.");
        }

        [MenuItem("Icebreaker/WIN-00 창 스파이크/강제 폴백 끄기")]
        private static void DisableForceFallback()
        {
            Environment.SetEnvironmentVariable(ForceFallbackEnvironmentVariable, null);
            Debug.Log("[WIN-00] 강제 폴백을 껐습니다. Play를 눌러 플러그인 경로를 확인하세요.");
        }

        [MenuItem("Icebreaker/WIN-00 창 스파이크/현재 플래그 상태")]
        private static void LogForceFallbackStatus()
        {
            var value = Environment.GetEnvironmentVariable(ForceFallbackEnvironmentVariable);
            Debug.Log($"[WIN-00] {ForceFallbackEnvironmentVariable}={value ?? "<unset>"}");
        }
    }
}
