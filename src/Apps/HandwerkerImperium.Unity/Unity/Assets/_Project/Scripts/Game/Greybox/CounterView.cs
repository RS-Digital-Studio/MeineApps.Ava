using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3/§5: Tresen/Abgabepunkt — der Avatar gibt seine getragenen Waren ab (Geld via Economy),
    /// danach spawnen Cash-Wuerfel als Optik, die der Avatar per Sammelradius einsammelt.
    /// Braucht einen Trigger-Collider.
    /// </summary>
    public sealed class CounterView : MonoBehaviour
    {
        [SerializeField] private GreyboxGameController controller;
        [SerializeField] private Transform cashSpawnPoint;
        [SerializeField] private GameObject cashPrefab;
        [SerializeField] private int maxCashPerDeposit = 12;
        [SerializeField] private float cashSpread = 1.5f;

        private void OnTriggerStay(Collider other)
        {
            if (controller == null) return;
            var avatar = other.GetComponent<AvatarController>();
            if (avatar == null || avatar.CarriedCount <= 0) return;

            int sold = avatar.Deposit();
            controller.NotifyPhysicalSale(sold); // gekoppelt: bedient wartende Kunden der Runtime-Queue
            if (sold > 0) controller.Audio?.Play(GameSfx.MoneyEarned);
            SpawnCash(sold);
        }

        private void SpawnCash(int count)
        {
            if (cashPrefab == null || count <= 0) return;
            int spawn = Mathf.Min(count, maxCashPerDeposit);
            Vector3 origin = cashSpawnPoint != null ? cashSpawnPoint.position : transform.position;
            for (int i = 0; i < spawn; i++)
            {
                Vector3 pos = origin + new Vector3(Random.Range(-cashSpread, cashSpread), 0.2f, Random.Range(-cashSpread, cashSpread));
                Instantiate(cashPrefab, pos, Quaternion.identity);
            }
        }
    }
}
