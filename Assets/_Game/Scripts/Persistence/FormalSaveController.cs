using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Legacy;
namespace WasteCity.Persistence
{
    public sealed class FormalSaveController : MonoBehaviour
    {
        [SerializeField] PlaceholderMobileCity city; [SerializeField] FormalEconomyController economy; [SerializeField] LegacySelectionController legacy;
        string SavePath => Path.Combine(Application.persistentDataPath,"formal-world.json");
        void Update(){if(Keyboard.current==null)return;if(Keyboard.current.f5Key.wasPressedThisFrame)Save();if(Keyboard.current.f9Key.wasPressedThisFrame)Load();}
        public void Save(){Directory.CreateDirectory(Application.persistentDataPath);if(File.Exists(SavePath))File.Copy(SavePath,SavePath+".bak",true);var i=economy.Inventory;
            File.WriteAllText(SavePath,FormalSaveCodec.Encode(new FormalSaveData{worldSeed=8128,cityX=city.transform.position.x,cityY=city.transform.position.y,iron=i.Get(ResourceIds.Iron),energyCrystal=i.Get(ResourceIds.EnergyCrystal),stone=i.Get(ResourceIds.Stone),biomass=i.Get(ResourceIds.Biomass),water=i.Get(ResourceIds.Water),legacyPathId=legacy.Model.Selected?.Id.Value}));}
        public bool Load(){var d=Read(SavePath)??Read(SavePath+".bak");if(d==null)return false;city.transform.position=new Vector3(d.cityX,d.cityY,city.transform.position.z);var i=economy.Inventory;i.Set(ResourceIds.Iron,d.iron);i.Set(ResourceIds.EnergyCrystal,d.energyCrystal);i.Set(ResourceIds.Stone,d.stone);i.Set(ResourceIds.Biomass,d.biomass);i.Set(ResourceIds.Water,d.water);if(!string.IsNullOrEmpty(d.legacyPathId))legacy.Model.Restore(d.legacyPathId);return true;}
        static FormalSaveData Read(string path)=>File.Exists(path)?FormalSaveCodec.Decode(File.ReadAllText(path)):null;
    }
}
