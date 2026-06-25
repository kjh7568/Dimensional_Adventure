using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6칸(3x2) 카드 배치 패널. RunState.PlayerDeck(6장) 의 슬롯 배치를 결정한다.
/// FirstSetup: 빈 6칸 + 대기 큐. 큐 머리 카드를 빈 슬롯 탭으로 배치. 6장 완료 후 [확정].
/// DeckView: RunState.layout 을 로드. 슬롯 탭 → 선택 + 상세 표시 + 큐 자리에 미리보기.
///           다른 슬롯 탭 → 두 슬롯 swap. 같은 슬롯 재탭 → 해제. [확정] 으로 변경 저장.
/// 큐 카드는 카드 비율을 유지한 채 X 좌표로 옆으로 쌓이고, 머리가 빠지면 우측에서 좌측으로 슬라이드.
/// </summary>
public class DeckLayoutPanel : MonoBehaviour
{
    public enum Mode { FirstSetup, DeckView }
    private enum SlideKind { None, Place, Return }

    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private CardView[] slotViews;
    [SerializeField] private CardView[] queueViews;
    [SerializeField] private DeckDetailView detail;
    [SerializeField] private Button confirmButton;

    [Header("Queue Layout")]
    [Tooltip("큐 카드 간 X 오프셋. 카드 폭보다 작게 두면 카드들이 더미처럼 겹쳐 보임.")]
    [SerializeField] private float queueStepX = 60f;
    [Tooltip("큐 슬라이드 애니메이션 길이(초). 0 이면 즉시 스냅.")]
    [SerializeField] private float queueSlideDuration = 0.15f;

    /// <summary>패널이 [확정] 으로 닫혔을 때 1회 호출. MapPanelController 가 노드 입력 잠금 해제용으로 구독.</summary>
    public event Action OnClosed;

    private Mode currentMode;
    private readonly int[] slotIndices = new int[RunState.LayoutSize];
    private readonly List<int> queue = new List<int>(RunState.LayoutSize);
    /// <summary>DeckView 모드에서만 사용. -1 = 미선택, 그 외 = slotIndices 의 슬롯 인덱스(0..5).</summary>
    private int selectedSlot = -1;
    private Coroutine slideRoutine;

    private void Awake()
    {
        if (slotViews != null)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null) continue;
                int captured = i;
                slotViews[i].OnClicked += _ => HandleSlotClicked(captured);
            }
        }
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirm);
        if (root != null) root.SetActive(false);
    }

    public void Open(Mode mode)
    {
        currentMode = mode;
        selectedSlot = -1;
        queue.Clear();
        if (root != null) root.SetActive(true);

        if (mode == Mode.FirstSetup)
        {
            for (int i = 0; i < slotIndices.Length; i++) slotIndices[i] = -1;
            for (int i = 0; i < RunState.PlayerDeck.Count && i < RunState.LayoutSize; i++)
                queue.Add(i);
        }
        else
        {
            for (int i = 0; i < slotIndices.Length; i++) slotIndices[i] = RunState.GetLayoutAt(i);
        }

        BindAll();
        LayoutQueueImmediate();
    }

    private void Close()
    {
        if (root != null) root.SetActive(false);
        OnClosed?.Invoke();
    }

    private void HandleSlotClicked(int slot)
    {
        if (currentMode == Mode.FirstSetup) HandleSlotClickedFirstSetup(slot);
        else HandleSlotClickedDeckView(slot);
    }

    private void HandleSlotClickedFirstSetup(int slot)
    {
        if (slotIndices[slot] >= 0) return;
        if (queue.Count == 0) return;
        slotIndices[slot] = queue[0];
        queue.RemoveAt(0);
        BindAll();
        PlayQueueSlide(SlideKind.Place);
    }

    private void HandleSlotClickedDeckView(int slot)
    {
        if (selectedSlot == -1)
        {
            if (slotIndices[slot] < 0) return;
            selectedSlot = slot;
        }
        else if (selectedSlot == slot)
        {
            selectedSlot = -1;
        }
        else
        {
            int tmp = slotIndices[selectedSlot];
            slotIndices[selectedSlot] = slotIndices[slot];
            slotIndices[slot] = tmp;
            selectedSlot = -1;
        }

        BindAll();
        LayoutQueueImmediate();
    }

    private void HandleConfirm()
    {
        if (!CanConfirm()) return;
        RunState.CommitLayout(slotIndices);
        SaveBridge.SaveRunProgress();
        Close();
    }

    /// <summary>FirstSetup 은 6칸 모두 채워야 활성. DeckView 는 항상 활성(현 상태 그대로 저장).</summary>
    private bool CanConfirm()
    {
        if (currentMode == Mode.DeckView) return true;
        for (int i = 0; i < slotIndices.Length; i++)
            if (slotIndices[i] < 0) return false;
        return true;
    }

    /// <summary>슬롯/큐 카드 binding 과 상세/확정 버튼 상태만 갱신. 위치는 건드리지 않는다.</summary>
    private void BindAll()
    {
        for (int i = 0; i < slotViews.Length; i++)
        {
            int deckIdx = (i < slotIndices.Length) ? slotIndices[i] : -1;
            BindSlot(slotViews[i], deckIdx);
            slotViews[i].SetHighlight(i == selectedSlot);
        }

        if (queueViews != null)
        {
            for (int i = 0; i < queueViews.Length; i++)
            {
                if (queueViews[i] == null) continue;
                int deckIdx = -1;
                if (currentMode == Mode.FirstSetup)
                {
                    if (i < queue.Count) deckIdx = queue[i];
                }
                else
                {
                    if (i == 0 && selectedSlot >= 0) deckIdx = slotIndices[selectedSlot];
                }
                if (deckIdx < 0)
                {
                    queueViews[i].gameObject.SetActive(false);
                }
                else
                {
                    queueViews[i].gameObject.SetActive(true);
                    BindSlot(queueViews[i], deckIdx);
                    queueViews[i].SetInteractable(false);
                }
            }
        }

        int detailDeckIdx = -1;
        if (currentMode == Mode.FirstSetup)
        {
            if (queue.Count > 0) detailDeckIdx = queue[0];
        }
        else
        {
            if (selectedSlot >= 0) detailDeckIdx = slotIndices[selectedSlot];
        }
        if (detail != null)
        {
            if (detailDeckIdx >= 0 && detailDeckIdx < RunState.PlayerDeck.Count)
                detail.Bind(RunState.PlayerDeck[detailDeckIdx], detailDeckIdx);
            else
                detail.Clear();
        }

        if (confirmButton != null) confirmButton.interactable = CanConfirm();
    }

    /// <summary>큐 카드들의 anchoredPosition.x 를 i*queueStepX 로 즉시 스냅 + sibling 순서 정리.</summary>
    private void LayoutQueueImmediate()
    {
        StopSlideIfRunning();
        if (queueViews == null) return;
        for (int i = 0; i < queueViews.Length; i++)
        {
            if (queueViews[i] == null) continue;
            var rt = queueViews[i].transform as RectTransform;
            if (rt == null) continue;
            rt.anchoredPosition = new Vector2(i * queueStepX, rt.anchoredPosition.y);
        }
        UpdateQueueSiblingOrder();
    }

    /// <summary>큐 머리(i=0)가 가장 위에 그려지도록 sibling 순서 역배치.</summary>
    private void UpdateQueueSiblingOrder()
    {
        if (queueViews == null) return;
        for (int i = queueViews.Length - 1; i >= 0; i--)
            if (queueViews[i] != null) queueViews[i].transform.SetAsLastSibling();
    }

    private void PlayQueueSlide(SlideKind kind)
    {
        if (kind == SlideKind.None || queueViews == null || queueSlideDuration <= 0f)
        {
            LayoutQueueImmediate();
            return;
        }
        StopSlideIfRunning();
        slideRoutine = StartCoroutine(SlideCoroutine(kind));
    }

    private void StopSlideIfRunning()
    {
        if (slideRoutine != null) { StopCoroutine(slideRoutine); slideRoutine = null; }
    }

    /// <summary>
    /// Place: 큐 머리가 빠짐 → 모든 카드가 한 칸 좌측으로 슬라이드 (시작 = (i+1)*stepX, 목표 = i*stepX).
    /// Return: 카드가 큐 맨 앞에 추가됨 → 모든 카드가 한 칸 우측으로 슬라이드 (시작 = (i-1)*stepX, 목표 = i*stepX).
    /// </summary>
    private IEnumerator SlideCoroutine(SlideKind kind)
    {
        UpdateQueueSiblingOrder();
        int n = queueViews.Length;
        var rts = new RectTransform[n];
        var startsX = new float[n];
        var endsX = new float[n];
        float delta = (kind == SlideKind.Place) ? 1f : -1f;
        for (int i = 0; i < n; i++)
        {
            if (queueViews[i] == null) { rts[i] = null; continue; }
            rts[i] = queueViews[i].transform as RectTransform;
            endsX[i] = i * queueStepX;
            startsX[i] = (i + delta) * queueStepX;
            if (rts[i] != null)
                rts[i].anchoredPosition = new Vector2(startsX[i], rts[i].anchoredPosition.y);
        }

        float duration = queueSlideDuration;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                float x = Mathf.Lerp(startsX[i], endsX[i], u);
                rts[i].anchoredPosition = new Vector2(x, rts[i].anchoredPosition.y);
            }
            yield return null;
        }
        for (int i = 0; i < n; i++)
        {
            if (rts[i] == null) continue;
            rts[i].anchoredPosition = new Vector2(endsX[i], rts[i].anchoredPosition.y);
        }
        slideRoutine = null;
    }

    private void BindSlot(CardView view, int deckIdx)
    {
        if (view == null) return;
        if (deckIdx < 0 || deckIdx >= RunState.PlayerDeck.Count)
        {
            view.BindPreview(null, 0, 0, interactable: true);
            view.SetHighlight(false);
            return;
        }
        int hpBonus = RunState.GlobalHpBonus + RunState.GetPerCardHpBonus(deckIdx);
        int skillBonus = RunState.GetPerCardSkillBonus(deckIdx);
        view.BindPreview(RunState.PlayerDeck[deckIdx], hpBonus, skillBonus, interactable: true);
    }
}
