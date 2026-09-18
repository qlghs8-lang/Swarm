using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Swarm.EditorTools
{
    /// <summary>
    /// Wwise가 만든 산출물을 Unity가 실제로 읽는 자리로 옮기고, 뱅크에서 이름 상수를 다시 뽑는다.
    ///
    /// 이 스크립트가 존재하는 이유는 하나의 함정 때문이다. Wwise는
    /// <c>Swarm_WwiseProject/GeneratedSoundBanks/</c>에 뱅크를 만들고, Unity 런타임은
    /// <c>Assets/StreamingAssets/Audio/GeneratedSoundBanks/&lt;플랫폼&gt;/</c>에서 읽는다.
    /// 둘을 잇는 복사는 <c>WwiseSettings.xml</c>의 <c>CopySoundBanksAsPreBuildStep</c>이 담당하는데
    /// 이름 그대로 <b>빌드 시점에만</b> 돈다. 에디터 Play 모드는 커버되지 않는다.
    ///
    /// 그 결과가 <c>Bank Load Failed Name: 1355168291</c>(= "Init"의 FNV 해시)이 프레임마다 쏟아지는
    /// 상황이다. 뱅크에는 아무 문제가 없고 복사가 안 됐을 뿐이라, 로그만 봐서는 원인이 보이지 않는다.
    /// 실제로 한 번 겪었다. 설계 문서 §3 참고.
    ///
    /// 사운드뱅크 생성 자체는 여기서 하지 않는다. Wwise Authoring이나 WAAPI의 몫이고, 이 메뉴는
    /// "이미 만들어진 것을 Unity에 반영한다"까지만 책임진다.
    /// </summary>
    public static class AudioPipeline
    {
        private const string SettingsPath = "Assets/WwiseSettings.xml";

        [MenuItem("Swarm/Audio/Rebuild %#a", priority = 0)]
        public static void Rebuild()
        {
            if (!Run(out var message))
            {
                EditorUtility.DisplayDialog("Audio Rebuild 실패", message, "확인");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Audio] Rebuild 완료 — {message}");
        }

        [MenuItem("Swarm/Audio/생성 위치 열기", priority = 20)]
        public static void RevealSoundBankFolder()
        {
            var root = ResolveSoundBankRoot();
            if (Directory.Exists(root)) EditorUtility.RevealInFinder(root);
            else Debug.LogError($"[Audio] 폴더가 없다: {root}");
        }

        /// <summary>복사 + 상수 재생성. 에디터 메뉴와 빌드 훅이 함께 쓴다.</summary>
        private static bool Run(out string message)
        {
            var source = ResolveSoundBankRoot();
            var destination = ResolveStreamingAssetsPath();

            if (!Directory.Exists(source))
            {
                message = $"사운드뱅크 폴더가 없다: {source}\n\n" +
                          "Wwise에서 SoundBank를 먼저 생성할 것.";
                return false;
            }

            int copied;
            try
            {
                copied = CopySoundBanks(source, destination);
            }
            catch (Exception e)
            {
                message = $"복사 실패: {e.Message}";
                return false;
            }

            if (copied == 0)
            {
                message = $"{source} 안에 복사할 뱅크가 없다.\n\nWwise에서 SoundBank를 먼저 생성할 것.";
                return false;
            }

            var regenerated = WwiseIdGenerator.Generate(destination);
            message = $"파일 {copied}개 복사, WwiseIds {(regenerated ? "갱신됨" : "변경 없음")}";
            return true;
        }

        /// <summary>
        /// 플랫폼 폴더별로 기존 산출물을 지우고 새로 복사한다. 지우는 이유는, 이름이 바뀌어 사라진
        /// 뱅크가 남아 있으면 게임이 옛 뱅크를 로드해 놓고 정상 동작하는 것처럼 보이기 때문이다.
        /// </summary>
        private static int CopySoundBanks(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            var copied = 0;

            // 루트의 ProjectInfo.json — 플랫폼 목록이 들어 있다.
            foreach (var file in Directory.GetFiles(source, "*.json"))
                copied += CopyOne(file, Path.Combine(destination, Path.GetFileName(file))) ? 1 : 0;

            foreach (var platformDir in Directory.GetDirectories(source))
            {
                var platform = Path.GetFileName(platformDir);
                var destDir = Path.Combine(destination, platform);
                Directory.CreateDirectory(destDir);

                foreach (var stale in Directory.GetFiles(destDir)
                             .Where(f => f.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    File.Delete(stale);
                    var meta = stale + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                }

                foreach (var file in Directory.GetFiles(platformDir)
                             .Where(f => f.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    copied += CopyOne(file, Path.Combine(destDir, Path.GetFileName(file))) ? 1 : 0;
                }
            }

            return copied;
        }

        private static bool CopyOne(string from, string to)
        {
            File.Copy(from, to, overwrite: true);
            return true;
        }

        /// <summary>
        /// <c>WwiseSettings.xml</c>의 <c>RootOutputPath</c>. 값이 없거나 파일을 못 읽으면 통합 기본값으로
        /// 되돌아간다 — 여기서 예외를 던지면 설정 파일 한 줄 때문에 빌드 전체가 멈춘다.
        /// </summary>
        private static string ResolveSoundBankRoot()
        {
            var configured = ReadSetting("RootOutputPath");
            if (string.IsNullOrEmpty(configured))
                configured = "../Swarm_WwiseProject/GeneratedSoundBanks/";
            // 설정의 상대 경로는 Assets 폴더 기준이다.
            return Path.GetFullPath(Path.Combine(Application.dataPath, Normalize(configured)));
        }

        private static string ResolveStreamingAssetsPath()
        {
            var configured = ReadSetting("WwiseStreamingAssetsPath");
            if (string.IsNullOrEmpty(configured))
                configured = "Audio/GeneratedSoundBanks";
            return Path.GetFullPath(Path.Combine(
                Application.dataPath, "StreamingAssets", Normalize(configured)));
        }

        private static string ReadSetting(string element)
        {
            try
            {
                var path = Path.GetFullPath(SettingsPath);
                if (!File.Exists(path)) return null;
                return XDocument.Load(path).Root?.Element(element)?.Value;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] {SettingsPath}에서 {element}를 읽지 못했다. 기본값을 쓴다: {e.Message}");
                return null;
            }
        }

        /// <summary>설정 파일은 Windows 구분자를 쓴다. macOS에서도 열 수 있게 통일한다.</summary>
        private static string Normalize(string path) =>
            path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        // ── WebGL: 뱅크를 빌드에서 빼낸다 ────────────────────────────────────────────────
        //
        // 웹 빌드는 무음이다. AK 런타임 어셈블리가 빌드에서 빠지고 AudioDirector가 no-op이 된다
        // (docs/webgl-build.md §2). 그런데 StreamingAssets는 플랫폼과 무관하게 통째로 빌드에
        // 실리므로, 그냥 두면 아무도 읽지 않을 Windows 뱅크 6MB가 웹 빌드에 따라 들어간다.
        // 폰트를 480KB로 서브셋한 빌드에 6MB를 얹는 건 말이 안 된다.
        //
        // 지우지 않고 치웠다 되돌리는 이유는 에디터다. StreamingAssets의 뱅크가 사라지면
        // Play 모드에서 Bank Load Failed가 프레임마다 쏟아지는데, 뱅크에는 아무 문제가 없고
        // 복사가 안 됐을 뿐이라 로그만 봐서는 원인이 보이지 않는다 — 이 클래스 주석이 말하는
        // 바로 그 실패다. 웹 빌드를 한 번 돌렸다는 이유로 그 상태에 빠지면 안 된다.

        private const string StashFolderName = "SwarmWebGLAudioStash";

        /// <summary>Assets 밖이면서 저장소에 들어가지 않는 자리. Library는 .gitignore 대상이다.</summary>
        private static string StashRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", StashFolderName));

        /// <summary>뱅크를 Assets 밖으로 옮긴다. 옮길 것이 있었으면 true.</summary>
        private static bool StashBanks()
        {
            var banks = ResolveStreamingAssetsPath();
            if (!Directory.Exists(banks)) return false;

            var stash = StashRoot;
            if (Directory.Exists(stash)) Directory.Delete(stash, true);

            Directory.Move(banks, stash);

            // .meta도 함께 옮긴다. 폴더만 사라지고 meta가 남으면 유니티가 다음 임포트에서
            // 고아 meta를 지우고, 되돌릴 때 GUID가 새로 발급된다.
            var meta = banks + ".meta";
            if (File.Exists(meta)) File.Move(meta, stash + ".meta");

            return true;
        }

        /// <summary>치워둔 뱅크를 제자리로. 되돌릴 것이 있었으면 true.</summary>
        private static bool RestoreBanks()
        {
            var stash = StashRoot;
            if (!Directory.Exists(stash)) return false;

            var banks = ResolveStreamingAssetsPath();
            if (Directory.Exists(banks)) Directory.Delete(banks, true);
            Directory.CreateDirectory(Path.GetDirectoryName(banks));

            Directory.Move(stash, banks);

            var stashMeta = stash + ".meta";
            if (File.Exists(stashMeta))
            {
                var meta = banks + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                File.Move(stashMeta, meta);
            }

            return true;
        }

        /// <summary>
        /// 빌드가 중간에 실패하거나 취소되면 후처리가 돌지 않아 뱅크가 Library에 남는다.
        /// 그 상태를 알아채지 못한 채 에디터를 쓰면 위에 적은 그 실패로 되돌아가므로,
        /// 에디터가 코드를 다시 로드할 때마다 남은 것이 있으면 되돌린다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RestoreStashOnLoad()
        {
            if (!Directory.Exists(StashRoot)) return;

            EditorApplication.delayCall += () =>
            {
                if (!RestoreBanks()) return;
                Debug.Log("[Audio] 중단된 WebGL 빌드가 남긴 사운드뱅크를 StreamingAssets로 되돌렸다.");
                AssetDatabase.Refresh();
            };
        }

        /// <summary>
        /// 사람이 메뉴 누르는 것을 잊어도 빌드는 잊지 않는다. Wwise 자체 pre-build 복사와 겹치지만,
        /// 이쪽은 상수 재생성까지 함께 하므로 뱅크와 코드가 어긋난 채로 빌드가 나가지 않는다.
        ///
        /// WebGL은 정반대로 움직인다 — 복사하는 대신 치운다. 위 주석 참고.
        /// </summary>
        private sealed class BuildHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport
        {
            public int callbackOrder => -100;

            public void OnPreprocessBuild(BuildReport report)
            {
                if (report.summary.platform == BuildTarget.WebGL)
                {
                    if (StashBanks())
                        Debug.Log("[Audio] WebGL 빌드 — 사운드뱅크를 빌드에서 제외한다(빌드 후 되돌림).");
                    return;
                }

                if (Run(out var message))
                {
                    Debug.Log($"[Audio] 빌드 전 정리 — {message}");
                    return;
                }

                throw new BuildFailedException(
                    $"[Audio] 사운드뱅크를 준비하지 못해 빌드를 중단한다.\n{message}");
            }

            public void OnPostprocessBuild(BuildReport report)
            {
                if (report.summary.platform != BuildTarget.WebGL) return;

                if (RestoreBanks())
                    Debug.Log("[Audio] 사운드뱅크를 StreamingAssets로 되돌렸다.");
            }
        }
    }
}
