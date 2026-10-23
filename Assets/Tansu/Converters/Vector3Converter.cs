using System;
using UnityEngine;


namespace Tansu.Converters
{
    /// <summary>
    /// <see cref="Vector3"/>を<c>{"x":0,"y":0,"z":0}</c>の形式で読み書きする。
    /// </summary>
    public sealed class Vector3Converter : ComponentStructConverter<Vector3>
    {
        private static readonly string[] ComponentNames = { "x", "y", "z" };
        private static readonly double[] DefaultComponents = { 0, 0, 0 };

        
        public Vector3Converter() : base(ComponentNames, DefaultComponents)
        {
        }


        protected override void Decompose(Vector3 value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
            components[2] = value.z;
        }


        protected override Vector3 Compose(ReadOnlySpan<double> components)
        {
            return new Vector3((float)components[0], (float)components[1], (float)components[2]);
        }
    }
}
