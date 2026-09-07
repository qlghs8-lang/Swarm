using Swarm.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class TitleManager : MonoBehaviour
    {
        private const string SelectedCharacterKey = "Swarm_SelectedCharacterId";

        [SerializeField] private Text goldText;
        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private ShopSlot slotPrefab;
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private string testStageSceneName = "TestStage";

        // 테스트 스테이지 버튼, 진행도 초기화 버튼처럼 개발 중에만 필요한 오브젝트들.
        // DevUi가 꺼져 있으면(기본값) 화면에서 사라지므로, 에디터 플레이 화면이 출시 화면과
        // 같아진다. Swarm ▸ 개발용 UI 표시로 다시 켤 수 있다.
        [SerializeField] private GameObject[] devOnlyObjects;

        private SlotListView _list;

        private void OnEnable()
        {
            // The shop panel spends gold through its own UI, so this screen's total would sit at
            // the pre-purchase value until the scene reloaded.
            GoldWallet.OnChanged += UpdateGoldText;
        }

        private void OnDisable()
        {
            GoldWallet.OnChanged -= UpdateGoldText;
        }

        private void UpdateGoldText()
        {
            if (goldText != null) goldText.text = $"보유 골드: {GoldWallet.Current}";
        }

        private void Awake()
        {
            if (DevUi.IsEnabled || devOnlyObjects == null) return;

            foreach (var target in devOnlyObjects)
            {
                if (target != null) target.SetActive(false);
            }
        }

        private void Start()
        {
            // 사실 보고일 뿐이다. 타이틀에서 무엇을 어떻게 들려줄지는 Wwise가 정한다.
            AudioDirector.SetGameState(GameAudioState.Title);

            if (string.IsNullOrEmpty(PlayerPrefs.GetString(SelectedCharacterKey, "")) && characters.Length > 0)
            {
                PlayerPrefs.SetString(SelectedCharacterKey, characters[0].Id);
                PlayerPrefs.Save();
            }

            RefreshUI();
        }

        public void StartGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        public void OpenTestStage()
        {
            SceneManager.LoadScene(testStageSceneName);
        }

        public void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            GoldWallet.Invalidate();

            if (characters.Length > 0)
            {
                PlayerPrefs.SetString(SelectedCharacterKey, characters[0].Id);
            }

            PlayerPrefs.Save();
            RefreshUI();
        }

        private void RefreshUI()
        {
            UpdateGoldText();

            _list ??= SlotListView.Attach(slotContainer, slotPrefab);

            string selectedId = PlayerPrefs.GetString(SelectedCharacterKey, "");

            _list.Rebuild(characters.Length, (i, slot) =>
            {
                CharacterDefinition character = characters[i];
                if (character == null) return;

                // Characters unlock in one purchase, so there is no level track to show.
                slot.HideProgress();

                if (!character.IsUnlocked)
                {
                    var affordable = GoldWallet.Current >= character.UnlockCost;
                    slot.Setup($"{character.DisplayName}\n해금 비용: {character.UnlockCost} G", "해금", affordable, () =>
                    {
                        character.TryUnlock();
                        RefreshUI();
                    });
                    slot.SetAffordable(affordable);
                    return;
                }

                slot.SetAffordable(true);

                if (character.Id == selectedId)
                {
                    slot.Setup(character.DisplayName, "선택됨", false, null);
                    slot.SetSelected(true);
                }
                else
                {
                    slot.Setup(character.DisplayName, "선택", true, () =>
                    {
                        PlayerPrefs.SetString(SelectedCharacterKey, character.Id);
                        PlayerPrefs.Save();
                        RefreshUI();
                    });
                }
            });
        }
    }
}
