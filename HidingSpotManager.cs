using System.Collections.Generic;
using UnityEngine;

public class HidingSpotManager : MonoBehaviour
{
    public static HidingSpotManager Instance { get; private set; }

    [Header("Registered Spots")]
    public List<HidingSpot> allHidingSpots = new List<HidingSpot>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (allHidingSpots.Count == 0)
        {
            allHidingSpots.AddRange(FindObjectsOfType<HidingSpot>());
        }
    }

    public HidingSpot GetAvailableHidingSpot()
    {
        List<HidingSpot> freeSpots = allHidingSpots.FindAll(spot => !spot.isOccupied);

        if (freeSpots.Count == 0)
        {
            Debug.LogWarning("[HidingSpotManager] No free hiding spots available! Returning fallback spot.");
            return allHidingSpots.Count > 0 ? allHidingSpots[Random.Range(0, allHidingSpots.Count)] : null;
        }

        HidingSpot chosenSpot = freeSpots[Random.Range(0, freeSpots.Count)];
        chosenSpot.isOccupied = true;
        return chosenSpot;
    }

    public HidingSpot GetSpotByID(int spotID)
    {
        for (int i = 0; i < allHidingSpots.Count; i++)
        {
            if (allHidingSpots[i] != null && allHidingSpots[i].spotID == spotID)
            {
                return allHidingSpots[i];
            }
        }
        return null;
    }

    public void ReleaseSpot(int spotID)
    {
        HidingSpot spot = GetSpotByID(spotID);
        if (spot != null)
        {
            spot.isOccupied = false;
        }
    }
}