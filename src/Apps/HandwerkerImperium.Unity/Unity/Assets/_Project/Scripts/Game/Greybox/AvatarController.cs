using UnityEngine;
using UnityEngine.InputSystem;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Avatar mit CharacterController + WASD/Gamepad (New Input System) + Carry-Stack-Visual
    /// (gestapelte Wuerfel skalieren mit der Tragmenge). Traegt Waren von Stationen zum Tresen; Aufnahme/
    /// Abgabe laufen ueber den GreyboxGameController/§3-Services. (On-Screen-Joystick fuer Mobile in P1.)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class AvatarController : MonoBehaviour
    {
        [SerializeField] private GreyboxGameController controller;
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private GameObject carryWarePrefab;
        [Tooltip("Optional: Trag-Ware je Station (Index = Stations-Index) — Fallback ist carryWarePrefab.")]
        [SerializeField] private GameObject[] stationWarePrefabs;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotateSpeedDeg = 720f;
        [SerializeField] private float carryWareHeight = 0.45f;
        [SerializeField] private float fallbackWalkSpeed = 5f;

        private CharacterController _cc;
        private float _vy;
        private int _visualCount;

        public int CarriedCount { get; private set; }
        public int CarriedStation { get; private set; } = -1;

        public bool IsFull => controller != null && controller.Economy != null && CarriedCount >= controller.Economy.CarryCapacity;

        private void Awake() => _cc = GetComponent<CharacterController>();

        private void Update()
        {
            Vector2 input = ReadMove();
            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = (controller != null && controller.Balancing != null) ? (float)controller.Balancing.WalkSpeed : fallbackWalkSpeed;

            if (_cc.isGrounded && _vy < 0f) _vy = -1f;
            _vy += gravity * Time.deltaTime;
            Vector3 velocity = move * speed + Vector3.up * _vy;
            _cc.Move(velocity * Time.deltaTime);

            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotateSpeedDeg * Time.deltaTime);
            }
        }

        private static Vector2 ReadMove()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            }
            var gp = Gamepad.current;
            if (gp != null) v += gp.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>Nimmt Waren an einer Station auf (bis Trag-Kapazitaet; nur von EINER Station gleichzeitig). Liefert die aufgenommene Menge.</summary>
        public int TryPickupFrom(int stationIndex)
        {
            if (controller == null || IsFull) return 0;
            if (CarriedStation != -1 && CarriedStation != stationIndex) return 0; // erst abgeben
            int capacity = controller.Economy.CarryCapacity - CarriedCount;
            if (capacity <= 0) return 0;
            int taken = controller.Stations.Pickup(stationIndex, capacity);
            if (taken > 0)
            {
                CarriedStation = stationIndex;
                CarriedCount += taken;
                RebuildCarryVisual();
                controller.Audio?.Play(GameSfx.Pickup);
            }
            return taken;
        }

        /// <summary>Gibt alle getragenen Waren am Tresen ab (Geld via Economy). Liefert die abgegebene Anzahl (fuer Cash-Spawn).</summary>
        public int Deposit()
        {
            if (controller == null || CarriedCount <= 0) return 0;
            int sold = CarriedCount;
            controller.Economy.Sell(CarriedStation, sold);
            CarriedCount = 0;
            CarriedStation = -1;
            RebuildCarryVisual();
            return sold;
        }

        private void RebuildCarryVisual()
        {
            if (carryAnchor == null) return;
            GameObject prefab = WarePrefabFor(CarriedStation);
            while (_visualCount < CarriedCount)
            {
                if (prefab != null)
                {
                    var go = Instantiate(prefab, carryAnchor);
                    go.transform.localPosition = new Vector3(0f, _visualCount * carryWareHeight, 0f);
                    go.transform.localRotation = Quaternion.identity;
                }
                _visualCount++;
            }
            while (_visualCount > CarriedCount && carryAnchor.childCount > 0)
            {
                Destroy(carryAnchor.GetChild(carryAnchor.childCount - 1).gameObject);
                _visualCount--;
            }
        }

        /// <summary>Trag-Ware der Quell-Station (stationsspezifisches Visual), sonst der generische Fallback.</summary>
        private GameObject WarePrefabFor(int stationIndex)
        {
            if (stationWarePrefabs != null && stationIndex >= 0 && stationIndex < stationWarePrefabs.Length && stationWarePrefabs[stationIndex] != null)
                return stationWarePrefabs[stationIndex];
            return carryWarePrefab;
        }
    }
}
