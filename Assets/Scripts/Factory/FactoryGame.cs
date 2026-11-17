using UnityEngine;

namespace Game.Factory
{
    public class FactoryGame : MonoBehaviour
    {
        public FactorySim Sim { get; private set; }

        HallRefs _hall;
        Hud _hud;
        float _probeTimer = 3f;
        bool _probeDone;

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (Sim == null) Sim = new FactorySim();
            if (_hall != null && _hud != null) return;

            if (_hall == null)
            {
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

        void Update()
        {
            if (Sim == null || _hall == null || _hud == null) EnsureInitialized();
            if (Sim == null) return;

            float dt = Time.deltaTime;
            Sim.Tick(dt);
            UpdateHall(dt);
            _hud.Tick();
            UpdateProbe(dt);
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
                            ? "МАТ " + line.Materials + " · ТОВ " + line.Products + " · $" + Sim.SalePrice(line)
                            : "НЕ ПОСТРОЕНА · $" + line.BuildCost;
                        view.Sign.Info.color = line.Built
                            ? new Color(0.90f, 0.93f, 0.97f)
                            : new Color(0.74f, 0.78f, 0.85f);
                    }

                    Face(view.Sign.Title);
                    Face(view.Sign.Info);
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
        }

        void Face(TextMesh label)
        {
            if (label == null || _hall.Camera == null) return;
            Vector3 d = label.transform.position - _hall.Camera.transform.position;
            label.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        void UpdateProbe(float dt)
        {
            _probeTimer -= dt;
            if (_probeTimer > 0f) return;
            _probeTimer = 2f;

            GameProbe.Metric("money", Sim.Money);
            GameProbe.Metric("day", Sim.Day);
            GameProbe.Metric("energyUsed", Sim.EnergyUsed);
            GameProbe.Metric("energyCapacity", Sim.EnergyCapacity);
            GameProbe.Metric("materials", Sim.TotalMaterials);
            GameProbe.Metric("products", Sim.TotalProducts);
            GameProbe.Metric("totalSold", Sim.TotalSold);

            if (_probeDone) return;
            _probeDone = true;

            GameProbe.Check("hud created", _hud != null);
            GameProbe.Check("energy within capacity", Sim.EnergyOk, Sim.EnergyUsed + " / " + Sim.EnergyCapacity);
            GameProbe.Check("three lines", Sim.Lines.Count == 3);
            GameProbe.Check("two generators", Sim.Generators.Count == 2);
            GameProbe.Check("gear line built", Sim.Lines[0].Built);
        }
    }
}
