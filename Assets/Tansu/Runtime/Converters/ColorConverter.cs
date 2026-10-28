using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Color" />を<c>{"r":0,"g":0,"b":0,"a":1}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>アルファが欠けている場合に透明にならないよう、アルファの既定値を1（不透明）にしている。</remarks>
    internal sealed class ColorConverter : ComponentStructConverter<Color>
    {
        private static readonly string[] s_componentNames = { "r", "g", "b", "a" };
        private static readonly double[] s_defaultComponents = { 0, 0, 0, 1 };


        /// <summary>
        /// <see cref="Color" />のコンバーターを作成する。
        /// </summary>
        public ColorConverter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override void Decompose(Color value, Span<double> components)
        {
            components[0] = value.r;
            components[1] = value.g;
            components[2] = value.b;
            components[3] = value.a;
        }


        /// <inheritdoc />
        protected override Color Compose(ReadOnlySpan<double> components)
        {
            return new Color((float)components[0], (float)components[1], (float)components[2], (float)components[3]);
        }
    }
}
