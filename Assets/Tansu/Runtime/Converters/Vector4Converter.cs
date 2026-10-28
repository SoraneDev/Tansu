using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Vector4" />を<c>{"x":0,"y":0,"z":0,"w":0}</c>の形式で読み書きする。
    /// </summary>
    internal sealed class Vector4Converter : ComponentStructConverter<Vector4>
    {
        private static readonly string[] s_componentNames = { "x", "y", "z", "w" };
        private static readonly double[] s_defaultComponents = { 0, 0, 0, 0 };


        /// <summary>
        /// <see cref="Vector4" />のコンバーターを作成する。
        /// </summary>
        public Vector4Converter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override void Decompose(Vector4 value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
            components[2] = value.z;
            components[3] = value.w;
        }


        /// <inheritdoc />
        protected override Vector4 Compose(ReadOnlySpan<double> components)
        {
            return new Vector4((float)components[0], (float)components[1], (float)components[2], (float)components[3]);
        }
    }
}
