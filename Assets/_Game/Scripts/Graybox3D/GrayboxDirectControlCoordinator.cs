using System;
using UnityEngine;
using WasteCity.City;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxDirectControlCoordinator : MonoBehaviour
    {
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxLeaderController3D leader;

        public DirectControlTarget ControlTarget { get; private set; } =
            DirectControlTarget.City;

        public event Action<DirectControlTarget> TargetChanged;

        public void Configure(
            GrayboxMobileCityController3D city,
            GrayboxLeaderController3D leader)
        {
            this.city = city;
            this.leader = leader;
        }

        public bool Refresh()
        {
            DirectControlTarget requested =
                DirectControlRules.Resolve(
                    city?.Deployment?.Mode ?? CityMode.Mobile,
                    leader != null && leader.Model.Recruited);
            if (requested == ControlTarget)
                return false;

            ControlTarget = requested;
            TargetChanged?.Invoke(ControlTarget);
            return true;
        }
    }
}
