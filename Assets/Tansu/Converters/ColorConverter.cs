using System;
using UnityEngine;


namespace Tansu.Converters
{
    /// <summary>
    /// <see cref="Color"/>を<c>{"r":0,"g":0,"b":0,"a":1}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>
    /// アルファが欠けている場合に透明にならないよう、アルファの既定値を1（不透明）にしている。
    /// </remarks>
    public sealed class ColorConverter : ComponentStructConverter<Color>
    {
        private static readonly string[] ComponentNames = { "r", "g", "b", "a" };
        private static readonly double[] DefaultComponents = { 0, 0, 0, 1 };

        
        public ColorConverter() : base(ComponentNames, DefaultComponents)
        {
        }


        protected override void Decompose(Color value, Span<double> components)
        {
            components[0] = value.r;
            components[1] = value.g;
            components[2] = value.b;
            components[3] = value.a;
        }


        protected override Color Compose(ReadOnlySpan<double> components)
        {
            return new Color((float)components[0], (float)components[1], (float)components[2], (float)components[3]);
        }
    }
}
