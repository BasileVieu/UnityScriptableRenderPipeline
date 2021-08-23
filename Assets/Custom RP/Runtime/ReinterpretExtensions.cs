﻿using System.Runtime.InteropServices;

public static class ReinterpretExtensions
{
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        [FieldOffset(0)] public int intValue;

        [FieldOffset(0)] public float floatValue;
    }
    
    public static float ReinterpretAsFloat(this int _value)
    {
        IntFloat converter = default;
        converter.intValue = _value;

        return converter.floatValue;
    }
}