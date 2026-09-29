using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    public class LevelUpTestUI : MonoBehaviour
    {
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceNames;
        [SerializeField] private TMP_Text[] choiceDescs;

        public void OnClickLevelUpButton()
        {
            var choices = weaponController.GetRandomUpgradeChoices(choiceButtons.Length);

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (i < choices.Count)
                {
                    var choice = choices[i];
                    choiceNames[i].text = choice.IsNewWeapon
                        ? $"신규 획득: {choice.NewWeaponData.name}"
                        : $"{choice.Weapon.Data.name} - {choice.Option.name}";
                    choiceDescs[i].text = choice.IsNewWeapon
                        ? $"{choice.NewWeaponData.desc}"
                        : $"{choice.Option.desc}";
                    choiceButtons[i].gameObject.SetActive(true);

                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() =>
                    {
                        weaponController.ApplyUpgradeChoice(choice);
                        HideAllChoices();
                    });
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideAllChoices()
        {
            foreach (var button in choiceButtons)
            {
                button.gameObject.SetActive(false);
            }
        }
    }
}
