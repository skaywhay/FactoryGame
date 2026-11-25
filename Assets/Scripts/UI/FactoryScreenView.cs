using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Главное визуальное представление экрана производственного цеха (uGUI).
    /// Отвечает за сериализацию компонентов, обновление полей данных, анимации выезда
    /// и переключение сегмент-контроллеров вкладок.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FactoryScreenView : MonoBehaviour
    {
        [Header("Canvas & Group")]
        [SerializeField] private CanvasGroup _mainCanvasGroup;

        [Header("TopBar Binding")]
        [SerializeField] private TextMeshProUGUI _balanceText;
        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _planLabelText;
        [SerializeField] private Slider _planProgressSlider;
        [SerializeField] private TextMeshProUGUI _shiftTimerText;
        [SerializeField] private TextMeshProUGUI _energyText;
        [SerializeField] private Image _energyStatusDot;
        [SerializeField] private TextMeshProUGUI _warehouseText;
        [SerializeField] private TextMeshProUGUI _fuelText;

        [Header("TopBar Buttons")]
        [SerializeField] private Button _tutorialButton;
        [SerializeField] private Button _tableButton;
        [SerializeField] private Button _restartButton;

        [Header("Banner Binding")]
        [SerializeField] private TextMeshProUGUI _dailyPlanBannerText;
        [SerializeField] private TextMeshProUGUI _adviceTitleText;
        [SerializeField] private TextMeshProUGUI _adviceBodyText;

        [Header("Sidebar Controls")]
        [SerializeField] private RectTransform _sidebarTransform;
        [SerializeField] private Button _collapseButton;
        [SerializeField] private RectTransform _collapseArrowTransform;
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private Image[] _tabBackgroundImages;
        [SerializeField] private Sprite _tabActiveSprite;

        [Header("Cards Dynamic Pool")]
        [SerializeField] private RectTransform _cardsContentRoot;
        [SerializeField] private MachineCardItemView _cardPrefab;

        private readonly List<MachineCardItemView> _cardPool = new(16);
        private bool _isSidebarCollapsed;
        private Coroutine _sidebarAnimationRoutine;
        private Coroutine _fadeAnimationRoutine;

        public event Action OnTutorialClicked;
        public event Action OnTableClicked;
        public event Action OnRestartClicked;
        public event Action<int> OnTabSelected;
        public event Action<string> OnCardActionTriggered;

        private const float SidebarWidth = 340f;
        private const float AnimationDuration = 0.35f;

        private void Awake()
        {
            if (_mainCanvasGroup == null)
            {
                _mainCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (_tutorialButton != null) _tutorialButton.onClick.AddListener(() => OnTutorialClicked?.Invoke());
            if (_tableButton != null) _tableButton.onClick.AddListener(() => OnTableClicked?.Invoke());
            if (_restartButton != null) _restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            if (_collapseButton != null) _collapseButton.onClick.AddListener(ToggleSidebar);

            if (_tabButtons != null)
            {
                for (var i = 0; i < _tabButtons.Length; i++)
                {
                    var tabIndex = i;
                    _tabButtons[i].onClick.AddListener(() => HandleTabClicked(tabIndex));
                }
            }
        }

        private void OnDestroy()
        {
            if (_sidebarAnimationRoutine != null) StopCoroutine(_sidebarAnimationRoutine);
            if (_fadeAnimationRoutine != null) StopCoroutine(_fadeAnimationRoutine);

            if (_tutorialButton != null) _tutorialButton.onClick.RemoveAllListeners();
            if (_tableButton != null) _tableButton.onClick.RemoveAllListeners();
            if (_restartButton != null) _restartButton.onClick.RemoveAllListeners();
            if (_collapseButton != null) _collapseButton.onClick.RemoveAllListeners();

            if (_tabButtons != null)
            {
                foreach (var tab in _tabButtons)
                {
                    if (tab != null) tab.onClick.RemoveAllListeners();
                }
            }
        }

        public void PlayEntranceAnimation(Action onComplete = null)
        {
            if (_fadeAnimationRoutine != null) StopCoroutine(_fadeAnimationRoutine);
            _fadeAnimationRoutine = StartCoroutine(AnimateEntranceRoutine(onComplete));
        }

        private IEnumerator AnimateEntranceRoutine(Action onComplete)
        {
            _mainCanvasGroup.alpha = 0f;
            _mainCanvasGroup.interactable = false;
            _mainCanvasGroup.blocksRaycasts = false;

            if (_sidebarTransform != null)
            {
                _sidebarTransform.anchoredPosition = new Vector2(SidebarWidth, _sidebarTransform.anchoredPosition.y);
            }

            float elapsed = 0f;
            while (elapsed < AnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / AnimationDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                _mainCanvasGroup.alpha = ease;
                if (_sidebarTransform != null)
                {
                    _sidebarTransform.anchoredPosition = new Vector2(Mathf.Lerp(SidebarWidth, 0f, ease), _sidebarTransform.anchoredPosition.y);
                }

                yield return null;
            }

            _mainCanvasGroup.alpha = 1f;
            if (_sidebarTransform != null)
            {
                _sidebarTransform.anchoredPosition = new Vector2(0f, _sidebarTransform.anchoredPosition.y);
            }

            _mainCanvasGroup.interactable = true;
            _mainCanvasGroup.blocksRaycasts = true;
            onComplete?.Invoke();
        }

        public void PlayExitAnimation(Action onComplete = null)
        {
            if (_fadeAnimationRoutine != null) StopCoroutine(_fadeAnimationRoutine);
            _fadeAnimationRoutine = StartCoroutine(AnimateExitRoutine(onComplete));
        }

        private IEnumerator AnimateExitRoutine(Action onComplete)
        {
            _mainCanvasGroup.interactable = false;
            _mainCanvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            float duration = 0.25f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                _mainCanvasGroup.alpha = 1f - ease;
                if (_sidebarTransform != null)
                {
                    _sidebarTransform.anchoredPosition = new Vector2(Mathf.Lerp(0f, SidebarWidth, ease), _sidebarTransform.anchoredPosition.y);
                }

                yield return null;
            }

            _mainCanvasGroup.alpha = 0f;
            onComplete?.Invoke();
        }

        public void ToggleSidebar()
        {
            _isSidebarCollapsed = !_isSidebarCollapsed;
            if (_sidebarAnimationRoutine != null) StopCoroutine(_sidebarAnimationRoutine);
            _sidebarAnimationRoutine = StartCoroutine(AnimateSidebarRoutine(_isSidebarCollapsed));
        }

        private IEnumerator AnimateSidebarRoutine(bool collapsed)
        {
            if (_sidebarTransform == null) yield break;

            float startX = _sidebarTransform.anchoredPosition.x;
            float targetX = collapsed ? SidebarWidth : 0f;

            float startAngle = _collapseArrowTransform != null ? _collapseArrowTransform.localEulerAngles.z : 0f;
            float targetAngle = collapsed ? 180f : 0f;

            float elapsed = 0f;
            while (elapsed < AnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / AnimationDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                _sidebarTransform.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, ease), _sidebarTransform.anchoredPosition.y);
                if (_collapseArrowTransform != null)
                {
                    float angle = Mathf.LerpAngle(startAngle, targetAngle, ease);
                    _collapseArrowTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                }

                yield return null;
            }

            _sidebarTransform.anchoredPosition = new Vector2(targetX, _sidebarTransform.anchoredPosition.y);
            if (_collapseArrowTransform != null)
            {
                _collapseArrowTransform.localEulerAngles = new Vector3(0f, 0f, targetAngle);
            }
        }

        public void UpdateTopBar(TopBarData data)
        {
            if (_balanceText != null) _balanceText.text = $"$ {data.Balance:N0}";
            if (_dayText != null) _dayText.text = $"ДЕНЬ {data.Day}";
            if (_planLabelText != null) _planLabelText.text = $"ПЛАН {data.PlanCurrent}/{data.PlanTotal}";
            if (_planProgressSlider != null)
            {
                _planProgressSlider.value = data.PlanTotal > 0 ? (float)data.PlanCurrent / data.PlanTotal : 0f;
            }
            if (_shiftTimerText != null) _shiftTimerText.text = $"СМЕНА {data.ShiftSecondsRemaining:F1}с";
            if (_energyText != null) _energyText.text = $"ЭНЕРГИЯ {data.EnergyUsed} / {data.EnergyMax}";
            if (_warehouseText != null)
            {
                _warehouseText.text = $"МАТ {data.WarehouseMaterials}/{data.WarehouseMax} • ТОВ {data.WarehouseGoods}/{data.WarehouseMax}";
            }
            if (_fuelText != null) _fuelText.text = $"ТОПЛИВО ${data.FuelDailyCost}/день";
        }

        public void SetBannerInformation(string dailyPlanSummary, string directorAdviceTitle, string directorAdviceBody)
        {
            if (_dailyPlanBannerText != null) _dailyPlanBannerText.text = dailyPlanSummary;
            if (_adviceTitleText != null) _adviceTitleText.text = directorAdviceTitle;
            if (_adviceBodyText != null) _adviceBodyText.text = directorAdviceBody;
        }

        public void SelectTabVisual(int activeIndex)
        {
            if (_tabBackgroundImages == null) return;

            for (var i = 0; i < _tabBackgroundImages.Length; i++)
            {
                if (_tabBackgroundImages[i] == null) continue;
                var isActive = (i == activeIndex);
                _tabBackgroundImages[i].sprite = isActive ? _tabActiveSprite : null;
                _tabBackgroundImages[i].color = isActive ? Color.white : new Color(0, 0, 0, 0);
            }
        }

        public void RenderMachineCards(IReadOnlyList<MachineCardData> cards)
        {
            EnsurePoolSize(cards.Count);

            for (var i = 0; i < _cardPool.Count; i++)
            {
                if (i < cards.Count)
                {
                    _cardPool[i].gameObject.SetActive(true);
                    _cardPool[i].Bind(cards[i], ForwardCardAction);
                }
                else
                {
                    _cardPool[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsurePoolSize(int requiredCount)
        {
            if (_cardPrefab == null || _cardsContentRoot == null) return;

            while (_cardPool.Count < requiredCount)
            {
                var item = Instantiate(_cardPrefab, _cardsContentRoot);
                _cardPool.Add(item);
            }
        }

        private void ForwardCardAction(string cardId)
        {
            OnCardActionTriggered?.Invoke(cardId);
        }

        private void HandleTabClicked(int index)
        {
            SelectTabVisual(index);
            OnTabSelected?.Invoke(index);
        }
    }
}
