public interface IDRPGResonanceConditionProvider
{
    int GetConditionValue(
        string protagonistCharacterId,
        string friendCharacterId);
}

public sealed class DRPGZeroResonanceConditionProvider :
    IDRPGResonanceConditionProvider
{
    public int GetConditionValue(
        string protagonistCharacterId,
        string friendCharacterId)
    {
        return 0;
    }
}
