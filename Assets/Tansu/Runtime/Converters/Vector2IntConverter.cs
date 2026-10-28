using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Vector2Int" />を<c>{"x":0,"y":0}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>小数や範囲外の値は、四捨五入してintの範囲に収める。</remarks>
    internal sealed class Vector2IntConverter : ComponentStructConverter<Vector2Int>
    {
        private static readonly string[] s_componentNames = { "x", "y" };
        private static readonly double[] s_defaultComponents = { 0, 0 };


        /// <summary>
        /// <see cref="Vector2Int" />のコンバーターを作成する。
        /// </summary>
        public Vector2IntConverter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override bool WritesIntegers => true;


        /// <inheritdoc />
        protected override void Decompose(Vector2Int value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
        }


        /// <inheritdoc />
        protected override Vector2Int Compose(ReadOnlySpan<double> components)
        {
            return new Vector2Int(ToInt(components[0]), ToInt(components[1]));
        }
    }
}
