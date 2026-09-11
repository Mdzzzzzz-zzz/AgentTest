using UnityEngine;

namespace DiceDemo.DemoC
{
    public sealed class DemoCFallDetector : MonoBehaviour
    {
        [SerializeField] private DemoCDiceRuntime _runtime;

        public void Configure(DemoCDiceRuntime runtime)
        {
            _runtime = runtime;
        }

        private void OnTriggerEnter(Collider other)
        {
            DemoCDie die = other == null ? null : other.GetComponentInParent<DemoCDie>();
            if (_runtime != null && die != null) _runtime.RegisterDrop(die);
        }
    }
}
