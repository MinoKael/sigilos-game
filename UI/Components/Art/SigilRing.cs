using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
    /// <summary>
    /// Círculo de conjuração baseado em um octógono.
    /// Os três primeiros slots ficam à direita e os três últimos à esquerda.
    /// Os vértices 0 (topo) e 4 (chão) ficam reservados para uso futuro.
    /// </summary>
    public partial class SigilRing : Control
    {
        private readonly List<Control> _around = new();
        private Control? _center;

        private static readonly int[] ActiveSlots =
        {
            1, 2, 3, // Direita: superior, central e inferior
            5, 6, 7  // Esquerda: inferior, central e superior
        };

        private static readonly (int From, int To)[] InternalLines =
        {
            (0, 3), (0, 5),
            (1, 4), (1, 6),
            (2, 5), (2, 7),
            (3, 6), (3, 0),
            (4, 7), (4, 1)
        };

        public SigilRing(float diameter)
        {
            CustomMinimumSize = new Vector2(diameter, diameter);
            MouseFilter = MouseFilterEnum.Ignore;
            Resized += Arrange;
        }

        /// <summary>
        /// Distância dos slots ao centro, proporcional ao raio.
        /// </summary>
        public float Spread { get; set; } = 0.78f;

        public void Set(Control? center, IReadOnlyList<Control> around)
        {
            foreach (var child in GetChildren())
                Layout.Discard(child);

            _center = center;

            if (_center != null)
                AddChild(_center);

            _around.Clear();

            foreach (var control in around)
            {
                _around.Add(control);
                AddChild(control);
            }

            Arrange();
        }

        public override void _Draw()
        {
            var center = Size / 2f;
            var radius = Mathf.Min(Size.X, Size.Y) / 2f - 6f;

            if (radius <= 0)
                return;

            //// Octógono externo.
            //var outerPoints = CreateOctagon(center, radius * 0.94f, Mathf.DegToRad(22.5f));
            //DrawColoredPolygon(outerPoints, new Color(Palette.Inset, 0.55f));
            //DrawPolygonOutline(outerPoints, new Color(0f, 0f, 0f, 0.5f), 5f);
            //DrawPolygonOutline(outerPoints, Palette.GoldDark, 2f);

            //// Octógono interno decorativo.
            //var innerPoints = CreateOctagon(center, radius * 0.80f, Mathf.DegToRad(22.5f));
            //DrawPolygonOutline(innerPoints, new Color(Palette.Gold, 0.35f), 1.2f);

            DrawCircle(center, radius, new Color(Palette.Inset, 0.55f));
            DrawArc(center, radius, 0, Mathf.Tau, 96, new Color(0, 0, 0, 0.5f), 8, true);
            DrawArc(center, radius, 0, Mathf.Tau, 96, Palette.GoldDark, 2, true);
            DrawArc(center, radius - 14, 0, Mathf.Tau, 96, new Color(Palette.Gold, 0.45f), 1.5f, true);
            DrawArc(center, radius * 0.39f, 0, Mathf.Tau, 64, new Color(Palette.Gold, 0.25f), 1, true);
            for (var k = 0; k < 36; k++)
            {
                var angle = k * Mathf.Tau / 36;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var length = k % 3 == 0 ? 10 : 5;
                DrawLine(center + dir * (radius - 13), center + dir * (radius - 13 + length), new Color(Palette.Gold, 0.5f), 1.2f, true);
            }

            // Pontos geométricos correspondentes aos oito vértices.
            var points = CreateOctagon(center, radius * Spread, Mathf.DegToRad(22.5f));

            // Linhas internas do símbolo alquímico. Creation Symbol
            foreach (var (From, To) in InternalLines)
            {
                DrawLine(points[From], points[To], new Color(Palette.Gold, 0.6f), antialiased: true);
            }

            // Circulo do centro
            DrawCircle(center, 10f, new Color(Palette.Gold, 0.45f), antialiased: true);

            // Ponto entre 0 e 5 (50% do caminho)
            var meio0e5 = points[0].Lerp(points[5], 0.29f);
            DrawCircle(meio0e5, 3f, new Color(Palette.Gold, 1f), antialiased: true);

            // Ponto entre 1 e 4 (50% do caminho)
            var meio1e4 = points[1].Lerp(points[4], 0.71f);
            DrawCircle(meio1e4, 3f, new Color(Palette.Gold, 1f), antialiased: true);
        }

        private static Vector2[] CreateOctagon(
            Vector2 center,
            float radius,
            float rotation = 0f) // Adicionado parâmetro de rotação
        {
            var points = new Vector2[8];

            for (var i = 0; i < 8; i++)
            {
                // Adiciona a rotação ao cálculo do ângulo de cada vértice
                var angle = -Mathf.Pi / 2f + (i * Mathf.Tau / 8f) + rotation;

                points[i] = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) * radius;
            }

            return points;
        }

        private void DrawPolygonOutline(
            Vector2[] points,
            Color color,
            float width)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var next = (i + 1) % points.Length;

                DrawLine(
                    points[i],
                    points[next],
                    color,
                    width,
                    true
                );
            }
        }

        private void Arrange()
        {
            var center = Size / 2f;
            var radius = Mathf.Min(Size.X, Size.Y) / 1.6f;

            if (_center != null)
            {
                _center.Size = _center.CustomMinimumSize;
                _center.Position = center - _center.Size / 2f;
            }

            var controlCount = Mathf.Min(
                _around.Count,
                ActiveSlots.Length
            );

            // Os três primeiros controles ficam à direita.
            // Os três seguintes ficam à esquerda.
            for (var i = 0; i < controlCount; i++)
            {
                var control = _around[i];
                var slotIndex = ActiveSlots[i];

                control.Size = control.CustomMinimumSize;

                var slotPosition = CreateSlotPosition(
                    slotIndex,
                    center,
                    radius * Spread
                );

                control.Position = slotPosition - control.Size / 2f;
            }

            QueueRedraw();
        }

        private static Vector2 CreateSlotPosition(
            int index,
            Vector2 center,
            float radius)
        {
            var angle = -Mathf.Pi / 2f + index * Mathf.Tau / 8f;

            return center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius;
        }
    }
}