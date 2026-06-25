using UnityEngine;

[CreateAssetMenu(menuName = "BurgerMonster/CardData")]
public class CardDataSO : ScriptableObject
{
    public string cardName;
    public CardType type;
    public int baseHP;
    [Range(0, 5)] public int hpVariance = 0;
    public Sprite illustration;
    [TextArea] public string description;
    [Header("Skill (UI 표시용 메타)")]
    [Tooltip("스킬명. 예: \"도발\", \"일제사격\". 도감/덱뷰가 동일하게 사용.")]
    public string skillName;
    [Tooltip("스킬 설명문. 강화 보너스 수치는 UI 가 따로 표기 — 여기엔 base 수치/패시브 여부만 적는다.")]
    [TextArea(2, 5)] public string skillDescription;
}
