public struct AudioRequest
{
    public AudioStateType audioStateType;
    public object Data;
    public bool IsUrgent;

    public AudioRequest(AudioStateType audioStateType, object data = null, bool isUrgent = false)
    {
        this.audioStateType = audioStateType;
        Data = data;
        IsUrgent = isUrgent;
    }
}