using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BizzyBeeGames
{
	public class MobileAdsManager : MonoBehaviour, ISaveable
	{
		#region Enums

		public enum ConsentType
		{
			Unknown,
			Personalized,
			NonPersonalized
		}

		public enum UserLocation
		{
			Unknown,
			InEEA,
			NotInEEA
		}

		#endregion

		#region Inspector Variables

		[Tooltip("If selected then when an ad fails to load it will automatically retry after a set time sepcified by retryWaitTime.")]
		[SerializeField] private bool retryLoadIfFailed = true;

		[Tooltip("The amount of seconds to wait after an ad failed to load before retrying.")]
		[SerializeField] private float retryWaitTime = 3;

		[Tooltip("If selected, shows the consent popup when the app starts.")]
		[SerializeField] private bool showConsentPopup = false;

		[Tooltip("The Popup Id to show on app start for consent.")]
		[SerializeField] private string consentPopupId = "";

		#endregion

		#region Member Variables

		private static MobileAdsManager instance;

		private const string LogTag = "MobileAdsManager";

		private bool					isInstanceInitialized;
		private bool					isAdsInitialized;
		private UserLocation			userLocation;

		private Dictionary<string, AdNetworkHandler> networkHandlers;

		private AdEvent					onInterstitialAdClosedCallback;
		private AdEvent					onRewardAdClosedCallback;
		private RewardAdGrantedEvent	onRewardGrantedCallback;

		private IEnumerator				bannerRetryRoutine;
		private IEnumerator				interstitialRetryRoutine;
		private IEnumerator				rewardRetryRoutine;

		#endregion

		#region Properties

		/// <summary>
		/// Gets an instance to MobileAdsManager
		/// </summary>
		public static MobileAdsManager Instance
		{
			get
			{
				if (instance == null)
				{
					// If the instance is null try and find one in the scene
					instance = FindFirstObjectByType<MobileAdsManager>();

					if (instance == null)
					{
						// If there is no MobileAdsManager in the scene then create one and add it to the scene
						instance = new GameObject("MobileAdsManager").AddComponent<MobileAdsManager>();
					}

					// Initialize the instance now
					instance.InitializeInstance();
				}

				return instance;
			}
		}

		public string SaveId { get { return "mobile_ads_manager"; } }

		public bool IsInitialized				{ get { return isInstanceInitialized && isAdsInitialized; } }
		public bool AreAdsEnabled				{ get { return !AdsRemoved && MobileAdsSettings.Instance.adsEnabled; } }
		public bool AreBannerAdsEnabled			{ get { return AreAdsEnabled && MobileAdsSettings.Instance.AreBannerAdsEnabled; } }
		public bool AreInterstitialAdsEnabled	{ get { return AreAdsEnabled && MobileAdsSettings.Instance.AreInterstitialAdsEnabled; } }
		public bool AreRewardAdsEnabled			{ get { return AreAdsEnabled && MobileAdsSettings.Instance.AreRewardAdsEnabled; } }

		/// <summary>
		/// Gets a value indicating whether ads removed have been removed for this user
		/// </summary>
		public bool AdsRemoved { get; private set; }

		/// <summary>
		/// Gets the ConsentType that has been set, returns ConsentType.Unknown it consent has not been set
		/// </summary>
		public ConsentType ConsentStatus { get; private set; }

		/// <summary>
		/// Gets the banner ad network.
		/// </summary>
		public AdNetworkHandler BannerAdHandler { get { return GetAdNetworkHandler(MobileAdsSettings.Instance.SelectedBannerAdNetworkId); } }

		/// <summary>
		/// Gets the banner ad network.
		/// </summary>
		public AdNetworkHandler InterstitialAdHandler { get { return GetAdNetworkHandler(MobileAdsSettings.Instance.SelectedInterstitialAdNetworkId); } }

		/// <summary>
		/// Gets the banner ad network.
		/// </summary>
		public AdNetworkHandler RewardAdHandler { get { return GetAdNetworkHandler(MobileAdsSettings.Instance.SelectedRewardAdNetworkId); } }

		/// <summary>
		/// Returns the AdState for the interstitial ad
		/// </summary>
		public AdNetworkHandler.AdState BannerAdState
		{
			get { return BannerAdHandler != null ? BannerAdHandler.BannerAdState : AdNetworkHandler.AdState.None; }
		}

		/// <summary>
		/// Returns the AdState for the interstitial ad
		/// </summary>
		public AdNetworkHandler.AdState InterstitialAdState
		{
			get { return InterstitialAdHandler != null ? InterstitialAdHandler.InterstitialAdState : AdNetworkHandler.AdState.None; }
		}

		/// <summary>
		/// Returns the AdState for the reward ad
		/// </summary>
		public AdNetworkHandler.AdState RewardAdState
		{
			get { return RewardAdHandler != null ? RewardAdHandler.RewardAdState : AdNetworkHandler.AdState.None; }
		}

		#endregion

		#region Events

		public AdEvent				OnInitialized					{ get; set; }

		public AdEvent				OnBannerAdLoading				{ get; set; }
		public AdEvent				OnBannerAdLoaded				{ get; set; }
		public AdEvent				OnBannerAdFailedToLoad			{ get; set; }
		public AdEvent				OnBannerAdShown					{ get; set; }
		public AdEvent				OnBannerAdHidden				{ get; set; }

		public AdEvent				OnInterstitialAdLoading			{ get; set; }
		public AdEvent				OnInterstitialAdLoaded			{ get; set; }
		public AdEvent				OnInterstitialAdFailedToLoad	{ get; set; }
		public AdEvent				OnInterstitialAdShowing			{ get; set; }
		public AdEvent				OnInterstitialAdShown			{ get; set; }
		public AdEvent				OnInterstitialAdClosed			{ get; set; }

		public AdEvent				OnRewardAdLoading				{ get; set; }
		public AdEvent				OnRewardAdLoaded				{ get; set; }
		public AdEvent				OnRewardAdFailedToLoad			{ get; set; }
		public AdEvent				OnRewardAdShowing				{ get; set; }
		public AdEvent				OnRewardAdShown					{ get; set; }
		public AdEvent				OnRewardAdClosed				{ get; set; }
		public RewardAdGrantedEvent	OnRewardAdGranted				{ get; set; }

		public AdEvent				OnAdsRemoved					{ get; set; }

		#endregion

		#region Delegates

		public delegate void AdEvent();
		public delegate void RewardAdGrantedEvent(string rewardId, double rewardAmount);

		#endregion

		#region Unity Methods

		private void Awake()
		{
			SaveManager.Instance.Register(this);

			LoadSave();

			InitializeInstance();
		}

		private void Start()
		{
			if (ConsentStatus == ConsentType.Unknown && MobileAdsSettings.Instance.consentSetting == MobileAdsSettings.ConsentSetting.RequireOnlyInEEA)
			{
				Logger.Log(LogTag, "User consent is unknown and the consent setting is \"require only in EEA\", checking is user is in EEA now...");

				// If the consent status is unknown and we are only requiring consent if the user is in the EEA then before initializing ads
				// lets try and determine if the user is in the EEA or not
				StartCoroutine(CheckIsInEEA());
			}
			else
			{
				// Else we have all the info we need, initialize ads now
				InitializeAds();
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Loads the banner ad
		/// </summary>
		public void LoadBannerAd()
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return;
			}

			if (BannerAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Banner ads are not enabled");

				return;
			}

			if (bannerRetryRoutine == null)
			{
				StopCoroutine(bannerRetryRoutine);

				bannerRetryRoutine = null;
			}

			BannerAdHandler.LoadBannerAd();
		}

		/// <summary>
		/// Shows the banner ad
		/// </summary>
		public void ShowBannerAd()
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return;
			}

			if (BannerAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Banner ads are not enabled");

				return;
			}

			BannerAdHandler.ShowBannerAd();
		}

		/// <summary>
		/// Hides the banner ad
		/// </summary>
		public void HideBannerAd()
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return;
			}

			if (BannerAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Banner ads are not enabled");

				return;
			}

			BannerAdHandler.HideBannerAd();
		}

		/// <summary>
		/// Loads an interstitial ad if one is not already loaded, loading, or showing
		/// </summary>
		public void LoadInterstitialAd()
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return;
			}

			if (InterstitialAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Interstitial ads are not enabled");

				return;
			}

			if (interstitialRetryRoutine == null)
			{
				StopCoroutine(interstitialRetryRoutine);

				interstitialRetryRoutine = null;
			}

			InterstitialAdHandler.LoadInterstitialAd();
		}

		/// <summary>
		/// Shows an interstitial ad
		/// </summary>
		public void ShowInterstitialAd()
		{
			ShowInterstitialAd(null);
		}

		/// <summary>
		/// Shows the interstitial ad. Returns true if the ad was successfully shown, false otherwise. If the ad was shown then onFinished will be invoked
		/// when the ad finishes.
		/// </summary>
		public bool ShowInterstitialAd(AdEvent onFinished)
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return false;
			}

			if (InterstitialAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Interstitial ads are not enabled");
				
				return false;
			}

			onInterstitialAdClosedCallback = onFinished;  
			
			return InterstitialAdHandler.ShowInterstitialAd();
		}

		/// <summary>
		/// Loads an interstitial ad if one is not already loaded, loading, or showing
		/// </summary>
		public void LoadRewardAd()
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return;
			}

			if (RewardAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Reward ads are not enabled");

				return;
			}

			if (rewardRetryRoutine == null)
			{
				StopCoroutine(rewardRetryRoutine);

				rewardRetryRoutine = null;
			}

			RewardAdHandler.LoadRewardAd();
		}

		/// <summary>
		/// Shows the reward ad
		/// </summary>
		public void ShowRewardAd()
		{
			ShowRewardAd(null, null);
		}

		/// <summary>
		/// Shows the reward ad. Returns true if the ad was successfully shown, false otherwise.
		/// The onClosedCallback will be invoked when the ad closes, use this to resume game play.
		/// The onRewardGrantedCallback will be invoked when the player should be given the reward.
		/// </summary>
		public bool ShowRewardAd(AdEvent onClosedCallback, RewardAdGrantedEvent onRewardGrantedCallback)
		{
			if (!isAdsInitialized || AdsRemoved)
			{
				return false;
			}

			if (RewardAdHandler == null)
			{
				Logger.LogWarning(LogTag, "Reward ads are not enabled");

				return false;
			}

			this.onRewardAdClosedCallback	= onClosedCallback;
			this.onRewardGrantedCallback	= onRewardGrantedCallback;

			return RewardAdHandler.ShowRewardAd();
		}

		/// <summary>
		/// Sets the consent status to use when requesting ads, 0 for non-personalized ads and 1 for personalized ads
		/// </summary>
		public void SetConsentStatus(int consentStatus)
		{
			switch (consentStatus)
			{
				case 0:
					SetConsentStatus(ConsentType.NonPersonalized);
					break;
				case 1:
					SetConsentStatus(ConsentType.Personalized);
					break;
				default:
					Logger.LogError(LogTag, "Invalid constent status: " + consentStatus + ". Must be either 0 (non-personalized) or 1 (personalized)");
					break;
			}

		}

		/// <summary>
		/// Sets the consent status to use when requesting ads
		/// </summary>
		public void SetConsentStatus(ConsentType consentStatus)
		{
			Logger.Log(LogTag, "Setting consent status to: " + consentStatus.ToString());

			ConsentStatus = consentStatus;

			if (!isAdsInitialized)
			{
				// If ads have not been initialized yet then do it now
				InitializeAds();
			}
			else
			{
				// Notify all AdNetworkHandlers that the consent status has changed
				foreach (KeyValuePair<string, AdNetworkHandler> pair in networkHandlers)
				{
					pair.Value.SetConsentStatus(consentStatus);
				}
			}
		}

		/// <summary>
		/// Removes ads for this user
		/// </summary>
		public void RemoveAds()
		{
			if (AdsRemoved)
			{
				Logger.Log(LogTag, "RemoveAds: Ads already removed");

				return;
			}

			Logger.Log(LogTag, "Removing ads");

			AdsRemoved = true;

			if (isAdsInitialized)
			{
				foreach (KeyValuePair<string, AdNetworkHandler> pair in networkHandlers)
				{
					pair.Value.AdsRemoved();
				}
			}

			if (OnAdsRemoved != null)
			{
				OnAdsRemoved();
			}
		}

		/// <summary>
		/// Gets the banner height in pixels.
		/// </summary>
		public float GetBannerHeightInPixels()
		{
			if (BannerAdHandler != null)
			{
				return BannerAdHandler.GetBannerHeightInPixels();
			}

			return 0f;
		}

		/// <summary>
		/// Gets the banner height in pixels.
		/// </summary>
		public MobileAdsSettings.BannerPosition GetBannerPosition()
		{
			if (BannerAdHandler != null)
			{
				return BannerAdHandler.GetBannerPosition();
			}

			return MobileAdsSettings.BannerPosition.Bottom;
		}

		/// <summary>
		/// Starts a coroutine, used by AdNetworkHandlers
		/// </summary>
		public void BeginCoroutine(IEnumerator routine)
		{
			StartCoroutine(routine);
		}

		/// <summary>
		/// Stops a coroutine, used by AdNetworkHandlers
		/// </summary>
		public void EndCoroutine(IEnumerator routine)
		{
			StopCoroutine(routine);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Initializes this MobileAdsManager instance
		/// </summary>
		private void InitializeInstance()
		{
			if (isInstanceInitialized)
			{
				// This instance is already initialized
				return;
			}

			if (instance != null && instance != this)
			{
				Logger.Log(LogTag, "There is already an instance of MobileAdsManager in the scene, destroying second instance now.");

				Destroy(this);

				return;
			}

			Logger.Log(LogTag, "Initializing instance");

			instance = this;

			isInstanceInitialized = true;

			DontDestroyOnLoad(this);
		}

		/// <summary>
		/// Initializes ad networks and pre-loads ads
		/// </summary>
		private void InitializeAds()
		{
			// Check if ads have already been initialized
			if (!isInstanceInitialized || isAdsInitialized)
			{
				return;
			}

			Logger.Log(LogTag, "Initializing ads");

			// Check if ads have been removed for this user
			if (AdsRemoved)
			{
				Logger.Log(LogTag, "Ads have been removed, ads will not be initialized or requested");

				return;
			}

			// Check if ads are even enabled in settings
			if (!AreAdsEnabled)
			{
				Logger.Log(LogTag, "Ads are not enabled in settings, ads will not be initialized or requested");

				return;
			}

			// Check if consent is required before initializing ads
			if (RequireUserConsent())
			{
				if (ConsentStatus == ConsentType.Unknown)
				{
					Logger.Log(LogTag, "Consent is required and has not been given, ads will not be initialized or requested");

					if (showConsentPopup && PopupManager.Exists())
					{
						PopupManager.Instance.Show(consentPopupId);
					}

					// Consent has not been set, do not initialize ads until consent has been set
					return;
				}

				Logger.Log(LogTag, "Consent type is set to: " + ConsentStatus);
			}

			CreateAdNetworkHandlers();

			isAdsInitialized = true;

			if (OnInitialized != null)
			{
				OnInitialized();
			}
		}

		/// <summary>
		/// Creates the AdNetworkHandlers that will handle shwoing banner/interstitial/reward for the selected network
		/// </summary>
		private void CreateAdNetworkHandlers()
		{
			networkHandlers = new Dictionary<string, AdNetworkHandler>();

			// Create the AdNetworkHandler that should be used by banner ads
			if (AreBannerAdsEnabled)
			{
				try
				{
					CreateAdNetworkHandler(MobileAdsSettings.Instance.SelectedBannerAdNetworkId);

					BannerAdHandler.bannerAdsEnabled = true;

					// Add this instance event listeners
					BannerAdHandler.OnBannerAdLoading		+= BannerAdLoaded;
					BannerAdHandler.OnBannerAdLoaded		+= BannerAdLoaded;
					BannerAdHandler.OnBannerAdFailedToLoad	+= BannerAdFailedToLoad;
					BannerAdHandler.OnBannerAdShown			+= BannerAdShown;
					BannerAdHandler.OnBannerAdHidden		+= BannerAdHidden;
				}
				catch(System.Exception ex)
				{
					Logger.LogError(LogTag, "Could not create banner AdNetworkHandler: " + ex.Message);
				}
			}

			// Create the AdNetworkHandler that should be used by interstitial ads
			if (AreInterstitialAdsEnabled)
			{
				try
				{
					CreateAdNetworkHandler(MobileAdsSettings.Instance.SelectedInterstitialAdNetworkId);

					InterstitialAdHandler.interstitialAdsEnabled = true;

					// Add this instance event listeners
					InterstitialAdHandler.OnInterstitialAdLoading		+= InterstitialAdLoading;
					InterstitialAdHandler.OnInterstitialAdLoaded		+= InterstitialAdLoaded;
					InterstitialAdHandler.OnInterstitialAdFailedToLoad	+= InterstitialAdFailedToLoad;
					InterstitialAdHandler.OnInterstitialAdShowing		+= InterstitialAdShowing; 
					InterstitialAdHandler.OnInterstitialAdShown			+= InterstitialAdShown;
					InterstitialAdHandler.OnInterstitialAdClosed		+= InterstitialAdClosed;
				}
				catch(System.Exception ex)
				{
					Logger.LogError(LogTag, "Could not create interstitial AdNetworkHandler: " + ex.Message);
				}
			}

			// Create the AdNetworkHandler that should be used by reward ads
			if (AreRewardAdsEnabled)
			{
				try
				{
					CreateAdNetworkHandler(MobileAdsSettings.Instance.SelectedRewardAdNetworkId);

					RewardAdHandler.rewardAdsEnabled = true;

					// Add event listeners
					RewardAdHandler.OnRewardAdLoaded		+= RewardAdLoaded;
					RewardAdHandler.OnRewardAdLoading		+= RewardAdLoading;
					RewardAdHandler.OnRewardAdFailedToLoad	+= RewardAdFailedToLoad;
					RewardAdHandler.OnRewardAdShowing		+= RewardAdShowing;
					RewardAdHandler.OnRewardAdShown			+= RewardAdShown;
					RewardAdHandler.OnRewardAdClosed		+= RewardAdClosed;
					RewardAdHandler.OnRewardAdGranted		+= RewardAdGranted;
				}
				catch(System.Exception ex)
				{
					Logger.LogError(LogTag, "Could not create reward AdNetworkHandler: " + ex.Message);
				}
			}

			// Initialize each ad handler
			foreach (KeyValuePair<string, AdNetworkHandler> pair in networkHandlers)
			{
				pair.Value.SetConsentStatus(ConsentStatus);
				pair.Value.Initialize();
			}
		}

		/// <summary>
		/// Creates the AdNetworkHandler with the given network id
		/// </summary>
		private void CreateAdNetworkHandler(string networkId)
		{
			if (!networkHandlers.ContainsKey(networkId))
			{
				AdNetworkHandler networkHandler = null;

				switch (networkId)
				{
					case MobileAdsSettings.AdMobNetworkId:
						networkHandler = new AdMobNetworkHandler();
						break;
					case MobileAdsSettings.UnityAdsNetworkId:
						networkHandler = new UnityAdsNetworkHandler();
						break;
					default:
						throw new System.Exception("Unknown ad network id: " + networkId);
				}

				networkHandlers.Add(networkId, networkHandler);
			}
		}

		private IEnumerator CheckIsInEEA()
		{
			string url = "http://adservice.google.com/getconfig/pubvendors";

			using (UnityWebRequest unityWebRequest = UnityWebRequest.Get(url))
			{
				// Request and wait for the desired page.
				#if UNITY_2017_2_OR_NEWER
				yield return unityWebRequest.SendWebRequest();
				#else
				yield return unityWebRequest.Send();
				#endif

				bool isError = false;

				#if UNITY_2017_1_OR_NEWER
				isError = unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError;
				#else
				isError = unityWebRequest.isError;
				#endif

				if (isError)
				{
					Logger.Log(LogTag, "Error when checking users location, error: " + unityWebRequest.error);

					userLocation = UserLocation.Unknown;
				}
				else
				{
					// Example response: {"is_request_in_eea_or_unknown":false}
					JSONNode json = JSON.Parse(unityWebRequest.downloadHandler.text);

					if (json.IsObject && (json.AsObject).HasKey("is_request_in_eea_or_unknown") && (json.AsObject)["is_request_in_eea_or_unknown"].IsBoolean)
					{
						userLocation = (json.AsObject)["is_request_in_eea_or_unknown"].AsBool ? UserLocation.InEEA : UserLocation.NotInEEA;
					}
					else
					{
						Logger.Log(LogTag, "Could not parse response, invalid or unexpected format");

						userLocation = UserLocation.Unknown;
					}
				}

				Logger.Log(LogTag, "User location: " + userLocation);

				InitializeAds();
			}
		}

		/// <summary>
		/// Checks if ads will not be requested until consent is given
		/// </summary>
		private bool RequireUserConsent()
		{
			if (MobileAdsSettings.Instance.consentSetting == MobileAdsSettings.ConsentSetting.NotRequired)
			{
				return false;
			}

			if (MobileAdsSettings.Instance.consentSetting == MobileAdsSettings.ConsentSetting.RequireAll)
			{
				return true;
			}

			return (userLocation == UserLocation.InEEA || userLocation == UserLocation.Unknown);
		}

		/// <summary>
		/// Gets the ad network handler if it has been created
		/// </summary>
		private AdNetworkHandler GetAdNetworkHandler(string networkId)
		{
			if (networkHandlers != null && networkHandlers.ContainsKey(networkId))
			{
				return networkHandlers[networkId];
			}

			return null;
		}

		/// <summary>
		/// Invoked when the interstitial ad failed to load an ad.
		/// </summary>
		private void BannerAdFailedToLoad()
		{
			if (retryLoadIfFailed)
			{
				if (bannerRetryRoutine != null)
				{
					StopCoroutine(bannerRetryRoutine);
				}

				StartCoroutine(bannerRetryRoutine = RetryBannerAdLoad());
			}

			if (OnBannerAdFailedToLoad!= null)
			{
				OnBannerAdFailedToLoad();
			}
		}

		/// <summary>
		/// Waits the specified amount of time before trying to load a new banner ad
		/// </summary>
		private IEnumerator RetryBannerAdLoad()
		{
			yield return new WaitForSeconds(retryWaitTime);

			if (BannerAdState == AdNetworkHandler.AdState.None)
			{
				LoadBannerAd();
			}
		}

		/// <summary>
		/// Invoked when a banner ad starts loading
		/// </summary>
		private void BannerAdLoading()
		{
			if (OnBannerAdLoading!= null)
			{
				OnBannerAdLoading();
			}
		}

		/// <summary>
		/// Invoked when a banner ad has loaded
		/// </summary>
		private void BannerAdLoaded()
		{
			if (OnBannerAdLoaded!= null)
			{
				OnBannerAdLoaded();
			}
		}

		/// <summary>
		/// Invoked when the banner ad is shown
		/// </summary>
		private void BannerAdShown()
		{
			if (OnBannerAdShown!= null)
			{
				OnBannerAdShown();
			}
		}

		/// <summary>
		/// Invoked when the banner ad is hidden
		/// </summary>
		private void BannerAdHidden()
		{
			if (OnBannerAdHidden!= null)
			{
				OnBannerAdHidden();
			}
		}

		/// <summary>
		/// Invoked when the interstitial ad failed to load an ad.
		/// </summary>
		private void InterstitialAdFailedToLoad()
		{
			if (retryLoadIfFailed)
			{
				if (interstitialRetryRoutine != null)
				{
					StopCoroutine(interstitialRetryRoutine);
				}

				StartCoroutine(interstitialRetryRoutine = RetryInterstitialAdLoad());
			}

			if (OnInterstitialAdFailedToLoad!= null)
			{
				OnInterstitialAdFailedToLoad();
			}
		}

		/// <summary>
		/// Waits the specified amount of time before pre-loading an interstitial ad
		/// </summary>
		private IEnumerator RetryInterstitialAdLoad()
		{
			yield return new WaitForSeconds(retryWaitTime);

			if (InterstitialAdState == AdNetworkHandler.AdState.None)
			{
				LoadInterstitialAd();
			}
		}

		/// <summary>
		/// Invoked when the interstitial ad closes
		/// </summary>
		private void InterstitialAdClosed()
		{
			if (onInterstitialAdClosedCallback != null)
			{
				onInterstitialAdClosedCallback();
			}

			if (OnInterstitialAdClosed!= null)
			{
				OnInterstitialAdClosed();
			}
		}

		/// <summary>
		/// Invoked when an iterstitial ad starts loading
		/// </summary>
		private void InterstitialAdLoading()
		{
			if (OnInterstitialAdLoading!= null)
			{
				OnInterstitialAdLoading();
			}
		}

		/// <summary>
		/// Invoked when an interstitial ad has loaded successfully
		/// </summary>
		private void InterstitialAdLoaded()
		{
			if (OnInterstitialAdLoaded!= null)
			{
				OnInterstitialAdLoaded();
			}
		}

		/// <summary>
		/// Invoked when an interstitial ad is about to show
		/// </summary>
		private void InterstitialAdShowing()
		{
			if (OnInterstitialAdShowing!= null)
			{
				OnInterstitialAdShowing();
			}
		}

		/// <summary>
		/// Invoked when an interstitial ad is shown on the screen
		/// </summary>
		private void InterstitialAdShown()
		{
			if (OnInterstitialAdShown!= null)
			{
				OnInterstitialAdShown();
			}
		}

		/// <summary>
		/// Invoked when a reward ad failes to load
		/// </summary>
		private void RewardAdFailedToLoad()
		{
			if (retryLoadIfFailed)
			{
				if (rewardRetryRoutine != null)
				{
					StopCoroutine(rewardRetryRoutine);
				}

				StartCoroutine(rewardRetryRoutine = RetryRewardAdLoad());
			}

			if (OnRewardAdFailedToLoad!= null)
			{
				OnRewardAdFailedToLoad();
			}
		}

		/// <summary>
		/// Waits the specified amount of time before pre-loading an reward ad
		/// </summary>
		private IEnumerator RetryRewardAdLoad()
		{
			yield return new WaitForSeconds(retryWaitTime);

			if (RewardAdState == AdNetworkHandler.AdState.None)
			{
				LoadRewardAd();
			}
		}

		/// <summary>
		/// Invoked when a reward ad closes
		/// </summary>
		private void RewardAdClosed()
		{
			if (onRewardAdClosedCallback != null)
			{
				onRewardAdClosedCallback();
			}

			if (OnRewardAdClosed!= null)
			{
				OnRewardAdClosed();
			}
		}

		/// <summary>
		/// Invoked when the player should be rewarded for watching a reward ad
		/// </summary>
		private void RewardAdGranted(string rewardId, double rewardAmount)
		{
			if (onRewardGrantedCallback != null)
			{
				onRewardGrantedCallback(rewardId, rewardAmount);
			}

			if (OnRewardAdGranted!= null)
			{
				OnRewardAdGranted(rewardId, rewardAmount);
			}
		}

		/// <summary>
		/// Invoked when a reward ad starts loading
		/// </summary>
		private void RewardAdLoading()
		{
			if (OnRewardAdLoading!= null)
			{
				OnRewardAdLoading();
			}
		}

		/// <summary>
		/// Invoked when a reward ad has successfully loaded
		/// </summary>
		private void RewardAdLoaded()
		{
			if (OnRewardAdLoaded!= null)
			{
				OnRewardAdLoaded();
			}
		}

		/// <summary>
		/// Invoked when a reward ad is about to show
		/// </summary>
		private void RewardAdShowing()
		{
			if (OnRewardAdShowing!= null)
			{
				OnRewardAdShowing();
			}
		}

		/// <summary>
		/// Invoked when a reward ad is shown on the screen
		/// </summary>
		private void RewardAdShown()
		{
			if (OnRewardAdShown!= null)
			{
				OnRewardAdShown();
			}
		}

		#endregion

		#region Save Methods

		public Dictionary<string, object> Save()
		{
			Dictionary<string, object> json = new Dictionary<string, object>();

			json["ads_removed"]		= AdsRemoved;
			json["consent_status"]	= (int)ConsentStatus;

			return json;
		}

		public bool LoadSave()
		{
			JSONNode json = SaveManager.Instance.LoadSave(this);

			if (json == null)
			{
				return false;
			}

			AdsRemoved		= json["ads_removed"].AsBool;
			ConsentStatus	= (ConsentType)json["consent_status"].AsInt;

			return true;
		}

		#endregion
	}
}
