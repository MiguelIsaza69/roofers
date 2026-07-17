using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// One-line transient HUD notice ("mide el hueco primero", "plancha cortada a 1.8 m"…).
    /// Any system can post; <see cref="RoofingToolbelt"/> draws the current one. Keeps the
    /// prototype's feedback in one place until the real HUD exists.
    /// </summary>
    public static class HudNotice
    {
        private static string message;
        private static float until;

        public static void Show(string msg, float duration = 2.6f)
        {
            message = msg;
            until = Time.unscaledTime + duration;
        }

        /// <summary>The active notice, or null once it has expired.</summary>
        public static string Current => Time.unscaledTime < until ? message : null;
    }
}
