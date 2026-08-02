using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WasteCity.Combat;
using WasteCity.Narrative;
using WasteCity.Persistence;

namespace WasteCity.Core
{
    public sealed class FormalSessionController:MonoBehaviour
    {
        [SerializeField] private HealthComponent cityHealth;
        [SerializeField] private FormalSaveController saves;
        [SerializeField] private FormalGuidanceController guidance;
        public GameSessionStateModel Model { get; }=new GameSessionStateModel();
        public GuidanceStage LastCheckpointStage { get; private set; }
        private void Start(){Time.timeScale=1;cityHealth.Value.Died+=OnDefeated;guidance.Model.Changed+=OnGuidanceChanged;}
        private void Update()
        {
            if(Keyboard.current==null)return;
            if(Keyboard.current.pKey.wasPressedThisFrame&&Model.TogglePause())Time.timeScale=Model.State==GameSessionState.Paused?0:1;
            if(Model.State==GameSessionState.Defeated&&Keyboard.current.rKey.wasPressedThisFrame)Retry();
        }
        private void OnDefeated(){if(Model.Defeat())Time.timeScale=0;}
        private void OnGuidanceChanged(GuidanceStage stage)
        {
            if(stage!=GuidanceStage.FirstFortress&&stage!=GuidanceStage.ProductionChain&&stage!=GuidanceStage.PressureTest&&stage!=GuidanceStage.Broodmother)return;
            saves.Save();LastCheckpointStage=stage;
        }
        public void Retry()
        {
            Time.timeScale=1;if(!saves.Load())SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);Model.ResumeAfterRetry();
        }
        private void OnGUI()
        {
            if(Model.State==GameSessionState.Paused)GUI.Box(new Rect(Screen.width*.5f-170,Screen.height*.5f-45,340,90),"游戏已暂停\n按 P 继续");
            if(Model.State==GameSessionState.Defeated)GUI.Box(new Rect(Screen.width*.5f-220,Screen.height*.5f-70,440,140),"移动城市核心已失效\n按 R 从最近检查点重试\n检查点会保留资源、建筑、波次、敌人和目标进度");
        }
    }
}
