using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Color32" />を<c>{"r":0,"g":0,"b":0,"a":255}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>
    /// アルファが欠けている場合に透明にならないよう、アルファの既定値を255（不透明）にしている。
    /// 範囲外の値や小数は、0から255の整数に丸める。
    /// </remarks>
    internal sealed class Color32Converter : ComponentStructConverter<Color32>
    {
        private static readonly string[] s_componentNames = { "r", "g", "b", "a" };
        private static readonly double[] s_defaultComponents = { 0, 0, 0, 255 };


        /// <summary>
        /// <see cref="Color32" />のコンバーターを作成する。
        /// </summary>
        public Color32Converter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override bool WritesIntegers => true;


        /// <inheritdoc />
        protected override void Decompose(Color32 value, Span<double> components)
        {
            components[0] = value.r;
            components[1] = value.g;
            components[2] = value.b;
            components[3] = value.a;
        }


        /// <inheritdoc />
        protected override Color32 Compose(ReadOnlySpan<double> components)
        {
            return new Color32(
                ToByte(components[0]),
                ToByte(components[1]),
                ToByte(components[2]),
                ToByte(components[3]));
        }


        private static byte ToByte(double component)
        {
            return (byte)Math.Clamp(Math.Round(component), byte.MinValue, byte.MaxValue);
        }
    }
}
