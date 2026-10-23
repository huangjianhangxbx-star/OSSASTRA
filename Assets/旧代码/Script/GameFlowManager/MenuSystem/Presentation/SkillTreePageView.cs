using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillTreePageView : MonoBehaviour
{
    [SerializeField] private RectTransform connectionRoot;
    [SerializeField] private RectTransform nodeRoot;
    [SerializeField] private SkillTreeNodeView nodeTemplate;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button resetButton;

    private readonly List<SkillTreeNodeView> spawnedNodes =
        new List<SkillTreeNodeView>();
    private DRPGProgressionService progression;
    private DRPGCombatantProfileSO profile;
    private GameMenuTooltipView tooltip;
    private bool resetBound;

    public void Bind(
        DRPGProgressionService service,
        DRPGCombatantProfileSO targetProfile,
        GameMenuTooltipView tooltipView)
    {
        progression = service;
        profile = targetProfile;
        tooltip = tooltipView;
        if (!resetBound && resetButton != null)
        {
            resetButton.onClick.AddListener(ResetTree);
            resetBound = true;
        }
        Refresh();
    }

    public void Refresh()
    {
        ClearRuntimeViews();
        if (progression == null || profile == null)
            return;

        DRPGCharacterProgressionView view = progression.GetView(profile);
        pointsText?.SetText($"SKILL POINTS  {view.AvailablePoints}");
        DRPGSkillTreeSO tree = view.ActiveProfession != null
            ? view.ActiveProfession.SkillTree
            : null;
        if (tree == null || nodeTemplate == null || nodeRoot == null)
            return;

        IReadOnlyList<DRPGSkillTreeNodeDefinition> nodes = tree.Nodes;
        var byId = new Dictionary<string, DRPGSkillTreeNodeDefinition>(
            StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++)
            byId[nodes[i].NodeId] = nodes[i];
        for (int i = 0; i < nodes.Count; i++)
        {
            DRPGSkillTreeNodeDefinition node = nodes[i];
            IReadOnlyList<string> prerequisites = node.PrerequisiteNodeIds;
            for (int prerequisiteIndex = 0;
                 prerequisiteIndex < prerequisites.Count;
                 prerequisiteIndex++)
            {
                if (byId.TryGetValue(
                        prerequisites[prerequisiteIndex],
                        out DRPGSkillTreeNodeDefinition prerequisite))
                {
                    CreateConnection(
                        prerequisite.LayoutPosition,
                        node.LayoutPosition,
                        view.IsNodeUnlocked(prerequisite.NodeId) &&
                        view.IsNodeUnlocked(node.NodeId));
                }
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            DRPGSkillTreeNodeDefinition node = nodes[i];
            bool unlocked = view.IsNodeUnlocked(node.NodeId);
            bool prerequisitesMet = true;
            for (int p = 0; p < node.PrerequisiteNodeIds.Count; p++)
            {
                if (!view.IsNodeUnlocked(node.PrerequisiteNodeIds[p]))
                    prerequisitesMet = false;
            }
            bool available = !unlocked && prerequisitesMet &&
                view.AvailablePoints >= node.PointCost;

            SkillTreeNodeView instance = Instantiate(nodeTemplate, nodeRoot);
            instance.gameObject.SetActive(true);
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchoredPosition = node.LayoutPosition;
            instance.Bind(node, unlocked, available, ActivateNode, tooltip);
            spawnedNodes.Add(instance);
        }
    }

    private void ActivateNode(DRPGSkillTreeNodeDefinition node)
    {
        DRPGProgressionOperationResult result =
            progression.TryActivateNode(profile, node.NodeId);
        feedbackText?.SetText(result.Success ? "UNLOCKED" : result.Message);
        Refresh();
        tooltip?.ShowNode(node);
    }

    private void ResetTree()
    {
        DRPGProgressionOperationResult result = progression.ResetProfession(profile);
        feedbackText?.SetText(result.Success ? "TREE RESET" : result.Message);
        Refresh();
    }

    private void CreateConnection(Vector2 from, Vector2 to, bool unlocked)
    {
        if (connectionRoot == null)
            return;
        var lineObject = new GameObject("SkillPath", typeof(RectTransform), typeof(Image));
        RectTransform line = lineObject.GetComponent<RectTransform>();
        line.SetParent(connectionRoot, false);
        Vector2 delta = to - from;
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
        line.pivot = new Vector2(0f, 0.5f);
        line.anchoredPosition = from;
        line.sizeDelta = new Vector2(delta.magnitude, unlocked ? 7f : 3f);
        line.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        Image image = lineObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = unlocked
            ? new Color(1f, 0.08f, 0.42f, 0.92f)
            : new Color(0.28f, 0.07f, 0.15f, 0.62f);
    }

    private void ClearRuntimeViews()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            if (spawnedNodes[i] != null)
                Destroy(spawnedNodes[i].gameObject);
        }
        spawnedNodes.Clear();
        if (connectionRoot != null)
        {
            for (int i = connectionRoot.childCount - 1; i >= 0; i--)
                Destroy(connectionRoot.GetChild(i).gameObject);
        }
    }
}
