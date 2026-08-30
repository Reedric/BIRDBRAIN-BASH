using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Bird Database", menuName = "Birds/Bird Database")]
public class BirdDatabase : ScriptableObject
{
    public List<BirdData> birds;

    public BirdData GetBirdData(string id)
    {
        return birds.Find(bird => bird.birdName == id);
    }
}
