using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DeckLayoutPanel 상단의 카드 상세 영역. 선택된 카드 한 장의 일러스트/이름/클래스/HP/스킬/강화 표시.
/// 카드 미선택 상태에서는 Clear() 로 모든 텍스트/이미지를 비움.
/// </summary>
public class DeckDetailView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image illustration;
    [SerializeField] private TMP_Text classLabel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text skillText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Upgrade Summary")]
    [Tooltip("강화 내역 섹션 루트. 보너스가 전부 0 이면 SetActive(false). 없으면 기능 비활성.")]
    [SerializeField] private GameObject upgradeRoot;
    [Tooltip("강화 내역 합산 텍스트. 'HP +N · Skill +N' 형태. 없으면 hpText/skillText 의 (+N) 표기로 폴백.")]
    [SerializeField] private TMP_Text upgradeText;

    [Header("Class Display")]
    [Tooltip("CardType ↔ 한글 표시명 매핑. 비워두면 enum 이름 그대로 표시.")]
    [SerializeField] private ClassLabelPair[] classLabels;

    [System.Serializable]
    public struct ClassLabelPair
    {
        public CardType type;
        public string label;
    }

    public void Clear()
    {
        if (illustration != null) { illustration.sprite = null; illustration.enabled = false; }
        if (classLabel != null) classLabel.text = string.Empty;
        if (nameText != null) nameText.text = string.Empty;
        if (hpText != null) hpText.text = string.Empty;
        if (skillText != null) skillText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (upgradeRoot != null) upgradeRoot.SetActive(false);
        if (upgradeText != null) upgradeText.text = string.Empty;
    }

    public void Bind(CardDataSO data, int deckIndex)
    {
        if (data == null) { Clear(); return; }

        if (illustration != null)
        {
            illustration.sprite = data.illustration;
            illustration.enabled = data.illustration != null;
        }
        if (classLabel != null) classLabel.text = LookupClassLabel(data.type);
        if (nameText != null) nameText.text = data.cardName;

        int hpBonus = RunState.GlobalHpBonus + RunState.GetPerCardHpBonus(deckIndex);
        int maxHp = Mathf.Max(1, data.baseHP + hpBonus);
        if (hpText != null)
            hpText.text = hpBonus > 0 ? $"HP {maxHp} (+{hpBonus})" : $"HP {maxHp}";

        int skillBonus = RunState.GetPerCardSkillBonus(deckIndex);
        if (skillText != null)
        {
            string bonus = skillBonus > 0 ? $" +{skillBonus}" : string.Empty;
            skillText.text = !string.IsNullOrEmpty(data.skillName) ? $"[{data.skillName}]{bonus}" : string.Empty;
        }

        if (descriptionText != null)
            descriptionText.text = data.skillDescription ?? string.Empty;

        bool hasAny = hpBonus > 0 || skillBonus > 0;
        if (upgradeRoot != null) upgradeRoot.SetActive(hasAny);
        if (upgradeText != null)
        {
            if (!hasAny) upgradeText.text = string.Empty;
            else if (hpBonus > 0 && skillBonus > 0) upgradeText.text = $"HP +{hpBonus} · Skill +{skillBonus}";
            else if (hpBonus > 0) upgradeText.text = $"HP +{hpBonus}";
            else upgradeText.text = $"Skill +{skillBonus}";
        }
    }

    private string LookupClassLabel(CardType type)
    {
        if (classLabels != null)
            foreach (var p in classLabels)
                if (p.type == type && !string.IsNullOrEmpty(p.label)) return p.label;
        return type.ToString();
    }
}
