using System;
using UnityEngine;


namespace Tansu.Converters
{
    /// <summary>
    /// <see cref="Vector2Int"/>を<c>{"x":0,"y":0}</c>の形式で読み書きする。
    /// </summary>
    public sealed class Vector2IntConverter : ComponentStructConverter<Vector2Int>
    {
        private static readonly string[] ComponentNames = { "x", "y" };
        private static readonly double[] DefaultComponents = { 0, 0 };


        protected override bool WritesIntegers => true;

        
        public Vector2IntConverter() : base(ComponentNames, DefaultComponents)
        {
        }


        protected override void Decompose(Vector2Int value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
        }


        protected override Vector2Int Compose(ReadOnlySpan<double> components)
        {
            return new Vector2Int((int)components[0], (int)components[1]);
        }
    }
}
