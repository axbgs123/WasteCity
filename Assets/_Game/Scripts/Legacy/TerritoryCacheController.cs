using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class TerritoryCacheController : MonoBehaviour
    {
        [SerializeField] private LegacySelectionController legacy;
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        public TerritoryResourceNetwork Network { get; private set; }
        public TerritoryExtractionModel Extraction { get; } = new TerritoryExtractionModel();
        public bool Activated { get; private set; }
        public int X { get; private set; } public int Y { get; private set; }
        private SpriteRenderer marker; private string last;
        private bool Quantum => legacy?.Model?.Selected?.Id.Value=="core.legacy.quantum-entanglement";
        private void Start()=>TryInitialize();
        private void TryInitialize(){if(Network!=null||world.Model==null)return;Network=new TerritoryResourceNetwork(economy.Inventory);FindSite();CreateMarker();}
        private void FindSite(){for(int y=0;y<world.Model.Height;y++)for(int x=0;x<world.Model.Width;x++)if(world.Model.Get(x,y).HasResource&&Vector2.Distance(WorldPosition(x,y),city.transform.position)>7f){X=x;Y=y;return;}}
        private void CreateMarker(){var item=new GameObject("TerritoryCachePlaceholder");item.transform.SetParent(transform);item.transform.position=WorldPosition(X,Y);item.transform.localScale=Vector3.one*.8f;var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);marker=item.AddComponent<SpriteRenderer>();marker.sprite=sprite;marker.color=Color.yellow;marker.sortingOrder=5;}
        private void Update()
        {
            if(Network==null){TryInitialize();return;}marker.enabled=world.Model.IsRevealed(X,Y);bool near=Vector2.Distance(city.transform.position,WorldPosition(X,Y))<=1.5f;
            if(near&&city.Deployment.Mode==CityMode.Fortress&&Keyboard.current!=null&&Keyboard.current.gKey.wasPressedThisFrame){if(!Activated){Activated=true;last="领地采集缓存已激活";}else{int total=0;foreach(string id in ResourceIds.Base)total+=Network.Collect(id);last=$"从领地缓存收取 {total} 单位资源";}}
            if(!Activated)return;int cycles=Extraction.Tick(Time.deltaTime);if(cycles<=0)return;int harvested=world.Model.Harvest(X,Y,cycles,out string harvestedId);if(harvested>0)Network.Deposit(harvestedId,harvested,Quantum);
        }
        public void Restore(bool active,float progress,int[] local){Activated=active;Extraction.Restore(progress);if(local!=null)for(int i=0;i<ResourceIds.Base.Length&&i<local.Length;i++)Network.Restore(ResourceIds.Base[i],local[i]);}
        public int[] CaptureLocal(){var values=new int[ResourceIds.Base.Length];for(int i=0;i<values.Length;i++)values[i]=Network.Local(ResourceIds.Base[i]);return values;}
        private Vector2 WorldPosition(int x,int y)=>new Vector2(x-world.Model.Width*.5f,y-world.Model.Height*.5f);
        private void OnGUI(){if(Network==null)return;bool near=Vector2.Distance(city.transform.position,WorldPosition(X,Y))<=1.5f;if(near)GUI.Box(new Rect(18,Screen.height-240f,600f,50f),Activated?"领地缓存：堡垒状态按 G 收取；量子纠缠会自动汇入城市":"领地缓存：堡垒状态按 G 激活远程采集");if(Quantum&&Activated)GUI.Box(new Rect(Screen.width-390f,595f,370f,45f),"量子纠缠 Lv.1：领地基础资源实时共享");if(!string.IsNullOrEmpty(last))GUI.Box(new Rect(18,300f,450f,40f),last);}
    }
}
