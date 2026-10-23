using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    /// <summary>튜토리얼 한 단계의 표시·화면 연결 정보. 클라이언트 전용이라 서버 테이블(Tutorials)에 두지 않고 Id로 짝을 맞춘다.
    /// Script는 로비 가이드 단계 템플릿 이름이고, Lines는 같은 순번 단계에서 보여 주는 대사다.
    /// Dialogue는 가이드·전투 시작 전에 재생하는 대사 장면이다</summary>
    [Serializable]
    public sealed class TutorialPresentation
    {
        public static readonly TutorialPresentation Empty = new();

        public string Id;
        public string Script;
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

        /// <summary>로케일의 표시 문구로 Lines·Dialogue Text를 제자리에서 바꾼다(객체 참조 유지). 표에 없는 Id와 구조(Speaker·Side·개수)는 건드리지 않는다</summary>
        public void ApplyTexts(IEnumerable<TutorialPresentation> localized)
        {
            foreach (var source in localized ?? Enumerable.Empty<TutorialPresentation>())
            {
                if (source == null || !_map.TryGetValue(source.Id ?? "", out var row)) { continue; }
                for (int i = 0; i < row.Lines.Count && i < source.Lines.Count; i++) { row.Lines[i] = source.Lines[i]; }
                for (int i = 0; i < row.Dialogue.Count && i < source.Dialogue.Count; i++) { row.Dialogue[i].Text = source.Dialogue[i].Text; }
            }
        }

        /// <summary>로케일의 튜토리얼 연출 목록. ko 원본을 바탕으로 번역 파일(TutorialPresentations.&lt;코드&gt;.json)의 비어 있지 않은 Lines·Dialogue Text를 같은 순번에 덮어쓴다.
        /// 파일이나 항목이 없으면 ko 값을 쓴다</summary>
        public static List<TutorialPresentation> Load(string localeCode)
        {
            var asset = Resources.Load<TextAsset>("ClientData/TutorialPresentations")
                ?? throw new InvalidOperationException("Resources/ClientData/TutorialPresentations.json을 찾을 수 없습니다");
            var rows = JsonConvert.DeserializeObject<List<TutorialPresentation>>(asset.text);
            if (string.IsNullOrEmpty(localeCode) || localeCode == DisplayTexts.BaseLocale) { return rows; }

            var translated = Resources.Load<TextAsset>($"ClientData/TutorialPresentations.{localeCode}");
            var overlay = translated == null ? null : JsonConvert.DeserializeObject<List<TutorialPresentation>>(translated.text);
            foreach (var source in overlay ?? new List<TutorialPresentation>())
            {
                var row = source == null ? null : rows.Find(r => r.Id == source.Id);
                if (row == null) { continue; }
                for (int i = 0; i < row.Lines.Count && source.Lines != null && i < source.Lines.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(source.Lines[i])) { row.Lines[i] = source.Lines[i]; }
                }

                for (int i = 0; i < row.Dialogue.Count && source.Dialogue != null && i < source.Dialogue.Count; i++)
                {
                    if (source.Dialogue[i] != null && !string.IsNullOrWhiteSpace(source.Dialogue[i].Text)) { row.Dialogue[i].Text = source.Dialogue[i].Text; }
                }
            }

            return rows;
        }

        public TutorialPresentation For(string tutorialId) =>
            _map.TryGetValue(tutorialId ?? "", out var row) ? row : TutorialPresentation.Empty;
    }
}
