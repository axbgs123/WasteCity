#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifier3D
    {
        private const int ResourceIncrement = 100;
        private const int LargeResourceIncrement = 1000;

        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxMobileCityController3D city;
        private readonly IGrayboxBuildingPresentation3D presentation;
        private string currentResource = ResourceIds.Iron;

        public GrayboxDeveloperModifier3D(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.city = city ?? throw new ArgumentNullException(nameof(city));
        }

        public GrayboxDeveloperModifier3D(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            IGrayboxBuildingPresentation3D presentation)
            : this(session, city)
        {
            this.presentation = presentation ?? throw new ArgumentNullException(
                nameof(presentation));
        }

        public string CurrentResource => currentResource;

        public bool SetCurrentResource(string resourceId)
        {
            if (Array.IndexOf(ResourceIds.All, resourceId) < 0)
                return false;
            currentResource = resourceId;
            return true;
        }

        public int AddCurrentResource100()
        {
            return session.Inventory.Add(currentResource, ResourceIncrement);
        }

        public int AddCurrentResource1000()
        {
            return session.Inventory.Add(
                currentResource,
                LargeResourceIncrement);
        }

        public void ClearCurrentResource()
        {
            session.Inventory.Set(currentResource, 0);
        }

        public bool SetCurrentResourceAmount(int amount)
        {
            if (amount < 0) return false;
            session.Inventory.Set(currentResource, amount);
            return true;
        }

        public void UnlockResearch(string researchId)
        {
            session.UnlockResearchForDevelopment(researchId);
        }

        public void UnlockRoute(ContentRoute route)
        {
            session.UnlockRouteForDevelopment(route);
        }

        public void UnlockAllResearch()
        {
            session.UnlockAllResearchForDevelopment();
        }

        public bool SetCityMode(CityMode mode)
        {
            if (mode != CityMode.Mobile && mode != CityMode.Fortress)
                return false;
            return city.RestoreDeploymentForDevelopment(mode);
        }

        public bool CompleteDeploymentTransition()
        {
            return city.CompleteDeploymentTransitionForDevelopment();
        }

        public bool SetConstructionMultiplier(float multiplier)
        {
            if (multiplier != 1f && multiplier != 10f && multiplier != 100f)
                return false;
            session.SetConstructionMultiplierForDevelopment(multiplier);
            return true;
        }

        public void CompleteAllConstruction(
            IGrayboxBuildingPresentation3D presentation)
        {
            session.CompleteAllConstructionForDevelopment(presentation);
        }

        public bool CompleteAllConstruction()
        {
            if (presentation == null) return false;
            session.CompleteAllConstructionForDevelopment(presentation);
            return true;
        }
    }
}
#endif
