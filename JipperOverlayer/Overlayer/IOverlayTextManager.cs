using UnityEngine;

namespace JipperOverlayer.Overlayer;

public interface IOverlayTextManager
{
    void SetBest(float best);
    void CacheProgress(scrPlanet planet);
    void SeedProgress(float progress);
    void UpdateAccuracy(Overlay overlay, int index);
    void UpdateProgress(Overlay overlay);
    void UpdateProgressBar(Overlay overlay);
    void UpdateCheckpoint(Overlay overlay);
    void UpdateBest(Overlay overlay);
    float GetProgress();

    /// <summary>标签编辑后清空各文本重建路径的"值未变则跳过"节流，强制下次刷新重绘。</summary>
    void DirtyTextCaches();

    // Extended-overlay helpers (coop-aware)
    void UpdateDeath(Overlay overlay);
    void UpdateState(Overlay overlay, bool isPurePerfect);

    /// <summary>状态文本（不含标签前缀与分隔符），供 {State} tag 复用同一套判定分支。
    /// color 为 UpdateState 原本要给文本设置的颜色。</summary>
    string GetStateString(Overlay overlay, bool isPurePerfect, out Color color);

    bool CheckPurePerfect(Overlay overlay);
    int GetTooJudgement(Overlay overlay);
}
