using System;

namespace Game.Factory
{
    // Сборка сложных товаров.
    //
    // Идея простая: обычные линии делают детали, а сборочный пост берёт эти детали
    // и варит из них что-то подороже. У рецепта ровно два входа (A и B) — мне хватило,
    // а массив ингредиентов ради двух штук городить было лень.
    //
    // id входов:
    //   0, 1, 2     — склады обычных линий (шестерёнки, корпуса, микросхемы)
    //   10, 11, 12  — склады сборочных постов (редукторы, приводы, турбины)
    // Из-за этих 10+ и получается цепочка: редуктор -> привод -> турбина.
    // Если добавляешь четвёртый рецепт — не забудь, что 12 уже занято турбиной.
    public class Recipe
    {
        public string Name;
        public int A;          // что берём первым
        public int AN;         // сколько штук
        public int B;          // что берём вторым
        public int BN;
        public int Price;      // базовая цена готовой штуки (до инфляции)
        public float Time;     // секунд на сборку на первом уровне
        public int BuildCost;  // сколько стоит поставить пост
        public int Energy = 3; // расход на первом уровне, дальше +1 за апгрейд
    }

    public static class Recipes
    {
        // Порядок важен: индекс рецепта = индекс поста в зале и в списке Sim.Assembly.
        // Числа подбирал вручную. Ориентир: сборка должна быть выгоднее, чем продать
        // те же детали по отдельности, но не настолько, чтобы линии стали не нужны.
        public static readonly Recipe[] All =
        {
            // шестерёнки + корпуса -> редуктор
            new Recipe { Name = "Редуктор", A = 0, AN = 2, B = 1, BN = 1, Price = 90, Time = 7f, BuildCost = 260 },

            // редуктор + микросхемы -> привод. Вот здесь и нужна цепочка.
            new Recipe { Name = "Привод", A = 10, AN = 1, B = 2, BN = 2, Price = 260, Time = 11f, BuildCost = 700 },

            // два привода + три корпуса -> турбина. Самая дорогая штука в игре.
            new Recipe { Name = "Турбина", A = 11, AN = 2, B = 1, BN = 3, Price = 700, Time = 16f, BuildCost = 1600 },
        };

        // TODO: названия деталей живут в двух местах — ProductInfo и тут.
        // Если появится четвёртая линия, придётся сводить в один справочник.
        public static string PartName(int id)
        {
            if (id < 10) return ProductInfo.ShortName((ProductKind)id);
            return All[id - 10].Name.ToUpper();
        }
    }

    public class AssemblyLine
    {
        public const int Cap = 10; // больше 10 готовых штук на посту не держим

        public readonly Recipe Recipe;
        public bool Built;
        public int Level = 1;
        public int Output;    // готовое, лежит на складе поста
        public int Produced;  // всего сделано за игру (для статистики)
        public float Timer;   // остаток текущей сборки
        public float SellTimer;
        public bool Busy;
        public string Status = "НЕ ПОСТРОЕНА";

        // Буфер на верстаке. Сюда по одной детали перетаскиваются со складов,
        // чтобы пост не выгребал всё под ноль и продавец не оставался без товара.
        // Hopper[0] — под вход A, Hopper[1] — под вход B.
        public readonly int[] Hopper = new int[2];

        public AssemblyLine(Recipe recipe)
        {
            Recipe = recipe;
        }

        public string Name { get { return Recipe.Name; } }
        public int BuildCost { get { return Recipe.BuildCost; } }

        // Скорость растёт так же, как у станков: +28% за уровень.
        // Число 0.28 взял из головы, но с ним темп игры ощущается нормально.
        public float Speed { get { return 1f + 0.28f * (Level - 1); } }
        public int EnergyDraw { get { return Recipe.Energy + (Level - 1); } }
        public int NextEnergyDraw { get { return EnergyDraw + 1; } }
        public float CraftTime { get { return Recipe.Time / Speed; } }
        public float SellTime { get { return 1.6f / Speed; } }

        // +4 за уровень, у обычных линий +3. Так вышло, когда крутил баланс:
        // сборка должна отбивать свою цену чуть быстрее.
        public int Price { get { return Recipe.Price + 4 * (Level - 1); } }

        // Дорожает в 1.45 раза за уровень, цена округляется до 5,
        // чтобы в интерфейсе не было цен вида $137.
        public int UpgradeCost
        {
            get { return (int)(Math.Round(120.0 * Math.Pow(1.45, Level - 1) / 5.0) * 5.0); }
        }

        // Прогресс-бар на 3D-посте. Если не занят — всегда 0, иначе
        // считаем, сколько осталось от текущего цикла.
        public float CycleProgress
        {
            get
            {
                if (!Busy) return 0f;
                return 1f - Math.Max(0f, Timer) / Math.Max(0.01f, CraftTime);
            }
        }
    }
}
