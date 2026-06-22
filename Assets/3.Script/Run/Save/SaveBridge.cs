using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 시스템과 SaveSystem 사이의 고수준 접착부. 호출자가 "런 진행이 갱신됐다 / 전투 체크포인트다 / 런이 끝났다" 같은
/// 의미적 시점을 알려주면 RunState/BattleController 에서 적절한 스냅샷을 떠 SaveSystem.Current 에 넣고 Flush 한다.
/// 게임 코드가 세이브를 읽고 쓰는 유일한 진입점 — Save 폴더 밖에서는 SaveSystem 을 직접 부르지 않는다.
/// </summary>
public static class SaveBridge
{
    /// <summary>이어하기 가능한 런이 저장돼 있는가. 설정만 있는 상태는 해당 안 됨.</summary>
    public static bool HasResumableRun() => SaveSystem.HasResumableRun();

    /// <summary>오프닝 컷씬을 한 번이라도 봤는가.</summary>
    public static bool HasSeenIntro => SaveSystem.Current.hasSeenIntro;

    /// <summary>저장된 설정(읽기용). 변경 후 디스크 반영은 <see cref="SaveSettings"/>.</summary>
    public static SettingsState Settings => SaveSystem.Current.settings;

    /// <summary>전투 밖에서 런 진행이 갱신된 시점 — Run 만 저장하고 battle 슬롯은 비운다.
    /// 지도 도착·노드 진입 직전·이벤트 완료·전투 승리 후 노드 전진 모두 이 한 경로를 쓴다.
    /// battle 이 비어 있으므로 이어하기는 항상 지도로 복귀한다.</summary>
    public static void SaveRunProgress()
    {
        var data = SaveSystem.Current;
        data.run = RunState.CaptureSnapshot();
        data.battle = null;
        SaveSystem.Flush();
    }

    /// <summary>전투 중 안정 시점(플레이어 입력 대기) — Run + Battle 스냅샷 갱신. 이 호출이 한 번이라도 일어나야 이어하기가 전투로 복귀.
    /// 입력 대기 중이 아니라 스냅샷을 못 뜨면 아무것도 바꾸지 않고 false 를 반환한다(직전 체크포인트 유지).</summary>
    public static bool SaveBattleCheckpoint(BattleController battle)
    {
        if (battle == null) return false;
        var snap = battle.CaptureSnapshot();
        if (snap == null) return false;
        var data = SaveSystem.Current;
        data.run = RunState.CaptureSnapshot();
        data.battle = snap;
        SaveSystem.Flush();
        return true;
    }

    /// <summary>지금 진행을 디스크에 남긴다 — 앱 일시정지/종료, 타이틀로 나가기에서 사용.
    /// 전투 씬이면 체크포인트를 시도하고, 못 뜨면(연출·적 턴 중) 직전 체크포인트를 둔 채 메모리 상태만 Flush.</summary>
    public static void SaveCurrentProgress()
    {
        var battle = Object.FindObjectOfType<BattleController>();
        if (!SaveBattleCheckpoint(battle)) SaveSystem.Flush();
    }

    /// <summary>설정 변경 직후 호출. run/battle 은 건드리지 않음.</summary>
    public static void SaveSettings()
    {
        var data = SaveSystem.Current;
        if (data.settings == null) data.settings = new SettingsState();
        if (AudioManager.Instance != null)
        {
            data.settings.bgmVolume = AudioManager.Instance.BgmVolume;
            data.settings.sfxVolume = AudioManager.Instance.SfxVolume;
        }
        SaveSystem.Flush();
    }

    /// <summary>오프닝 컷씬을 봤다고 기록. 이미 기록돼 있으면 디스크 쓰기 없음.</summary>
    public static void MarkIntroSeen()
    {
        var data = SaveSystem.Current;
        if (data.hasSeenIntro) return;
        data.hasSeenIntro = true;
        SaveSystem.Flush();
    }

    /// <summary>진행 중인 런을 메모리(RunState)와 세이브(run/battle 슬롯) 양쪽에서 비운다. 설정·도감·플래그는 보존.
    /// 새 게임 시작, 패배 등 "런이 끝나는" 모든 경로가 이 한 메서드를 쓴다.</summary>
    public static void ClearRun()
    {
        SaveSystem.ClearRun();
        RunState.ResetRun();
    }

    /// <summary>이어하기 — SaveSystem.Current.run 으로 RunState 복원 + 재진입할 씬 로드.</summary>
    public static void ResumeRun()
    {
        var data = SaveSystem.Current;
        if (data.run == null)
        {
            Debug.LogWarning("[SaveBridge] Resume 시도 — 저장된 Run 없음. 새 게임으로 폴백.");
            ClearRun();
            SceneLoader.LoadAsync(SceneNames.Map);
            return;
        }

        RunState.RestoreFromSnapshot(data.run);
        if (data.battle != null)
        {
            BattleController.PendingRestoreSnapshot = data.battle;
            SceneLoader.LoadAsync(SceneNames.Battle);
        }
        else
        {
            SceneLoader.LoadAsync(SceneNames.Map);
        }
    }
}
