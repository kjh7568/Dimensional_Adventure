using UnityEngine;

/// <summary>
/// BattleController 의 상태 전이를 듣고 SaveBridge 를 호출.
/// 매 플레이어 턴의 입력 대기(AwaitCardSelect) 진입마다 체크포인트를 남겨, 강제 종료돼도 잃는 진행은 한 턴 미만이 되게 한다.
/// 일시정지/종료는 AutoSaveHook 이, "타이틀로 나가기"는 SettingsPanel.OnReturnToTitle 이 보강하며,
/// 그때도 입력 대기 중이 아니면 새로 캡처하지 않고 이 체크포인트를 그대로 둔다(BattleController.CaptureSnapshot 참고).
/// 전투 종료(승리/패배) 시점의 세이브 처리는 ResultUI 가 전담한다.
/// BattleScene 루트에 BattleController 와 같이 두면 된다.
/// </summary>
[RequireComponent(typeof(BattleController))]
public class BattleSaveTrigger : MonoBehaviour
{
    [SerializeField] private BattleController battle;
    private bool subscribed;

    private void Awake()
    {
        if (battle == null) battle = GetComponent<BattleController>();
    }

    private void OnEnable()
    {
        if (battle == null) return;
        battle.OnStateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed || battle == null) return;
        battle.OnStateChanged -= HandleStateChanged;
        subscribed = false;
    }

    /// <summary>플레이어 턴 입력 대기에 들어설 때마다 체크포인트 저장.</summary>
    private void HandleStateChanged(BattleState s)
    {
        if (s == BattleState.AwaitCardSelect)
            SaveBridge.SaveBattleCheckpoint(battle);
    }
}
