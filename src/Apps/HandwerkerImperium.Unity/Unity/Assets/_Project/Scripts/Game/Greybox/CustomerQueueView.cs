using System.Collections.Generic;
using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Physische Kunden-Schlange am Tresen: spiegelt die Domain-Queue
    /// (<c>OrderQueueState.PendingCustomers</c> via <see cref="GreyboxGameController.WaitingCustomers"/>)
    /// als laufende NPCs — Kunden kommen vom Stadttor, stellen sich an (leichter Zickzack,
    /// Blick zum Tresen) und gehen nach der Bedienung sichtbar wieder ab. Maximal
    /// <see cref="maxVisible"/> NPCs gleichzeitig (die Domain-Queue darf größer sein).
    /// </summary>
    public sealed class CustomerQueueView : MonoBehaviour
    {
        [SerializeField] private GreyboxGameController controller;
        [Tooltip("Kunden-Prefabs (Vielfalt) — Spawn rotiert deterministisch durch.")]
        [SerializeField] private GameObject[] customerPrefabs;
        [Tooltip("Spawn/Exit der Kunden (Stadttor).")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Vorderster Queue-Platz (Kundenseite des Tresens); Schlange wächst entlang +forward.")]
        [SerializeField] private Transform queueFront;
        [SerializeField] private int maxVisible = 5;
        [SerializeField] private float queueSpacing = 1.25f;
        [SerializeField] private float checkInterval = 0.25f;

        private readonly List<CustomerAgent> _agents = new List<CustomerAgent>(); // 0 = vorderster
        private float _timer;
        private int _spawnRotation;

        private void Update()
        {
            if (controller == null || customerPrefabs == null || customerPrefabs.Length == 0 ||
                spawnPoint == null || queueFront == null) return;

            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;

            _agents.RemoveAll(a => a == null); // abgegangene (zerstörte) Kunden austragen

            int target = Mathf.Min(controller.WaitingCustomers, maxVisible);

            // Bediente Kunden gehen vorn ab (Verkauf bedient bis zu CarryCapacity auf einmal)
            while (_agents.Count > target)
            {
                var front = _agents[0];
                _agents.RemoveAt(0);
                if (front != null) front.Leave();
                RestackSlots();
            }

            // Neue Kunden kommen hinten an
            while (_agents.Count < target)
            {
                var prefab = customerPrefabs[_spawnRotation % customerPrefabs.Length];
                _spawnRotation++;
                if (prefab == null) continue;
                var go = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
                var agent = go.GetComponent<CustomerAgent>();
                if (agent == null) { Destroy(go); break; }
                int index = _agents.Count;
                agent.Init(SlotPosition(index), spawnPoint.position, queueFront.position - queueFront.forward * 2f);
                _agents.Add(agent);
            }
        }

        /// <summary>Nach dem Abgang des vordersten rücken alle verbliebenen einen Platz auf.</summary>
        private void RestackSlots()
        {
            for (int i = 0; i < _agents.Count; i++)
                if (_agents[i] != null) _agents[i].MoveToSlot(SlotPosition(i));
        }

        /// <summary>Queue-Platz i: entlang +forward des Front-Ankers, leichter Zickzack für Natürlichkeit.</summary>
        private Vector3 SlotPosition(int index)
        {
            float side = (index % 2 == 0 ? 1f : -1f) * 0.28f * Mathf.Min(index, 1);
            return queueFront.position + queueFront.forward * (index * queueSpacing) + queueFront.right * side;
        }
    }
}
