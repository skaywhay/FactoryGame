# Окно настроек: редизайн — план работ

> **Для агентов-исполнителей:** REQUIRED SUB-SKILL: superpowers:executing-plans (в этой сессии выполняю я сам, по одному таску за раз, с проверкой после каждого).

**Goal:** привести модалку `SettingsModal` к дизайн-системе игры (вариант B: окно 560×520, секции-карточки, тонкая styled-полоса прокрутки, нормальный слайдер и переключатель), не меняя функций.

**Architecture:** весь UI строится кодом в `Hud` (`Assets/Scripts/Factory/Hud.cs`) — там же и остаётся; новых ассетов нет, спрайты либо из `Assets/Resources/UI/Sprites` через `LoadSprite`, либо процедурные через новый хелпер `SoftShape`. Никаких префабов/UIToolkit.

**Tech Stack:** Unity 6000.6.9f1, uGUI (com.unity.ugui), NUnit-тесты в `Assets/Tests`, редактор живёт на порту 17890 (AIUnityBridge).

**Spec:** `docs/superpowers/specs/2026-11-26-settings-modal-design.md`

## Global Constraints

- Функции не меняем: ключи PlayerPrefs (`MusicVolume`, `SfxVolume`, `ParticlesEnabled`, `GraphicsQuality`, `VSync`, `AutoPauseOnFocusLoss`, `ConfirmExit`, `GameSpeed`), значения по умолчанию и точки применения громкости — как есть.
- Состав строк и разделов не меняем: АУДИО (2 слайдера), ГРАФИКА (частицы, качество, VSync), ИГРА (автопауза, подтверждение выхода, скорость), ДРУГОЕ (сброс, версия).
- Палитра и спрайты только из существующих токенов `Hud`: `ColorSurface0/1/2`, `ColorBorder`, `TextWhite/TextMuted`, `AccentCyan/Amber/Green/Pink/Purple`, `_sprSidebarBg`, `_sprCardMachineBase`, `_sprAccentBar`, `_sprProgressTrack`, `_sprProgressFill`, `_sprTabActive`, `_sprBtnNeutral`, `_sprBtnRed`.
- Шрифт только `_font` (Bahnschrift), размеры 11/12/13/20 как в соседних модалках.
- Окно не должно иметь горизонтальной прокрутки и обрезанного текста.
- Проверка на чужом разрешении: reference 1440×900, окно фиксированного размера.

## Review Focus

- Полоса прокрутки перекрывает строки настроек → правый внутренний отступ контента 16 px и полоса в желобе, проверяется скриншотом с прокруткой.
- Контент влезает без прокрутки → handle полосы должен быть скрыт (`size >= 0.999`), иначе висит мусорная полоска.
- `SoftShape` без anti-aliasing даёт «лесенку»/жёсткие края → проверка угловых пикселей на прозрачность в EditMode-тесте.
- Текст обрезается при узком окне → все подписи в отдельных строках, ширина строки = ширина карточки − 24 px.
- Esc закрывает окно и снимает паузу, не ломая паузу, поставленную игроком до открытия (сохраняем текущее поведение: `OpenSettings` ставит паузу, `CloseSettings` снимает).

---

### Task 1: Процедурные «мягкие» спрайты + их тест

**Files:**
- Modify: `Assets/Scripts/Factory/Hud.cs` — рядом с `LoadSprite` (после строки 246) добавить хелпер.
- Create: `Assets/Tests/EditMode/HudSpriteTests.cs`
- Create: `Assets/Editor/RunTestsProbe.cs` (инструмент прогона тестов через мост, не продукт)

**Interfaces:**
- Produces: `internal static Sprite Hud.SoftShape(string name, int size, float radiusPx, Color fill, Color stroke, float strokePx)` — кэширует по `name`, строит `size×size` текстуру с anti-aliased скруглением `radiusPx`, обводкой `strokePx`, возвращает `Sprite` с border `(strokePx, strokePx, strokePx, strokePx)`. `radiusPx = size/2` и `strokePx = 0` → круг без обводки.
- Produces: `public static class RunTestsProbe` с `StartEditMode(string filter)` / `Poll()` — результат прогона пишется в `TestArtifacts/test-results-editmode.json` (используется всеми последующими тасками как проверка).

- [ ] **Step 1:** Написать падающий EditMode-тест: `SoftShape_Circle_CornersAreTransparent` (угловой пиксель альфа < 8), `SoftShape_Circle_CenterIsFill` (центральный пиксель = fill), `SoftShape_Cached_ReturnsSameSprite` (два вызова с одним именем → один и тот же объект). Тест вызывает `Hud.SoftShape` — значит хелпер должен быть `internal static` в `Game.Factory` (EditMode-асмбамble ссылается на `Game.Runtime`).
- [ ] **Step 2:** Прогнать тест и убедиться, что он падает (метода нет → компиляционная ошибка/`MissingMethodException`).
- [ ] **Step 3:** Реализовать `SoftShape`: `Texture2D(size, size, TextureFormat.RGBA32, false)`, на каждый пиксель — расстояние до центра с учётом радиуса + обводка, `SetPixels`/`Apply`, `Sprite.Create(..., new Vector4(stroke, stroke, stroke, stroke))`, кэш в `Dictionary<string, Sprite>`. Для точного круга `radiusPx = size * 0.5f`.
- [ ] **Step 4:** Добавить `RunTestsProbe.StartEditMode(filter)`/`Poll()` на `TestRunnerApi` (`UnityEditor.TestTools.TestRunner.Api`): `Execute(new ExecutionSettings(new EditModeTestListFilter...)` — по факту `ExecutionSettings` с фильтром по именам; пишет JSON `{state, passed, failed, failedTests[]}` в `TestArtifacts/test-results-editmode.json`.
- [ ] **Step 5:** `refresh` через мост → `ping` до `compiling:false`, ошибок компиляции нет; запустить тест, `Poll` → PASS (3/3).

### Task 2: Каркас окна — рамка, шапка, закрытие, желоб прокрутки

**Files:**
- Modify: `Assets/Scripts/Factory/Hud.cs` — `BuildSettings` (строки 894–982), заменить блок ScrollRect (922–973); поля `_settings`, `_settingsContent`, `_settingsScrollbar`; `OpenSettings`/`CloseSettings` (1381–1427), `Tick` (добавить обработку Esc).

**Interfaces:**
- Consumes: из Task 1 — ничего (только хелперы `NewRect`/`AddSlicedImage`/`SetRect`, уже в файле).
- Produces: `_settingsScrollbar` (`Scrollbar`) и `_settingsScrollRect` (`ScrollRect`) — использует Task 6; `ScrollbarVisibility()` — вызывается из `Tick`.

- [ ] **Step 1:** Окно 560×520, `frame_sidebar_bg`, центрировано (anchors 0.5/0.5). Внутренние отступы 24 px.
- [ ] **Step 2:** Шапка: слева «НАСТРОЙКИ» 20 bold cyan; справа кнопка «ЗАКРЫТЬ» 96×32 `_sprBtnNeutral` (как «ЗАКРЫТЬ» у ТАБЛИЦЫ); под шапкой разделитель 1 px `ColorBorder`.
- [ ] **Step 3:** `OpenSettings`/`CloseSettings` — без изменений поведения (пауза/снятие паузы), кнопка «ЗАКРЫТЬ» → `CloseSettings`.
- [ ] **Step 4:** В `Tick`: если `_settings.activeSelf` и нажат Escape → `CloseSettings` (проверка `Input.GetKeyDown(KeyCode.Escape)`).
- [ ] **Step 5:** Viewport прокрутки: правый внутренний отступ контента 16 px (полоса в желобе, не перекрывает строки), `RectMask2D` как сейчас.
- [ ] **Step 6:** Тонкая полоса справа: трек — плоский цвет трека прогресса с альфой 0.35, ширина 6; handle — `_sprAccentBar` sliced, cyan, альфа 0.55, мин. высота 36. Если `scrollRect.size` (вертикальный нормализованный размер) >= 0.999 → `handle.gameObject.SetActive(false)` (вычисляется в `Tick`).
- [ ] **Step 7:** Проверка: `refresh`, `ping` (компиляция чистая), Play Mode, «НАСТРОЙКИ» → скриншот: шапка, «ЗАКРЫТЬ», полоса в желобе, контент не заезжает под полосу.

### Task 3: Секции-карточки и строки настроек

**Files:**
- Modify: `Assets/Scripts/Factory/Hud.cs` — `CreateSettingSection` (1104–1154), `BuildAudioSection`/`BuildGraphicsSection`/`BuildGameplaySection`/`BuildOtherSection` (984–1102), `_musicSlider`/`_sfxSlider` остаются.

**Interfaces:**
- Consumes: `Hud.SoftShape` (Task 1) — для «утопленной» плашки строки (radius 6) и для круга бегунка (Task 4/5).
- Produces: `GameObject CreateSettingSection(Transform parent, string title, Color accent)` — карточка `card_machine_base` + полоска 4 px + заголовок 13 bold uppercase белый + разделитель; `RectTransform CreateInsetRow(Transform parent, string name, float height)` — «утопленная» строка (radius 6, цвет трека прогресса) + `LayoutElement.preferredHeight = height`.

- [ ] **Step 1:** Переписать `CreateSettingSection` под карточку: подложка `_sprCardMachineBase` (sliced), акцентная полоска 4 px слева (`_sprAccentBar`) цветом раздела (Аудио cyan, Графика purple, Игра amber, Другоее — `TextMuted`), заголовок 13 bold uppercase белый, разделитель 1 px альфа 8 %, внутренние отступы 12/12, расстояние между строками 8.
- [ ] **Step 2:** Перевести все четыре раздела на карточки и ряды: подпись 13 белая слева, значение 13 cyan bold справа, ряды — `CreateInsetRow` (слайдер 48, тумблер 36, сегменты 36, кнопка 40, версия 20).
- [ ] **Step 3:** Раздел ДРУГОЕ: «Сбросить настройки» → кнопка «СБРОСИТЬ» 140×32 `_sprBtnRed`; «Версия 1.0.0» серым 11 по центру раздела (как строка ряда).
- [ ] **Step 4:** Проверка: `refresh`/`ping`, Play Mode, открыть «НАСТРОЙКИ», скриншот — карточки, полоски, подписи, ничего не обрезано.

### Task 4: Слайдер громкости и скорости

**Files:**
- Modify: `Assets/Scripts/Factory/Hud.cs` — `CreateSliderSetting` (1156–1231).

**Interfaces:**
- Consumes: `SoftShape` (Task 1), `CreateInsetRow` (Task 3).
- Produces: `out Slider slider, out Text valueText` — сигнатуры не меняются, `_musicSlider`/`_musicVolumeText` и `RefreshSettingsSliders` продолжают работать без правок.

- [ ] **Step 1:** Ряд 48: строка подписи (слева «Громкость музыки», справа значение cyan bold) + трек 8 px из `_sprProgressTrack`, залив `_sprProgressFill` (как прогресс-бар плана в топбаре).
- [ ] **Step 2:** Бегунок — круг 18×18 через `SoftShape`: белая заливка + cyan обводка 2 px (вместо куска `btn_cta_amber` 20×24).
- [ ] **Step 3:** Логика без изменений: `minValue 0`, `maxValue 1`, значение из `PlayerPrefs`, `onValueChanged` → существующий колбэк (`OnMusicVolumeChanged`, lambda SFX, lambda скорости).
- [ ] **Step 4:** Проверка: подвигать оба слайдера, значения меняются, музыка/эффекты реагируют, после перезапуска сцены значения восстановились. Скриншот.

### Task 5: Переключатель (тумблер)

**Files:**
- Modify: `Assets/Scripts/Factory/Hud.cs` — `CreateToggleSetting` (1233–1283).

**Interfaces:**
- Consumes: `SoftShape` (Task 1), `CreateInsetRow` (Task 3).
- Produces: `Toggle` — сигнатура метода не меняется (колбэки и ключи PlayerPrefs те же).

- [ ] **Step 1:** Дорожка 46×24: «утопленный» фон + рамка 1 px `ColorBorder`; бегунок — круг 18 px (`SoftShape`), серый (`TextMuted`) = выкл, `AccentCyan` = вкл.
- [ ] **Step 2:** Бегунок ездит по `onValueChanged`: `anchoredPosition.x = isOn ? 22 : 2` (anchorMin/Max — правый край ряда), никакого `toggle.graphic` по всей дорожке.
- [ ] **Step 3:** Логика без изменений: `isOn` из `PlayerPrefs`, `onValueChanged` → существующий колбэк.
- [ ] **Step 4:** Проверка: щёлкнуть все четыре тумблера, состояние меняется и переживает перезапуск сцены. Скриншот.

### Task 6: «Качество» сегментами + финальная сборка и прогон

**Files:**
- Modify: `Assets/Scripts/Factory/Hud.cs` — `CreateDropdownSetting` (1285–1342).

**Interfaces:**
- Consumes: `CreateInsetRow` (Task 3), паттерн активной вкладки из `UpdateTabs`.

- [ ] **Step 1:** Ряд «Качество»: подпись слева фиксированной шириной 120 px, справа три кнопки 84×28 с шагом 6: неактивная — прозрачный фон + серый текст, активная — `_sprTabActive` + белый bold (как вкладки линий), вместо «активный = цвет текста».
- [ ] **Step 2:** Кнопка сброса в ДРУГОЕ — красная (`_sprBtnRed`) — если не сделано в Task 3.
- [ ] **Step 3:** Прогон EditMode-тестов через `RunTestsProbe` (`Poll` → все PASS, включая `HudSpriteTests`).
- [ ] **Step 4:** Play Mode: открыть «НАСТРОЙКИ» через `OpenSettingsProbe`, скриншот «до/после», прокрутка колесом и полоса, Esc, закрытие, рестарт сцены, ошибок в консоли нет.
- [ ] **Step 5:** Коммит: `Assets/Scripts/Factory/Hud.cs`, тесты, пробу, доку.
## Итог выполнения (факт)

- Task 1: Hud.SoftShape + 4 EditMode-теста (HudSpriteTests) + RunTestsProbe (коммит 9f50d61).
  Прогон: `RunTestsProbe.Start("")` -> 41 passed / 2 failed.
  Две упавшие — FactorySimTests.DayStart_PaysGeneratorUpkeep и Defect_LeftAloneEatsOneProduct:
  они падали и до правок (диff трогает только Hud.cs, эти тесты про экономику FactorySim).
- Task 2: рамка 560x520, шапка с «ЗАКРЫТЬ», закрытие по Esc, желоб 18 px и тонкая полоса 6 px
  (коммит 214af00).
- Task 3-6 одним коммитом d06becb (строки и виджеты неразделимы, промежуточное состояние ломало вёрстку):
  карточки разделов с акцентными полосками, «утопленные» строки, слайдер на тонком треке с круглым
  бегунком, тумблер дорожка+бегунок 16 px, «Качество» сегментами как вкладки линий,
  красная «СБРОСИТЬ», версия строкой.
- Отличия от плана: бегунок тумблера 16 px (не 18 — 18 выглядел болотом в 22 px дорожке);
  у «Скорости игры» добавлена только отображение значения (поведение не менялось);
  добавлен DebugSettingsScroll для проверки прокрутки без мыши (как остальные Debug-входы Hud).
- Проверено в редакторе: скриншоты TestArtifacts/screenshots/settings-*.png,
  состояния прокрутки — геометрией через мост (контент 758 px, окно 440 px, все 4 раздела на месте).
- Тестовые PlayerPrefs (GameSpeed=0, громкости=0) убраны из реестра, вернулись значения по умолчанию.