using System;

namespace RainMeadow.Exceptions;

public class NonExhaustiveException(object missingItem)
    : Exception($"Code is not exhaustive. Missing: {missingItem.GetType()}");
