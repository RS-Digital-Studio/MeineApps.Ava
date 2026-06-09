using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Richtet ein Welt-Schild jeden Frame zur Hauptkamera aus (Billboard) — Stations-Namen und
    /// Plot-Preise bleiben aus jeder Blickrichtung lesbar (Nord- wie Süd-Reihe des Hofs).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
        }
    }
}
