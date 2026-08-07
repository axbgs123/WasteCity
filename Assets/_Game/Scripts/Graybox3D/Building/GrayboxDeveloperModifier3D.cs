#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperModifier3D
    {
        private readonly GrayboxBuildingSession3D session;
        private readonly GrayboxMobileCityController3D city;
        private readonly GrayboxBuildingWorldView3D presentation;

        public GrayboxDeveloperModifier3D(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxBuildingWorldView3D presentation)
        {
            this.session = session ?? throw new ArgumentNullException(
                nameof(session));
            this.city = city ?? throw new ArgumentNullException(nameof(city));
            this.presentation = presentation ?? throw new ArgumentNullException(
                nameof(presentation));
        }

        public bool AddResource(string resourceId, int amount)
        {
            if (!IsKnownResource(resourceId) || amount <= 0)
                return false;
            session.Inventory.Add(resourceId, amount);
            return true;
        }

        public bool SetResource(string resourceId, int amount)
        {
            if (!IsKnownResource(resourceId) || amount < 0)
                return false;
            session.Inventory.Set(resourceId, amount);
            return true;
        }

        public bool ClearResource(string resourceId)
        {
            if (!IsKnownResource(resourceId))
                return false;
            session.Inventory.Set(resourceId, 0);
            return true;
        }

        public bool UnlockResearch(string researchId)
        {
            if (ResearchCatalog.Find(researchId) == null)
                return false;
            session.UnlockResearchForDevelopment(researchId);
            return true;
        }

        public bool UnlockRoute(ContentRoute route)
        {
            if (route != ContentRoute.Technology &&
                route != ContentRoute.Cultivation &&
                route != ContentRoute.BiologicalAscension &&
                route != ContentRoute.Psionics)
                return false;
            session.UnlockRouteForDevelopment(route);
            return true;
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

        public bool CompleteCityTransition()
        {
            return city.CompleteDeploymentTransitionForDevelopment();
        }

        public bool SetConstructionSpeed(DevelopmentConstructionSpeed speed)
        {
            if (speed != DevelopmentConstructionSpeed.Normal &&
                speed != DevelopmentConstructionSpeed.Fast10 &&
                speed != DevelopmentConstructionSpeed.Fast100)
                return false;
            session.SetConstructionMultiplierForDevelopment((float)speed);
            return true;
        }

        public void CompleteAllConstruction()
        {
            session.CompleteAllConstructionForDevelopment(presentation);
        }

        private static bool IsKnownResource(string resourceId)
        {
            return Array.IndexOf(ResourceIds.All, resourceId) >= 0;
        }
    }
}
#endif
