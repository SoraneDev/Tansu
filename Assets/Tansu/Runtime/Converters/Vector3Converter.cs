using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Vector3" />を<c>{"x":0,"y":0,"z":0}</c>の形式で読み書きする。
    /// </summary>
    internal sealed class Vector3Converter : ComponentStructConverter<Vector3>
    {
        private static readonly string[] s_componentNames = { "x", "y", "z" };
        private static readonly double[] s_defaultComponents = { 0, 0, 0 };


        /// <summary>
        /// <see cref="Vector3" />のコンバーターを作成する。
        /// </summary>
        public Vector3Converter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override void Decompose(Vector3 value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
            components[2] = value.z;
        }


        /// <inheritdoc />
        protected override Vector3 Compose(ReadOnlySpan<double> components)
        {
            return new Vector3((float)components[0], (float)components[1], (float)components[2]);
        }
    }
}
