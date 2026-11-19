using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Factory
{
    // Склейка трёх частей: симуляция (FactorySim), 3D-зал (HallBuilder) и HUD.
    // Логики тут почти нет — только вызвать нужный метод и обновить подписи.
    // Если хочется понять игру, начинать лучше с FactorySim.
    public class FactoryGame : MonoBehaviour
    {
        public FactorySim Sim { get; private set; }

        HallRefs _hall;
        Hud _hud;

        void Awake()
        {
            EnsureInitialized();
        }

        // При рестарте сцена перезагружается, но на всякий случай умеем
        // пересобрать зал, если он вдруг потерялся.
        void EnsureInitialized()
        {
            if (Sim == null) Sim = new FactorySim();
            if (_hall != null && _hud != null) return;

            if (_hall == null)
            {
                // Удаляем всё, что осталось от предыдущей сборки зала.
                for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
                _hall = HallBuilder.Build(transform);
            }

            if (_hud == null)
            {
                GameObject old = GameObject.Find("HUD");
                if (old != null) Destroy(old);
                _hud = new Hud(this);
            }
        }

        // Порядок как в симуляции: сначала считаем экономику, потом
        // разводим её по картинке и интерфейсу.
        void Update()
        {
            if (Sim == null || _hall == null || _hud == null) EnsureInitialized();
            if (Sim == null) return;

            // Пробел — быстрая пауза. В обучении игра тоже стоит,
            // иначе смена заканчивается, пока читаешь подсказки.
            if (Input.GetKeyDown(KeyCode.Space)) Sim.Paused = !Sim.Paused;

            bool paused = Sim.Paused || _hud.TutorialOpen;

            float dt = Time.deltaTime;
            if (!paused) Sim.Tick(dt);
            UpdateHall(paused ? 0f : dt);
            _hud.Tick();
            HandleClicks();
        }

        // Клик по красной бракованной детали в 3D-зале.
        // Кнопка в HUD делает то же самое — кому как удобнее.
        void HandleClicks()
        {
            if (Sim.Paused || _hud.TutorialOpen) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_hall == null || _hall.Camera == null) return;

            Ray ray = _hall.Camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 200f)) return;

            DefectMarker marker = hit.collider.GetComponentInParent<DefectMarker>();
            if (marker != null) Sim.FixDefect(marker.Line);
        }

        public void UpgradeMachine(int line, MachineKind kind)
        {
            UpgradeStatus status = Sim.TryUpgradeMachine(line, kind);
            if (status == UpgradeStatus.NotEnoughEnergy) Sim.SetEvent("Сначала нужно больше энергии", EventTone.Bad);
            else if (status == UpgradeStatus.NotEnoughMoney) Sim.SetEvent("Не хватает денег на улучшение", EventTone.Bad);
            else if (status == UpgradeStatus.NotBuilt) Sim.SetEvent("Линия ещё не построена", EventTone.Bad);
        }

        public void UpgradeGenerator(int index)
        {
            UpgradeStatus status = Sim.TryUpgradeGenerator(index);
            if (status == UpgradeStatus.NotEnoughMoney) Sim.SetEvent("Не хватает денег на улучшение генератора", EventTone.Bad);
        }

        public void BuildLine(int index)
        {
            UpgradeStatus status = Sim.TryBuildLine(index);
            if (status == UpgradeStatus.NotEnoughMoney) Sim.SetEvent("Не хватает денег на новую линию", EventTone.Bad);
            else if (status == UpgradeStatus.NotEnoughEnergy) Sim.SetEvent("Для линии не хватает энергии", EventTone.Bad);
        }

        // Кнопка на карточке сборки: если пост уже стоит — качаем, иначе строим.
        // Ошибки показываем баннером, чтобы игрок понял, чего не хватает.
        public void AssemblyAction(int index)
        {
            UpgradeStatus status = Sim.Assembly[index].Built
                ? Sim.TryUpgradeAssembly(index)
                : Sim.TryBuildAssembly(index);

            if (status == UpgradeStatus.NotEnoughMoney) Sim.SetEvent("Не хватает денег на сборку", EventTone.Bad);
            else if (status == UpgradeStatus.NotEnoughEnergy) Sim.SetEvent("Сначала нужно больше энергии", EventTone.Bad);
        }

        // debug: DebugAssembly(0..2) — строит/качает сборку без похода в UI
        public void DebugAssembly(int index)
        {
            AssemblyAction(index);
        }

        // Ниже идут отладочные входы: их дёргают тесты и я из консоли.
        // В обычной игре они не используются, но и не мешают.
        public void Restart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        public void DebugAddMoney(float amount)
        {
            Sim.Money += amount;
        }

        public void DebugEndShift()
        {
            if (Sim.ShiftActive) Sim.EndDay();
        }

        public void DebugTable()
        {
            if (_hud != null) _hud.ToggleTable();
        }

        public void DebugTutorial()
        {
            if (_hud != null) _hud.ToggleTutorial();
        }

        public void DebugSelect(int line)
        {
            if (_hud != null) _hud.Select(line);
        }

        public void DebugBuild(int line)
        {
            BuildLine(line);
        }

        public void DebugUpgradeGenerator(int index)
        {
            UpgradeGenerator(index);
        }

        public void DebugUpgrade(int line, int kind)
        {
            UpgradeMachine(line, (MachineKind)kind);
        }

        // Обновление зала. Тут только визуал: подписи, прогресс-бары и вращение.
        // Никакой экономики — она уже посчитана в Sim.Tick.
        void UpdateHall(float dt)
        {
            bool running = Sim.ShiftActive && Sim.EnergyOk;

            for (int l = 0; l < _hall.Lines.Length && l < Sim.Lines.Count; l++)
            {
                LineView view = _hall.Lines[l];
                LineState line = Sim.Lines[l];

                if (view.Root != null) view.Root.gameObject.SetActive(line.Built);
                if (view.Empty != null) view.Empty.gameObject.SetActive(!line.Built);

                if (view.Sign != null)
                {
                    if (view.Sign.Info != null)
                    {
                        view.Sign.Info.text = line.Built
                            ? "МАТ " + line.Materials + " · ТОВ " + line.Products + " · $" + Sim.SalePrice(line) +
                              "\nСДЕЛАНО " + line.Produced
                            : "НЕ ПОСТРОЕНА · $" + line.BuildCost;
                        view.Sign.Info.color = line.Built
                            ? new Color(0.90f, 0.93f, 0.97f)
                            : new Color(0.74f, 0.78f, 0.85f);
                    }

                    Face(view.Sign.Title);
                    Face(view.Sign.Info);
                }

                if (view.Defect != null)
                {
                    bool show = line.Built && line.Defect;
                    if (view.Defect.activeSelf != show) view.Defect.SetActive(show);

                    if (show)
                    {
                        // Крутим красный кубик, чтобы он цеплял взгляд.
                        view.Defect.transform.Rotate(0f, 90f * dt, 0f, Space.Self);

                        if (view.DefectLabel != null)
                        {
                            view.DefectLabel.text = "БРАК · " + Mathf.CeilToInt(line.DefectLeft) + "с";
                            Face(view.DefectLabel);
                        }
                    }
                }

                if (!line.Built) continue;

                for (int m = 0; m < 3; m++)
                {
                    Station station = view.Stations[m];
                    MachineState machine = line.Machine((MachineKind)m);
                    if (station == null || station.Root == null) continue;

                    if (station.Info != null)
                    {
                        string text;
                        Color color;
                        if (!Sim.EnergyOk)
                        {
                            text = "НЕТ ЭНЕРГИИ";
                            color = new Color(1f, 0.40f, 0.36f);
                        }
                        else if (!Sim.ShiftActive)
                        {
                            text = Sim.Skipped ? "СМЕНА ПРОПУЩЕНА" : "ПЕРЕРЫВ";
                            color = new Color(0.78f, 0.74f, 0.52f);
                        }
                        else
                        {
                            text = machine.Status;
                            color = new Color(0.82f, 0.86f, 0.92f);
                        }

                        station.Info.text = text;
                        station.Info.color = color;
                    }

                    if (station.ProgressFill != null)
                    {
                        Vector3 scale = station.ProgressFill.localScale;
                        scale.x = Mathf.Max(0.001f, running ? machine.CycleProgress : 0f);
                        station.ProgressFill.localScale = scale;
                    }

                    Face(station.Title);
                    Face(station.Info);

                    for (int s = 0; s < station.Spinners.Count; s++)
                    {
                        if (station.Spinners[s] == null) continue;
                        station.Spinners[s].Rotate(0f, (running ? 40f : 6f) * dt, 0f, Space.Self);
                    }
                }
            }

            for (int i = 0; i < _hall.Generators.Length && i < Sim.Generators.Count; i++)
            {
                Station station = _hall.Generators[i];
                GeneratorState generator = Sim.Generators[i];

                if (station.Info != null)
                {
                    station.Info.text = "Lv " + generator.Level + " · +" + generator.Capacity + " энергии";
                    station.Info.color = running ? new Color(0.82f, 0.86f, 0.92f) : new Color(0.78f, 0.74f, 0.52f);
                }

                Face(station.Title);
                Face(station.Info);

                float speed = running ? 85f : 10f;
                for (int s = 0; s < station.Spinners.Count; s++)
                {
                    if (station.Spinners[s] == null) continue;
                    float direction = (s % 2 == 0) ? 1f : -1f;
                    station.Spinners[s].Rotate(0f, direction * speed * dt, 0f, Space.Self);
                }
            }

            for (int i = 0; i < _hall.Assembly.Length && i < Sim.Assembly.Count; i++)
            {
                Station shop = _hall.Assembly[i];
                AssemblyLine assembly = Sim.Assembly[i];
                if (shop == null || shop.Root == null) continue;

                if (shop.Info != null)
                {
                    string text;
                    Color color;
                    if (!assembly.Built)
                    {
                        text = "НЕ ПОСТРОЕНА · $" + assembly.BuildCost;
                        color = new Color(0.74f, 0.78f, 0.85f);
                    }
                    else if (!Sim.EnergyOk)
                    {
                        text = "НЕТ ЭНЕРГИИ";
                        color = new Color(1f, 0.40f, 0.36f);
                    }
                    else if (!Sim.ShiftActive)
                    {
                        text = Sim.Skipped ? "СМЕНА ПРОПУЩЕНА" : "ПЕРЕРЫВ";
                        color = new Color(0.78f, 0.74f, 0.52f);
                    }
                    else
                    {
                        text = assembly.Status + " · " + assembly.Output + "/" + AssemblyLine.Cap +
                               " · ВСЕГО " + assembly.Produced;
                        color = new Color(0.82f, 0.86f, 0.92f);
                    }

                    shop.Info.text = text;
                    shop.Info.color = color;
                }

                if (shop.ProgressFill != null)
                {
                    Vector3 scale = shop.ProgressFill.localScale;
                    scale.x = Mathf.Max(0.001f, running ? assembly.CycleProgress : 0f);
                    shop.ProgressFill.localScale = scale;
                }

                Face(shop.Title);
                Face(shop.Info);

                for (int s = 0; s < shop.Spinners.Count; s++)
                {
                    if (shop.Spinners[s] == null) continue;
                    shop.Spinners[s].Rotate(0f, (running ? 40f : 6f) * dt, 0f, Space.Self);
                }
            }
        }

        // Подписи — это TextMesh в мировых координатах, поэтому их надо
        // поворачивать к камере вручную. Иначе при смене угла они «отваливаются».
        void Face(TextMesh label)
        {
            if (label == null || _hall.Camera == null) return;
            Vector3 d = label.transform.position - _hall.Camera.transform.position;
            label.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
