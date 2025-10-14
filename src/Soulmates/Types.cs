using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Soulmates;

[Serializable]
public struct RecalculateSoulmatesEvent
{
    public List<int> soulmates;
    public Dictionary<int, Dictionary<CharacterAfflictions.STATUSTYPE, float>> playerStatus;
    public bool firstTime;

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static RecalculateSoulmatesEvent Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<RecalculateSoulmatesEvent>(s);
    }
}

[Serializable]
public enum SharedDamageKind
{
    ADD,
    SUBTRACT,
    SET
}

[Serializable]
public struct SharedDamage
{
    public CharacterAfflictions.STATUSTYPE type;
    public float value;
    public SharedDamageKind kind;
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static SharedDamage Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<SharedDamage>(s);
    }
}

[Serializable]
public struct UpdateWeight
{
    public float weight = 0.0f;
    public float thorns = 0.0f;

    public UpdateWeight() { }
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static UpdateWeight Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<UpdateWeight>(s);
    }
}

[Serializable]
public struct ConnectToSoulmate
{
    public int from;
    public int to;
    public Dictionary<CharacterAfflictions.STATUSTYPE, float> status;
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static ConnectToSoulmate Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<ConnectToSoulmate>(s);
    }
}
enum SoulmateEventType
{
    RECALCULATE = 0,
    DAMAGE = 1,
    UPDATE_WEIGHT = 2,
    CONNECT_TO_SOULMATE = 3
}