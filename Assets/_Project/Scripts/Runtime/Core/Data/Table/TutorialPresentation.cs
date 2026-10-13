using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>튜토리얼 한 단계의 표시·화면 연결 정보. 클라이언트 전용이라 서버 테이블(Tutorials)에 두지 않고 Id로 짝을 맞춘다.
    /// Script는 로비 가이드 단계 템플릿 이름이고, Lines는 같은 순번 단계에서 보여 주는 대사다.
    /// UnlocksTab은 이 튜토리얼이 시작되기 전까지 잠가 둘 하단 탭(예: skill)이다. 시작되면 해금 연출과 함께 열린다.
    /// Dialogue는 가이드·전투 시작 전에 재생하는 대사 장면이다</summary>
    [Serializable]
    public sealed class TutorialPresentation
    {
        public static readonly TutorialPresentation Empty = new();

        public string Id;
        public string Script;
        public string UnlocksTab;
        public List<string> Lines = new();
        public List<DialogueLine> Dialogue = new();
    }

    /// <summary>대사 한 줄. Side는 화자 초상이 서는 쪽 (left 기본, right)</summary>
    [Serializable]
    public sealed class DialogueLine
    {
        public string Speaker;
        public string Side;
        public string Text;

        public bool IsRight => Side == "right";
    }

    /// <summary>TutorialPresentation을 튜토리얼 Id로 찾는 표. 없는 Id는 빈 연출(대사·잠금 없음)을 돌려준다</summary>
    public sealed class TutorialPresentationTable
    {
        private readonly Dictionary<string, TutorialPresentation> _map;

        public TutorialPresentationTable(IEnumerable<TutorialPresentation> rows, IEnumerable<TutorialDefinition> tutorials)
        {
            _map = new Dictionary<string, TutorialPresentation>();
            var known = tutorials.ToDictionary(t => t.Id);
            foreach (var row in rows ?? Enumerable.Empty<TutorialPresentation>())
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Id) || !known.ContainsKey(row.Id) || !_map.TryAdd(row.Id, row))
                {
                    throw new InvalidOperationException($"TutorialPresentations {row?.Id}: Id가 비었거나 Tutorials에 없거나 중복됩니다");
                }

                if (row.Dialogue != null && row.Dialogue.Any(line => line == null || string.IsNullOrWhiteSpace(line.Speaker) || string.IsNullOrWhiteSpace(line.Text)))
                {
                    throw new InvalidOperationException($"TutorialPresentations {row.Id}: Dialogue의 Speaker와 Text는 비울 수 없습니다");
                }
            }

            foreach (var tutorial in known.Values.Where(t => t.IsGuide))
            {
                var row = For(tutorial.Id);
                if (string.IsNullOrWhiteSpace(row.Script) || row.Lines == null || row.Lines.Count == 0)
                {
                    throw new InvalidOperationException($"TutorialPresentations {tutorial.Id}: 가이드 튜토리얼은 Script와 Lines가 필요합니다");
                }
            }
        }

        public TutorialPresentation For(string tutorialId) =>
            _map.TryGetValue(tutorialId ?? "", out var row) ? row : TutorialPresentation.Empty;
    }
}
