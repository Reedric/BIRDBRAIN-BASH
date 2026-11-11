using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every prefab that NetworkFx may need to show on clients, so a prefab can be sent as an index.
/// Filled in by BIRDBRAIN > Apply Network Setup, and loaded from Resources.
/// </summary>
public class NetworkFxRegistry : ScriptableObject
{
    public const string ResourcePath = "NetworkFxRegistry";

    public List<GameObject> prefabs = new();
}
