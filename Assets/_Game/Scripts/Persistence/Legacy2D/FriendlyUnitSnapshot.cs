using System;

namespace WasteCity.Combat
{
    [Serializable]
    public sealed class FriendlyUnitSnapshot
    {
        public FriendlyUnitSnapshot()
        {
        }

        public float x, y;
        public int health;
        public float maintenanceElapsed;
        public bool maintenanceActive;
    }
}
