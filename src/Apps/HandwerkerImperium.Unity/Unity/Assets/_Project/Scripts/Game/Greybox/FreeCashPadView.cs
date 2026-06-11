using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Free-Cash-Pad (GDD §9.1): Betritt der Avatar das bereite Pad, wird
    /// <c>RuntimeGameController.ClaimFreeCash()</c> gutgeschrieben (2× Einkommen je Zeitblock)
    /// und Münzen spawnen als Feedback; danach kühlt das Pad für
    /// <c>Monetization.FreeCashBlockSeconds</c> ab (Countdown am Schild). Ad-Gate folgt mit dem
    /// Ads-SDK (P3) — bis dahin bewusster Direkt-Claim wie beim Offline-Verdoppeln-Stub.
    /// </summary>
    public sealed class FreeCashPadView : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController runtime;
        [SerializeField] private GreyboxGameController controller;
        [SerializeField] private GameObject cashPrefab;
        [SerializeField] private TextMesh labelText;
        [SerializeField] private GameObject readyVisual;
        [SerializeField] private int maxCashSpawn = 10;
        [SerializeField] private float cashSpread = 1.2f;

        private float _readyAtTime;

        private void Update()
        {
            bool ready = Time.time >= _readyAtTime;
            if (readyVisual != null && readyVisual.activeSelf != ready) readyVisual.SetActive(ready);
            if (labelText == null) return;
            if (ready)
            {
                labelText.text = "Gratis-Geld!";
                labelText.color = new Color(1f, 0.85f, 0.25f);
            }
            else
            {
                int remaining = Mathf.CeilToInt(_readyAtTime - Time.time);
                labelText.text = $"{remaining / 60}:{remaining % 60:00}";
                labelText.color = new Color(0.85f, 0.82f, 0.75f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (runtime == null || Time.time < _readyAtTime) return;
            if (other.GetComponent<AvatarController>() == null) return;

            decimal reward = runtime.ClaimFreeCash();
            if (reward <= 0m) return;
            _readyAtTime = Time.time + (float)runtime.Balancing.Monetization.FreeCashBlockSeconds;
            controller?.Audio?.Play(GameSfx.OfflineEarnings);
            SpawnCash();
        }

        private void SpawnCash()
        {
            if (cashPrefab == null) return;
            for (int i = 0; i < maxCashSpawn; i++)
            {
                Vector3 pos = transform.position +
                    new Vector3(Random.Range(-cashSpread, cashSpread), 0.25f, Random.Range(-cashSpread, cashSpread));
                Instantiate(cashPrefab, pos, Quaternion.identity);
            }
        }
    }
}
