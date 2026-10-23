using System;
using UnityEngine;


namespace Tansu.Converters
{
    /// <summary>
    /// <see cref="Vector2"/>を<c>{"x":0,"y":0}</c>の形式で読み書きする。
    /// </summary>
    public sealed class Vector2Converter : ComponentStructConverter<Vector2>
    {
        private static readonly string[] ComponentNames = { "x", "y" };
        private static readonly double[] DefaultComponents = { 0, 0 };


        public Vector2Converter() : base(ComponentNames, DefaultComponents)
        {
        }


        protected override void Decompose(Vector2 value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
        }


        protected override Vector2 Compose(ReadOnlySpan<double> components)
        {
            return new Vector2((float)components[0], (float)components[1]);
        }
    }
}
