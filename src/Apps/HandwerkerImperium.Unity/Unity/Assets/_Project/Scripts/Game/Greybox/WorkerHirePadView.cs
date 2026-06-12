using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3 + GDD §6.2: Hold-to-Pay-Pad in zwei Phasen — erst stellt es den Worker einer
    /// Station an (spawnt den NPC, der Station&lt;-&gt;Tresen automatisiert), danach verkauft es
    /// die Worker-Tempo-Stufen (+50 % Tragegeschwindigkeit je Stufe, geometrische Kosten).
    /// Das Label zeigt die jeweils nächste Aktion; auf Max-Stufe verschwindet das Pad.
    /// </summary>
    public sealed class WorkerHirePadView : HoldToPayPad
    {
        [SerializeField] private int stationIndex;
        [SerializeField] private GameObject workerNpcPrefab;
        [SerializeField] private Transform stationPoint;
        [SerializeField] private Transform counterPoint;
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Pad-Beschriftung (Builder-Label) — zeigt Anstellen/Tempo-Stufe/MAX.")]
        [SerializeField] private TextMesh labelText;

        private void Start() => UpdateLabel();

        protected override bool IsDone() =>
            controller != null && controller.Workers != null &&
            controller.Workers.HasWorker(stationIndex) &&
            controller.Workers.Level(stationIndex) >= controller.Workers.MaxLevel;

        protected override void TryPayStep()
        {
            if (!controller.Workers.HasWorker(stationIndex))
            {
                if (!controller.Workers.Hire(stationIndex)) return;
                controller.Audio?.Play(GameSfx.WorkerHired);

                if (workerNpcPrefab != null)
                {
                    Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
                    var go = Instantiate(workerNpcPrefab, pos, Quaternion.identity);
                    var npc = go.GetComponent<WorkerNpc>();
                    if (npc != null) npc.Setup(stationPoint, counterPoint);
                }
            }
            else
            {
                if (!controller.Workers.Upgrade(stationIndex)) return;
                controller.Audio?.Play(GameSfx.UpgradePaid);
            }

            UpdateLabel();
            if (IsDone()) gameObject.SetActive(false); // Max-Stufe erreicht — Pad hat ausgedient
        }

        private void UpdateLabel()
        {
            if (labelText == null || controller == null || controller.Workers == null) return;
            if (!controller.Workers.HasWorker(stationIndex))
            {
                labelText.text = "Arbeiter";
            }
            else
            {
                int level = controller.Workers.Level(stationIndex);
                labelText.text = level >= controller.Workers.MaxLevel
                    ? "Arbeiter MAX"
                    : $"Tempo {level + 1}/{controller.Workers.MaxLevel}";
            }
        }
    }
}
