using System;
using UnityEngine;


namespace Tansu
{
    /// <summary>
    /// <see cref="Vector2" />を<c>{"x":0,"y":0}</c>の形式で読み書きする。
    /// </summary>
    internal sealed class Vector2Converter : ComponentStructConverter<Vector2>
    {
        private static readonly string[] s_componentNames = { "x", "y" };
        private static readonly double[] s_defaultComponents = { 0, 0 };


        /// <summary>
        /// <see cref="Vector2" />のコンバーターを作成する。
        /// </summary>
        public Vector2Converter() : base(s_componentNames, s_defaultComponents)
        {
        }


        /// <inheritdoc />
        protected override void Decompose(Vector2 value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
        }


        /// <inheritdoc />
        protected override Vector2 Compose(ReadOnlySpan<double> components)
        {
            return new Vector2((float)components[0], (float)components[1]);
        }
    }
}
