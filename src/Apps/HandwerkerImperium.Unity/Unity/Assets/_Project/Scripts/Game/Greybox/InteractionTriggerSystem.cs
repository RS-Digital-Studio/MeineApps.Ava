using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Annaeherungs-Trigger. Hier die Avatar-Seite — Cash-Auto-Pickup im effektiven
    /// Sammelradius (Stationen/Tresen/Pads nutzen eigene Trigger-Collider). Cash ist reine Optik
    /// (Geld wurde beim Verkauf gutgeschrieben), das Einsammeln despawnt nur.
    /// </summary>
    [RequireComponent(typeof(AvatarController))]
    public sealed class InteractionTriggerSystem : MonoBehaviour
    {
        [SerializeField] private GreyboxGameController controller;
        [SerializeField] private float scanInterval = 0.1f;

        private float _timer;
        private readonly Collider[] _hits = new Collider[32];

        private void Update()
        {
            if (controller == null || controller.Economy == null) return;
            _timer += Time.deltaTime;
            if (_timer < scanInterval) return;
            _timer = 0f;

            float r = Mathf.Max(0.1f, (float)controller.Economy.CollectRadius);
            int n = Physics.OverlapSphereNonAlloc(transform.position, r, _hits);
            for (int i = 0; i < n; i++)
            {
                var cash = _hits[i].GetComponent<CashCube>();
                if (cash != null && cash.Collect(transform))
                    controller.Audio?.Play(GameSfx.CoinCollect);
            }
        }
    }
}
