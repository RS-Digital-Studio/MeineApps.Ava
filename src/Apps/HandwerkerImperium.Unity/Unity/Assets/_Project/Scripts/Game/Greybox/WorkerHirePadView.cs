using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Hold-to-Pay-Hire-Pad — stellt den Worker einer Station an (einmalig) und spawnt den
    /// NPC, der ab dann Station&lt;-&gt;Tresen automatisiert. Nach der Anstellung verschwindet das Pad.
    /// </summary>
    public sealed class WorkerHirePadView : HoldToPayPad
    {
        [SerializeField] private int stationIndex;
        [SerializeField] private GameObject workerNpcPrefab;
        [SerializeField] private Transform stationPoint;
        [SerializeField] private Transform counterPoint;
        [SerializeField] private Transform spawnPoint;

        protected override bool IsDone() => controller != null && controller.Workers.HasWorker(stationIndex);

        protected override void TryPayStep()
        {
            if (!controller.Workers.Hire(stationIndex)) return;

            if (workerNpcPrefab != null)
            {
                Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
                var go = Instantiate(workerNpcPrefab, pos, Quaternion.identity);
                var npc = go.GetComponent<WorkerNpc>();
                if (npc != null) npc.Setup(stationPoint, counterPoint);
            }
            gameObject.SetActive(false);
        }
    }
}
