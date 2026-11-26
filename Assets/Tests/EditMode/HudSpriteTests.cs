using Game.Factory;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Проверка процедурного генератора фигур для HUD: круг действительно круг,
    // скругление с антиалиасингом, кэш не плодит спрайты.
    public class HudSpriteTests
    {
        const int Size = 24;
        static readonly Color White = Color.white;
        static readonly Color Cyan = new Color(0f, 0.824f, 1f);

        [Test]
        public void SoftShape_Circle_CornersAreTransparent()
        {
            Texture2D tex = Hud.SoftShape("test_circle", Size, Size * 0.5f, White, Cyan, 2f).texture;

            Assert.Less(tex.GetPixel(0, 0).a, 0.05f, "Угол круга должен быть прозрачным");
            Assert.Less(tex.GetPixel(Size - 1, 0).a, 0.05f, "Угол круга должен быть прозрачным");
            Assert.Less(tex.GetPixel(Size - 1, Size - 1).a, 0.05f, "Угол круга должен быть прозрачным");
        }

        [Test]
        public void SoftShape_Circle_CenterIsFill()
        {
            Texture2D tex = Hud.SoftShape("test_circle", Size, Size * 0.5f, White, Cyan, 2f).texture;

            Color center = tex.GetPixel(Size / 2, Size / 2);
            Assert.Greater(center.a, 0.95f, "Центр круга должен быть непрозрачным");
            Assert.AreEqual(White.r, center.r, 0.01f, "В центре — цвет заливки, а не обводки");
        }

        [Test]
        public void SoftShape_Cached_ReturnsSameSprite()
        {
            Sprite a = Hud.SoftShape("test_cached", Size, Size * 0.5f, White, Cyan, 2f);
            Sprite b = Hud.SoftShape("test_cached", Size, Size * 0.5f, White, Cyan, 2f);

            Assert.AreSame(a, b, "Одно имя — один спрайт, иначе кэш не работает");
        }

        [Test]
        public void SoftShape_RoundedBox_EdgeIsFilled()
        {
            // Плашка с радиусом 6: середина верхней кромки уже внутри фигуры,
            // а самый угол по-прежнему прозрачен.
            Texture2D tex = Hud.SoftShape("test_box", 32, 6f, White, Cyan, 2f).texture;

            Assert.Greater(tex.GetPixel(16, 0).a, 0.95f, "Середина кромки должна быть закрашена");
            Assert.Less(tex.GetPixel(0, 0).a, 0.05f, "Угол скруглённой плашки должен быть прозрачным");
        }
    }
}