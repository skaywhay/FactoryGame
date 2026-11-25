using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Представление карточки станка или сборочного поста в сайдбаре.
    /// Поддерживает динамическое тинтование акцентной полоски, переключение состояний (Active, Locked, Disabled)
    /// и безопасную отвязку событий.
    /// </summary>
    public sealed class MachineCardItemView : MonoBehaviour
    {
        [Header("Typography")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _primarySpecText;
        [SerializeField] private TextMeshProUGUI _secondarySpecText;
        [SerializeField] private TextMeshProUGUI _actionButtonText;

        [Header("Visual Graphics")]
        [SerializeField] private Image _cardBackgroundImage;
        [SerializeField] private Image _accentMarkerImage;
        [SerializeField] private Image _actionButtonImage;
        [SerializeField] private Button _actionButton;

        [Header("Sprite Themes")]
        [SerializeField] private Sprite _btnActiveAmber;
        [SerializeField] private Sprite _btnActivePink;
        [SerializeField] private Sprite _btnDisabled;

        [Header("Accent Palettes")]
        [SerializeField] private Color _colorAccentAmber = new Color(1.0f, 0.702f, 0.0f, 1f);
        [SerializeField] private Color _colorAccentCyan = new Color(0.0f, 0.824f, 1.0f, 1f);
        [SerializeField] private Color _colorAccentGreen = new Color(0.18f, 0.835f, 0.451f, 1f);
        [SerializeField] private Color _colorAccentPink = new Color(0.949f, 0.580f, 0.722f, 1f);
        [SerializeField] private Color _colorAccentPurple = new Color(0.647f, 0.369f, 0.918f, 1f);

        private string _boundMachineId;
        private Action<string> _onActionClicked;

        private void Awake()
        {
            if (_actionButton != null)
            {
                _actionButton.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveListener(HandleClick);
            }
            _onActionClicked = null;
        }

        public void Bind(MachineCardData data, Action<string> onActionClicked)
        {
            _boundMachineId = data.Id;
            _onActionClicked = onActionClicked;

            if (_titleText != null) _titleText.text = data.Title.ToUpperInvariant();
            if (_levelText != null) _levelText.text = data.IsLocked ? "ЗАКРЫТО" : $"УР. {data.Level}";
            if (_primarySpecText != null) _primarySpecText.text = data.PrimarySpec;
            if (_secondarySpecText != null) _secondarySpecText.text = data.SecondarySpec;

            ApplyTheme(data.Theme);
            ApplyState(data);
        }

        private void ApplyTheme(CardAccentTheme theme)
        {
            if (_accentMarkerImage == null) return;

            _accentMarkerImage.color = theme switch
            {
                CardAccentTheme.Amber => _colorAccentAmber,
                CardAccentTheme.Cyan => _colorAccentCyan,
                CardAccentTheme.Green => _colorAccentGreen,
                CardAccentTheme.Pink => _colorAccentPink,
                CardAccentTheme.Purple => _colorAccentPurple,
                _ => Color.white
            };
        }

        private void ApplyState(MachineCardData data)
        {
            if (_actionButton == null) return;

            if (data.IsLocked)
            {
                _actionButton.interactable = false;
                if (_actionButtonImage != null && _btnDisabled != null) _actionButtonImage.sprite = _btnDisabled;
                if (_actionButtonText != null)
                {
                    _actionButtonText.text = $"Купить ${data.Cost}";
                    _actionButtonText.color = new Color(0.56f, 0.61f, 0.65f, 1f);
                }
                return;
            }

            if (!data.IsAffordable)
            {
                _actionButton.interactable = false;
                if (_actionButtonImage != null && _btnDisabled != null) _actionButtonImage.sprite = _btnDisabled;
                if (_actionButtonText != null)
                {
                    _actionButtonText.text = $"Нужно ${data.Cost}";
                    _actionButtonText.color = new Color(0.56f, 0.61f, 0.65f, 1f);
                }
                return;
            }

            _actionButton.interactable = true;
            if (_actionButtonImage != null)
            {
                _actionButtonImage.sprite = data.Theme == CardAccentTheme.Pink ? _btnActivePink : _btnActiveAmber;
            }

            if (_actionButtonText != null)
            {
                _actionButtonText.text = data.Kind == MachineCardKind.AssemblyUnit ? $"Построить ${data.Cost}" : $"Улучшить ${data.Cost}";
                _actionButtonText.color = Color.white;
            }
        }

        private void HandleClick()
        {
            if (!string.IsNullOrEmpty(_boundMachineId))
            {
                _onActionClicked?.Invoke(_boundMachineId);
            }
        }
    }
}
