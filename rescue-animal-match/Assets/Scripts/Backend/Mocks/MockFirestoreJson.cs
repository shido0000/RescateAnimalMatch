namespace RescueAnimalMatch.Backend.Mocks
{
    /// <summary>
    /// Root shape of Resources/Mocks/mock_firestore.json, parsed with
    /// JsonUtility. Field names must match the JSON exactly.
    /// </summary>
    [System.Serializable]
    public class MockFirestoreJson
    {
        public WeeklyGoalSnapshot weekly_goal;
        public ShelterData[] shelters;
        public DonationRecord[] donations;
    }
}
