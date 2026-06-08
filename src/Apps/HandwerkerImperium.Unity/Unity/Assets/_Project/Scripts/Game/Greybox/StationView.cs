using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3/§5: Stations-View — synchronisiert den sichtbaren Waren-Stapel mit dem Stock (StationService)
    /// und laesst den Avatar bei Annaeherung aufnehmen. Gesperrte Station zeigt ein Locked-Visual.
    /// Braucht einen Trigger-Collider (der Avatar-CharacterController loest OnTriggerStay aus).
    /// </summary>
    public sealed class StationView : MonoBehaviour
    {
        [SerializeField] private int stationIndex;
        [SerializeField] private GreyboxGameController controller;
        [SerializeField] private Transform stackAnchor;
        [SerializeField] private GameObject warePrefab;
        [SerializeField] private GameObject lockedVisual;
        [SerializeField] private float wareHeight = 0.5f;

        private int _visualCount;

        public int StationIndex => stationIndex;

        private void Update()
        {
            if (controller == null || controller.Stations == null) return;
            bool unlocked = controller.Stations.IsUnlocked(stationIndex);
            if (lockedVisual != null && lockedVisual.activeSelf == unlocked) lockedVisual.SetActive(!unlocked);
            SyncStack(unlocked ? controller.Stations.Stock(stationIndex) : 0);
        }

        private void SyncStack(int stock)
        {
            if (stackAnchor == null) return;
            while (_visualCount < stock)
            {
                if (warePrefab != null)
                {
                    var go = Instantiate(warePrefab, stackAnchor);
                    go.transform.localPosition = new Vector3(0f, _visualCount * wareHeight, 0f);
                    go.transform.localRotation = Quaternion.identity;
                }
                _visualCount++;
            }
            while (_visualCount > stock && stackAnchor.childCount > 0)
            {
                Destroy(stackAnchor.GetChild(stackAnchor.childCount - 1).gameObject);
                _visualCount--;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (controller == null || controller.Stations == null) return;
            if (!controller.Stations.IsUnlocked(stationIndex)) return;
            var avatar = other.GetComponent<AvatarController>();
            if (avatar != null) avatar.TryPickupFrom(stationIndex);
        }
    }
}
