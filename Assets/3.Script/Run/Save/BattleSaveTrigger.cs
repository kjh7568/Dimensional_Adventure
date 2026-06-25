using UnityEngine;

/// <summary>
/// BattleController 의 상태 전이를 듣고 SaveBridge 를 호출.
/// 체크포인트는 전투마다 첫 플레이어 입력 대기(AwaitCardSelect) 진입에서 한 번만 남긴다.
/// 매 턴 동기 디스크 쓰기(JSON 직렬화 + 파일 교체)를 없애는 대신, 강제 종료 시에는 전투 시작 시점으로 복귀한다.
/// 일시정지/종료는 AutoSaveHook 이, "타이틀로 나가기"는 SettingsPanel.OnReturnToTitle 이 SaveBridge.SaveCurrentProgress 로 보강하며,
/// 그때도 입력 대기 중이 아니면 새로 캡처하지 않고 직전 체크포인트를 그대로 둔다(BattleController.CaptureSnapshot 참고).
/// 전투 종료(승리/패배) 시점의 세이브 처리는 ResultUI 가 전담한다.
/// BattleScene 루트에 BattleController 와 같이 두면 된다.
/// </summary>
[RequireComponent(typeof(BattleController))]
public class BattleSaveTrigger : MonoBehaviour
{
    [SerializeField] private BattleController battle;
    private bool subscribed;
    private bool hasSavedBattleStart;

    private void Awake()
    {
        if (battle == null) battle = GetComponent<BattleController>();
    }

    private void OnEnable()
    {
        if (battle == null) return;
        battle.OnStateChanged += HandleStateChanged;
        subscribed = true;
        hasSavedBattleStart = false;
    }

    private void OnDisable()
    {
        if (!subscribed || battle == null) return;
        battle.OnStateChanged -= HandleStateChanged;
        subscribed = false;
    }

    /// <summary>전투 시작 후 첫 AwaitCardSelect 진입에서만 체크포인트 저장. 이후 턴에서는 저장하지 않는다.</summary>
    private void HandleStateChanged(BattleState s)
    {
        if (s != BattleState.AwaitCardSelect || hasSavedBattleStart) return;
        hasSavedBattleStart = true;
        SaveBridge.SaveBattleCheckpoint(battle);
    }
}
