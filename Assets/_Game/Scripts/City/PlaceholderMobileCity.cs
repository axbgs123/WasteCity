using UnityEngine;
using UnityEngine.InputSystem;

namespace WasteCity.City
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlaceholderMobileCity : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        private Rigidbody2D body;
        private Vector2 input;
        private CityDeploymentModel deployment;
        public CityDeploymentModel Deployment => deployment;
        public bool LongWorkAllowed => deployment != null && CityOperationalRules.LongWorkAllowed(deployment.Mode);
        private void Awake() { body = GetComponent<Rigidbody2D>(); deployment = new CityDeploymentModel(3f, 5f); }
        private void Update()
        {
            deployment.Tick(Time.deltaTime);
            if (Keyboard.current == null) { input = Vector2.zero; return; }
            if (Keyboard.current.xKey.wasPressedThisFrame) deployment.Toggle();
            if (deployment.Mode != CityMode.Mobile) { input = Vector2.zero; return; }
            input = new Vector2((Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
                (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)).normalized;
        }
        private void FixedUpdate() => body.MovePosition(body.position + input * moveSpeed * Time.fixedDeltaTime);
        public void RestoreDeployment(CityMode mode,float remaining)=>deployment.Restore(mode,remaining);
    }
}
