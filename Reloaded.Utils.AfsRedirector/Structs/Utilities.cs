namespace Reloaded.Utils.AfsRedirector.Structs;

internal static class Utilities
{
    /// <summary>
    /// Rounds up a specified number to the next multiple of X.
    /// </summary>
    /// <param name="number">The number to round up.</param>
    /// <param name="multiple">The multiple the number should be rounded to.</param>
    /// <returns></returns>
    public static int RoundUp(int number, int multiple)
    {
        if (multiple == 0)
            return number;

        int remainder = number % multiple;
        if (remainder == 0)
            return number;

        return number + multiple - remainder;
    }
}