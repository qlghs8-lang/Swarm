using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Swarm.EditorTools
{
    /// <summary>
    /// 생성된 사운드뱅크의 메타데이터(<c>*.json</c>)를 읽어 <c>WwiseIds.generated.cs</c>를 만든다.
    ///
    /// 왜 필요한가: Wwise 이벤트를 문자열로 부르면 오타가 컴파일을 통과하고 런타임에 조용히 아무 일도
    /// 하지 않는다. 소리가 안 나는데 에러도 없는 것이 오디오 작업에서 가장 비싼 실패다. 뱅크가 실제로
    /// 담고 있는 이름을 상수로 굳혀 두면 오타가 컴파일 에러가 되고, Wwise에서 이름을 바꿨을 때도
    /// 재생성 시점에 빌드가 깨져 즉시 알게 된다. 설계 문서 §2 참고.
    ///
    /// 뱅크가 진실의 원천이다. Wwise 프로젝트 파일(.wwu)이 아니라 <b>생성된 뱅크</b>를 읽는 이유는,
    /// 런타임이 실제로 로드하는 것이 뱅크이기 때문이다. 저작 쪽에서 이름을 바꾸고 뱅크를 다시 만들지
    /// 않았다면 게임이 보는 진실은 여전히 옛 이름이고, 이 생성기는 그 사실을 그대로 반영해야 한다.
    /// </summary>
    public static class WwiseIdGenerator
    {
        private const string OutputPath = "Assets/_Project/Scripts/Audio/WwiseIds.generated.cs";

        /// <summary>
        /// <paramref name="soundBankRoot"/> 아래의 모든 플랫폼 폴더에서 <c>*.json</c>을 모아 상수 파일을 쓴다.
        /// 내용이 이전과 같으면 파일을 건드리지 않는다 — 무의미한 리임포트와 diff를 만들지 않기 위해서다.
        /// </summary>
        /// <returns>파일이 실제로 바뀌었으면 true.</returns>
        public static bool Generate(string soundBankRoot)
        {
            if (!Directory.Exists(soundBankRoot))
            {
                Debug.LogError($"[Audio] 사운드뱅크 폴더가 없다: {soundBankRoot}");
                return false;
            }

            var events = new SortedDictionary<string, uint>(StringComparer.Ordinal);
            var rtpcs = new SortedDictionary<string, uint>(StringComparer.Ordinal);
            var banks = new SortedSet<string>(StringComparer.Ordinal);
            var stateGroups = new SortedDictionary<string, StateGroupInfo>(StringComparer.Ordinal);

            var files = Directory.GetFiles(soundBankRoot, "*.json", SearchOption.AllDirectories);
            var parsed = 0;

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                // ProjectInfo / PlatformInfo / PluginInfo 는 뱅크 목록이 아니라 환경 정보다.
                if (name.EndsWith("Info.json", StringComparison.Ordinal)) continue;

                BankRoot root;
                try
                {
                    root = JsonUtility.FromJson<BankRoot>(File.ReadAllText(file));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Audio] {name} 파싱 실패, 건너뛴다: {e.Message}");
                    continue;
                }

                if (root?.SoundBanksInfo?.SoundBanks == null) continue;
                parsed++;

                foreach (var bank in root.SoundBanksInfo.SoundBanks)
                {
                    if (!string.IsNullOrEmpty(bank.ShortName)) banks.Add(bank.ShortName);
                    Collect(bank.Events, events);
                    Collect(bank.GameParameters, rtpcs);

                    if (bank.StateGroups == null) continue;
                    foreach (var group in bank.StateGroups)
                    {
                        if (string.IsNullOrEmpty(group.Name) || !TryId(group.Id, out var groupId)) continue;
                        if (!stateGroups.TryGetValue(group.Name, out var info))
                        {
                            info = new StateGroupInfo { Id = groupId };
                            stateGroups[group.Name] = info;
                        }
                        Collect(group.States, info.States);
                    }
                }
            }

            if (parsed == 0)
            {
                Debug.LogError($"[Audio] {soundBankRoot} 에서 읽을 뱅크 메타데이터를 찾지 못했다. " +
                               "Wwise에서 SoundBank를 먼저 생성했는지 확인할 것.");
                return false;
            }

            var text = Build(events, rtpcs, stateGroups, banks);
            var full = Path.GetFullPath(OutputPath);
            if (File.Exists(full) && File.ReadAllText(full) == text) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllText(full, text, new UTF8Encoding(false));
            Debug.Log($"[Audio] {OutputPath} 갱신 — 이벤트 {events.Count} / RTPC {rtpcs.Count} / " +
                      $"State 그룹 {stateGroups.Count} / 뱅크 {banks.Count}");
            return true;
        }

        private static void Collect(NamedId[] source, IDictionary<string, uint> into)
        {
            if (source == null) return;
            foreach (var item in source)
            {
                if (string.IsNullOrEmpty(item.Name) || !TryId(item.Id, out var id)) continue;
                into[item.Name] = id;
            }
        }

        // 뱅크 메타데이터의 Id는 uint 범위의 십진 문자열이다. JsonUtility가 uint를 직접 못 받아
        // string으로 읽은 뒤 여기서 변환한다.
        private static bool TryId(string raw, out uint id) => uint.TryParse(raw, out id);

        private static string Build(
            SortedDictionary<string, uint> events,
            SortedDictionary<string, uint> rtpcs,
            SortedDictionary<string, StateGroupInfo> stateGroups,
            SortedSet<string> banks)
        {
            var sb = new StringBuilder();
            sb.Append("// <auto-generated>\n");
            sb.Append("//     Swarm → Audio → Rebuild 가 만든 파일이다. 손으로 고치지 말 것 — 다음 재생성에 사라진다.\n");
            sb.Append("//     원본: Assets/StreamingAssets/Audio/GeneratedSoundBanks/<플랫폼>/*.json\n");
            sb.Append("//     생성기: Assets/_Project/Scripts/Editor/WwiseIdGenerator.cs\n");
            sb.Append("// </auto-generated>\n\n");
            sb.Append("namespace Swarm.Audio\n{\n");
            sb.Append("    /// <summary>\n");
            sb.Append("    /// 사운드뱅크가 실제로 담고 있는 이름과 ID. 게임 코드가 Wwise를 부를 때 쓰는 유일한 이름 출처다.\n");
            sb.Append("    /// 문자열로 부르면 오타가 런타임에 조용히 실패하므로 상수로 굳혀 컴파일 에러가 되게 한다.\n");
            sb.Append("    /// </summary>\n");
            sb.Append("    public static class WwiseIds\n    {\n");

            AppendConstBlock(sb, "Events", "이벤트.", events);
            sb.Append('\n');
            AppendConstBlock(sb, "Rtpc", "Game Parameter(RTPC).", rtpcs);

            sb.Append("\n        /// <summary>State 그룹과 그 아래 상태들.</summary>\n");
            sb.Append("        public static class States\n        {\n");
            var firstGroup = true;
            foreach (var pair in stateGroups)
            {
                if (!firstGroup) sb.Append('\n');
                firstGroup = false;
                sb.Append($"            public static class {Sanitize(pair.Key)}\n            {{\n");
                sb.Append($"                public const uint Group = {pair.Value.Id}u;\n");
                foreach (var state in pair.Value.States)
                    sb.Append($"                public const uint {Sanitize(state.Key)} = {state.Value}u;\n");
                sb.Append("            }\n");
            }
            sb.Append("        }\n");

            sb.Append("\n        /// <summary>사운드뱅크 이름. 로드는 이름으로 한다.</summary>\n");
            sb.Append("        public static class Banks\n        {\n");
            foreach (var bank in banks)
                sb.Append($"            public const string {Sanitize(bank)} = \"{bank}\";\n");
            sb.Append("        }\n");

            sb.Append("    }\n}\n");
            return sb.ToString();
        }

        private static void AppendConstBlock(
            StringBuilder sb, string className, string summary, SortedDictionary<string, uint> entries)
        {
            sb.Append($"        /// <summary>{summary}</summary>\n");
            sb.Append($"        public static class {className}\n        {{\n");
            foreach (var pair in entries)
                sb.Append($"            public const uint {Sanitize(pair.Key)} = {pair.Value}u;\n");
            sb.Append("        }\n");
        }

        /// <summary>Wwise 이름에는 C# 식별자로 못 쓰는 문자가 들어갈 수 있다.</summary>
        private static string Sanitize(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        private sealed class StateGroupInfo
        {
            public uint Id;
            public readonly SortedDictionary<string, uint> States = new(StringComparer.Ordinal);
        }

        // ── 뱅크 메타데이터 스키마 (JsonUtility가 읽는 만큼만) ────────────────────────────
        [Serializable] private sealed class BankRoot { public SoundBanksInfo SoundBanksInfo; }
        [Serializable] private sealed class SoundBanksInfo { public SoundBank[] SoundBanks; }

        [Serializable]
        private sealed class SoundBank
        {
            public string ShortName;
            public NamedId[] Events;
            public NamedId[] GameParameters;
            public StateGroup[] StateGroups;
        }

        [Serializable] private sealed class NamedId { public string Id; public string Name; }
        [Serializable] private sealed class StateGroup { public string Id; public string Name; public NamedId[] States; }
    }
}
