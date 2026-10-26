using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NextGenDrive.Physics;
using NextGenDrive.Network;

namespace NextGenDrive.UI
{
    public class DashboardAndAuctionHUD : MonoBehaviour
    {
        [Header("References")]
        public RealisticCarController activeCar;
        public MultiplayerNetworkClient networkClient;

        [Header("Tachometer & Speedometer UI")]
        public TextMeshProUGUI speedTextKmh;
        public TextMeshProUGUI gearText;
        public TextMeshProUGUI odometerText;
        public RectTransform rpmNeedleTransform;
        public Image shiftLightIndicator;
        public float minNeedleAngle = 130f;
        public float maxNeedleAngle = -130f;

        [Header("Live Auction & Carfax Inspection Panel")]
        public GameObject auctionPopupPanel;
        public TextMeshProUGUI auctionCarTitleText;
        public TextMeshProUGUI auctionVinAndHistoryText;
        public TextMeshProUGUI currentBidText;
        public TMP_InputField customBidInput;

        private string currentInspectedVin;
        private Rigidbody carRb;

        private void Start()
        {
            if (activeCar != null)
                carRb = activeCar.GetComponent<Rigidbody>();

            if (auctionPopupPanel != null)
                auctionPopupPanel.SetActive(false);
        }

        private void Update()
        {
            if (activeCar == null || carRb == null) return;
            UpdateInstrumentCluster();
        }

        private void UpdateInstrumentCluster()
        {
            float speedKmh = carRb.linearVelocity.magnitude * 3.6f;
            if (speedTextKmh != null)
                speedTextKmh.text = $"{Mathf.RoundToInt(speedKmh)}";

            if (gearText != null)
            {
                string gearDisplay = activeCar.currentGear switch
                {
                    0 => "R",
                    1 => "N",
                    _ => (activeCar.currentGear - 1).ToString()
                };
                gearText.text = gearDisplay;
            }

            if (odometerText != null)
                odometerText.text = $"{activeCar.odometerKm:000000.0} KM";

            if (rpmNeedleTransform != null)
            {
                float rpmRatio = Mathf.Clamp01(activeCar.currentRPM / activeCar.maxRPM);
                float angle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, rpmRatio);
                rpmNeedleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (shiftLightIndicator != null)
            {
                bool shouldShift = activeCar.currentRPM >= (activeCar.maxRPM * 0.90f);
                shiftLightIndicator.enabled = shouldShift;
            }
        }

        public void OpenAuctionInspectionCard(string vin, string modelName, int hp, int currentBid, int accidentsCount, int ownersCount)
        {
            currentInspectedVin = vin;
            if (auctionPopupPanel != null) auctionPopupPanel.SetActive(true);

            if (auctionCarTitleText != null)
                auctionCarTitleText.text = $"{modelName} ({hp} HP)";

            if (auctionVinAndHistoryText != null)
            {
                string accidentBadge = accidentsCount == 0
                    ? "<color=#00FF88>TOZA (DTP Bo'lmagan)</color>"
                    : $"<color=#FF4444>DIQQAT: {accidentsCount} marta avariya bo'lgan!</color>";

                auctionVinAndHistoryText.text = $"VIN: {vin}\nEgalari soni: {ownersCount}\nCarfax holati: {accidentBadge}";
            }

            if (currentBidText != null)
                currentBidText.text = $"Joriy Stavka: ${currentBid:N0}";
        }

        public async void OnPlaceBidButtonClicked()
        {
            if (networkClient == null || string.IsNullOrEmpty(currentInspectedVin)) return;

            if (int.TryParse(customBidInput.text, out int bidValue))
            {
                await networkClient.PlaceAuctionBid(currentInspectedVin, bidValue);
            }
        }
    }
}
