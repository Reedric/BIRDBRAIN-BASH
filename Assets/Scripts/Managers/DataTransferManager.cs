using System.Collections.Generic;
using UnityEngine;

public static class DataTransferManager
{
    // which control scheme each human player will use
    public static List<bool> isKBMInput;

    // Which bird each human player has chosen. The list matches isKBMInput
    public static List<BirdType> selectedBirds;

    // How many points a set is played to
    public static int pointsPerSet;

    // How many total sets will be played
    public static int bestOfSets;

    // How many points the final set is played to
    public static int finalSetPoints;

    // The difficulty of the bots there are (if any)
    public static AIBehavior.AIDifficulty aiDifficulty;
}
