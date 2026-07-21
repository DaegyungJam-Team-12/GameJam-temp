#nullable enable

using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Icebreaker.Gameplay;

namespace Icebreaker.Integration.Tests
{
    /// <summary>
    /// PlayMode integration check for INT-01. Loading minjun.unity must assemble the vertical slice
    /// and IntegrationBootstrap must rebind the launcher HUD from the prefab's preview source to the
    /// real ProgressionCore-backed source at runtime, so the HUD shows the real initial funds (0),
    /// not the prefab preview value. Simulating a real mouse click on an ice cannot be done reliably
    /// headlessly (batch mode does not process focus-gated input, and Mouse.current resolves to the
    /// system mouse in a windowed editor), so the click-driven funds increase stays a manual check;
    /// the destruction-to-funds logic itself is covered by ProgressionStateServiceTests (EditMode).
    /// </summary>
    public sealed class Int01ClickToHudTests
    {
        [UnityTest]
        public IEnumerator MinjunScene_BindsRealProgressionSource_HudShowsInitialZeroFunds()
        {
            yield return SceneManager.LoadSceneAsync("minjun", LoadSceneMode.Single);
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            var view = Object.FindFirstObjectByType<IceFieldView>();
            Assert.IsNotNull(view, "minjun.unity must contain the IceFieldView combat source.");

            var fundsText = System.Array.Find(
                Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None),
                t => t.gameObject.name == "FundsText");
            Assert.IsNotNull(fundsText, "The launcher HUD must be present with a FundsText element.");

            // The real source starts at 0 funds -> "...0"; the prefab preview source is 12,400 -> "...12.4K".
            // Matching the trailing value (ignoring any label the HUD prepends) proves the runtime rebind.
            Assert.IsTrue(
                fundsText!.text.EndsWith("0") && !fundsText.text.Contains("K"),
                "IntegrationBootstrap should rebind the HUD to the real ProgressionCore source "
                + $"(initial funds 0), replacing the prefab preview value. Actual funds text: '{fundsText.text}'.");
        }
    }
}
