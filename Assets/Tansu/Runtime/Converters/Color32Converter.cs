using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Color32" />を<c>{"r":0,"g":0,"b":0,"a":255}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>
    /// アルファが欠けている場合に透明にならないよう、アルファの既定値を255（不透明）にしている。
    /// 小数や範囲外の値は、四捨五入して0から255の範囲に収める。
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
            // 引数を指定しないMath.Roundは、.5を偶数側に丸める（2.5は2になる）ため、0から遠い側に丸めて四捨五入にする。
            double rounded = Math.Round(component, MidpointRounding.AwayFromZero);
            return (byte)Math.Clamp(rounded, byte.MinValue, byte.MaxValue);
        }
    }
}
