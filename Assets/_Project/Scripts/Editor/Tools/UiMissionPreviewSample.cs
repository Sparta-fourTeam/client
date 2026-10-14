using System.Collections.Generic;
using Game.Core;
using Game.View;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Draws only the isolated UI Workspace mission clone through the public card API.</summary>
    internal static class UiMissionPreviewSample
    {
        internal static void Apply(GameObject root, string sample)
        {
            if (root.GetComponent<MissionScreenView>() == null) { return; }
            var card = root.GetComponentInChildren<StageCardView>(true);
            if (card == null) { return; }
            var profile = new PlayerProfile(new GameDataStore());
            int rating = sample == "보상 수령 가능" || sample == "수령 완료" ? 3 : 0;
            int claimed = sample == "수령 완료" ? 3 : 0;
            profile.Apply(new PlayerSnapshot
            {
                stageProgress = new List<StageProgressRow> { new() { stageId = 3, clearRating = rating, claimedRating = claimed } },
            });
            card.Show(3, profile, busy: false, claiming: false);
        }
    }
}
