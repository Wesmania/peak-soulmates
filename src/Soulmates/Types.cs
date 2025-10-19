using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Peak.Afflictions;
using UnityEngine;

namespace Soulmates;

[Serializable]
public struct Config
{
    public bool sharedBonk;
    public bool sharedSlip;
    public bool sharedExtraStaminaGain;
    public bool sharedExtraStaminaUse;
    public bool sharedLolli;
    public bool sharedEnergol;
    public int soulmateGroupSize;
    public float soulmateStrength;
}

[Serializable]
public struct RecalculateSoulmatesEvent
{
    public Config config;
    public List<int> soulmates;
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
public struct V3(Vector3 v)
{
    public float x = v.x;
    public float y = v.y;
    public float z = v.z;

    public Vector3 toVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public struct SharedBonk
{
    // Bonk() is called by random players, usually host. So remember the victim and send it to everyone.
    public int victim;
    public float ragdollTime;
    public V3 force;
    public V3 contactPoint;
    public float range;
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static SharedBonk Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<SharedBonk>(s);
    }
}

public struct SharedExtraStamina
{
    public float diff;
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static SharedExtraStamina Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<SharedExtraStamina>(s);
    }
}

public struct SharedAffliction
{
    public Affliction.AfflictionType type;
    public float totalTime;
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static SharedAffliction Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<SharedAffliction>(s);
    }
}

public struct WhoIsMySoulmate
{
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
    public static WhoIsMySoulmate Deserialize(string s)
    {
        return JsonConvert.DeserializeObject<WhoIsMySoulmate>(s);
    }
}

enum SoulmateEventType
{
    RECALCULATE = 0,
    DAMAGE = 1,
    UPDATE_WEIGHT = 2,
    SHARED_BONK = 4,
    SHARED_EXTRA_STAMINA = 5,
    SHARED_AFFLICTION = 6,
    WHO_IS_MY_SOULMATES = 7,
}