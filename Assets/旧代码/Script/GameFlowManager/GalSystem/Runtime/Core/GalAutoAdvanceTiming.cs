using UnityEngine;

public static class GalAutoAdvanceTiming
{
    public const float BaseSeconds = 0.8f;
    public const float SecondsPerVisibleCharacter = 0.14f;
    public const float MinimumSeconds = 1.8f;
    public const float MaximumSeconds = 6.5f;

    public static float Calculate(string text, float overrideSeconds)
    {
        if (overrideSeconds > 0f)
            return overrideSeconds;

        int visibleCharacters = CountVisibleCharacters(text);
        return Mathf.Clamp(
            BaseSeconds + visibleCharacters * SecondsPerVisibleCharacter,
            MinimumSeconds,
            MaximumSeconds);
    }

    private static int CountVisibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 0;
        bool insideTag = false;
        foreach (char character in text)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (insideTag && character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag && !char.IsWhiteSpace(character))
                count++;
        }

        return count;
    }
}
