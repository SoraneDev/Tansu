using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Vector3Int" />を<c>{"x":0,"y":0,"z":0}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>小数や範囲外の値は、四捨五入してintの範囲に収める。</remarks>
    internal sealed class Vector3IntConverter : ComponentStructConverter<Vector3Int>
    {
        private static readonly string[] s_componentNames = { "x", "y", "z" };
        private static readonly double[] s_defaultComponents = { 0, 0, 0 };


        /// <summary>
        /// <see cref="Vector3Int" />のコンバーターを作成する。
        /// </summary>
        public Vector3IntConverter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override bool WritesIntegers => true;


        /// <inheritdoc />
        protected override void Decompose(Vector3Int value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
            components[2] = value.z;
        }


        /// <inheritdoc />
        protected override Vector3Int Compose(ReadOnlySpan<double> components)
        {
            return new Vector3Int(ToInt(components[0]), ToInt(components[1]), ToInt(components[2]));
        }
    }
}
