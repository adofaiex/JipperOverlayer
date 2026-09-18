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
    bool CheckPurePerfect(Overlay overlay);
    int GetTooJudgement(Overlay overlay);
}
