using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CarParking.Multiplayer
{
    /// <summary>
    /// Unity bridge to car token ledger concepts (mint / transfer via backend messages).
    /// Full crypto verification runs server-side in car_token_ledger.py.
    /// </summary>
    public class CarTokenLedgerClient : MonoBehaviour
    {
        public static CarTokenLedgerClient Instance { get; private set; }

        public string localAccountId;
        public List<string> ownedTokenIds = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (string.IsNullOrEmpty(localAccountId))
                localAccountId = "player_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public async Task<string> MintVehicleToken(string vehicleClass, Dictionary<string, object> metadata)
        {
            string tokenId = Guid.NewGuid().ToString();
            ownedTokenIds.Add(tokenId);

            if (NetworkBackendClient.Instance != null)
            {
                // Extend protocol: market_create already tracks vehicles; ledger is authoritative on server
                await NetworkBackendClient.Instance.CreateMarketListing(tokenId, 0f, metadata ?? new Dictionary<string, object>
                {
                    { "class", vehicleClass },
                    { "owner", localAccountId }
                });
            }

            Debug.Log($"[TokenLedger] Minted {tokenId} class={vehicleClass}");
            return tokenId;
        }

        public async Task<bool> TransferPinkSlip(string tokenId, string toAccountId, float price)
        {
            if (!ownedTokenIds.Contains(tokenId)) return false;

            if (NetworkBackendClient.Instance != null)
            {
                // Use market buy flow as transfer primitive when listing exists
                await NetworkBackendClient.Instance.BuyMarketListing(tokenId);
            }

            ownedTokenIds.Remove(tokenId);
            Debug.Log($"[TokenLedger] Transferred {tokenId} -> {toAccountId} for {price}");
            return true;
        }

        public bool Owns(string tokenId) => ownedTokenIds.Contains(tokenId);
    }
}
