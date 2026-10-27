using System.Collections.Generic;
using UnityEngine;
using CarParking.Vehicle;
using CarParking.Core;

namespace CarParking.Multiplayer
{
    /// <summary>
    /// Full Avtobozor (car market) system.
    /// Park in a bay, set price, show 3D spec board, negotiate and transfer ownership.
    /// </summary>
    public class CarMarketAndTradeSystem : MonoBehaviour
    {
        public static CarMarketAndTradeSystem Instance { get; private set; }

        [System.Serializable]
        public class MarketListing
        {
            public int listingId;
            public int sellerNetId;
            public GameObject vehicle;
            public float price;
            public string engineName;
            public int horsepower;
            public float turboPSI;
            public float frontCamber;
            public float mileage;
            public bool isSold;
        }

        [Header("Market Settings")]
        public Transform[] marketBays;
        public GameObject specBoardPrefab;
        public float maxListingDistance = 6f;

        private Dictionary<int, MarketListing> _listings = new();
        private int _nextListingId = 1;
        private Dictionary<int, GameObject> _activeBoards = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool TryCreateListing(GameObject vehicle, float price, int sellerNetId)
        {
            if (vehicle == null || price <= 0f) return false;

            var tuning = vehicle.GetComponent<VehicleTuningSystem>();
            var drivetrain = vehicle.GetComponent<DrivetrainSystem>();

            MarketListing listing = new MarketListing
            {
                listingId = _nextListingId++,
                sellerNetId = sellerNetId,
                vehicle = vehicle,
                price = price,
                engineName = tuning != null ? tuning.currentEngine.ToString() : "Stock",
                horsepower = drivetrain != null ? Mathf.RoundToInt(drivetrain.maxTorque * 0.9f) : 200,
                turboPSI = tuning != null ? tuning.turboBoostPSI : 0f,
                frontCamber = tuning != null ? tuning.frontCamber : 0f,
                mileage = 0f,
                isSold = false
            };

            _listings[listing.listingId] = listing;
            SpawnSpecBoard(listing);
            return true;
        }

        private void SpawnSpecBoard(MarketListing listing)
        {
            if (specBoardPrefab == null || listing.vehicle == null) return;

            Vector3 pos = listing.vehicle.transform.position + listing.vehicle.transform.forward * 1.8f + Vector3.up * 1.4f;
            GameObject board = Instantiate(specBoardPrefab, pos, listing.vehicle.transform.rotation);
            board.transform.SetParent(listing.vehicle.transform);

            // Update text components if present
            var texts = board.GetComponentsInChildren<TextMesh>();
            foreach (var t in texts)
            {
                if (t.name.Contains("Price")) t.text = $"${listing.price:N0}";
                else if (t.name.Contains("HP")) t.text = $"{listing.horsepower} HP";
                else if (t.name.Contains("Engine")) t.text = listing.engineName;
                else if (t.name.Contains("Turbo")) t.text = $"{listing.turboPSI:F1} PSI";
                else if (t.name.Contains("Camber")) t.text = $"{listing.frontCamber:F1}°";
                else if (t.name.Contains("Mileage")) t.text = $"{listing.mileage:F0} km";
            }

            _activeBoards[listing.listingId] = board;
        }

        public bool TryBuyListing(int listingId, int buyerNetId)
        {
            if (!_listings.TryGetValue(listingId, out var listing)) return false;
            if (listing.isSold || listing.sellerNetId == buyerNetId) return false;

            listing.isSold = true;

            // Transfer ownership via save system
            if (GarageAndWorldSaveManager.Instance != null && listing.vehicle != null)
            {
                GarageAndWorldSaveManager.Instance.SaveAll();
            }

            if (_activeBoards.TryGetValue(listingId, out var board))
            {
                Destroy(board);
                _activeBoards.Remove(listingId);
            }

            _listings.Remove(listingId);
            return true;
        }

        public void CancelListing(int listingId)
        {
            if (_listings.TryGetValue(listingId, out var listing))
            {
                if (_activeBoards.TryGetValue(listingId, out var board))
                    Destroy(board);

                _activeBoards.Remove(listingId);
                _listings.Remove(listingId);
            }
        }

        public List<MarketListing> GetActiveListings()
        {
            return new List<MarketListing>(_listings.Values);
        }

        public MarketListing GetListingNearPosition(Vector3 pos)
        {
            foreach (var listing in _listings.Values)
            {
                if (listing.vehicle == null) continue;
                if (Vector3.Distance(pos, listing.vehicle.transform.position) < maxListingDistance)
                    return listing;
            }
            return null;
        }
    }
}
