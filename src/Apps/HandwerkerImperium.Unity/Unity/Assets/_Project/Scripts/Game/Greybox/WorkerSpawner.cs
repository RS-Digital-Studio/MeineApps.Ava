using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Spawnt die sichtbaren Worker-NPCs aus dem MODELL-Zustand — nicht mehr aus einem Hold-to-Pay-Pad
    /// (das es seit der HUD-Verwaltung nicht mehr gibt). Pollt je Station <c>HasWorker</c> und stellt
    /// genau einen <see cref="WorkerNpc"/> je angestellter, freigeschalteter Station auf. Dadurch
    /// erscheinen Worker auch nach einem <b>Save-Load</b> (vorher spawnte nur das Pad sie → Worker
    /// waren nach Neustart unsichtbar). Idempotent: bereits gespawnte Stationen werden übersprungen.
    /// </summary>
    public sealed class WorkerSpawner : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController runtime;
        [SerializeField] private GameObject workerNpcPrefab;
        [Tooltip("Station-Laufziel je Stations-Index (Worker pendelt Station<->Tresen).")]
        [SerializeField] private Transform[] stationPoints;
        [SerializeField] private Transform counterPoint;
        [SerializeField] private float checkInterval = 1f;

        private bool[] _spawned;
        private float _timer;

        private void Start()
        {
            _spawned = new bool[stationPoints != null ? stationPoints.Length : 0];
            SpawnDue(); // sofort beim Start (deckt geladene Saves ab)
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            SpawnDue();
        }

        private void SpawnDue()
        {
            if (runtime == null || workerNpcPrefab == null || stationPoints == null) return;
            int n = Mathf.Min(stationPoints.Length, runtime.StationCount);
            for (int i = 0; i < n; i++)
            {
                if (_spawned[i] || stationPoints[i] == null) continue;
                if (!runtime.GetWorkerRow(i).HasWorker) continue;

                var pos = stationPoints[i].position;
                var go = Instantiate(workerNpcPrefab, pos, Quaternion.identity);
                var npc = go.GetComponent<WorkerNpc>();
                if (npc != null) npc.Setup(stationPoints[i], counterPoint);
                _spawned[i] = true;
            }
        }
    }
}
