using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 창 오른쪽 스탯 패널. PlayerStats의 스탯 목록대로 줄을 자동 생성합니다.
// 스탯 종류를 늘리려면 Player의 PlayerStats > Stats 목록에 항목만 추가하면 됩니다.
[DisallowMultipleComponent]
public class StatPanelUI : MonoBehaviour
{
    [SerializeField] private StatRowUI rowTemplate;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private Text pointsText;

    private readonly List<StatRowUI> rows = new List<StatRowUI>();
    private PlayerStats stats;

    private void OnEnable()
    {
        if (stats == null)
            Build();
        if (stats != null)
            stats.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.Changed -= Refresh;
    }

    private void Build()
    {
        stats = PlayerStats.Ensure();
        if (rowTemplate != null)
            rowTemplate.gameObject.SetActive(false);
        if (stats == null || rowTemplate == null || rowContainer == null)
            return;

        for (int i = 0; i < stats.Count; i++)
        {
            StatRowUI row = Instantiate(rowTemplate, rowContainer);
            row.name = $"StatRow_{stats.GetDefinition(i).id}";
            row.gameObject.SetActive(true);
            row.Bind(stats, i);
            rows.Add(row);
        }
    }

    private void Update()
    {
        // 레벨업으로 포인트가 늘어난 경우도 반영합니다. (창이 열려 있을 때만 실행)
        Refresh();
    }

    private void Refresh()
    {
        if (pointsText != null)
            pointsText.text = stats != null ? $"POINTS  {stats.AvailablePoints}" : "POINTS  -";
        foreach (StatRowUI row in rows)
            row.Refresh();
    }
}
