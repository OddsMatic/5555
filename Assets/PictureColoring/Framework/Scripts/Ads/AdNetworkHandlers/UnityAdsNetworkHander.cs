using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if BBG_UNITYADS

using UnityEngine.Monetization; 
using UnityEngine.Advertisements;

#endif

namespace BizzyBeeGames
{
	public class UnityAdsNetworkHandler : AdNetworkHandler
	{
		#region Member Variables

		#if BBG_UNITYADS

		private UnityAdsEventListener	unityAdsEventListener;
		private bool					showBanner;
		private float					extraWaitTime = 0;

		#endif

		#endregion

		#region Properties

		protected override string LogTag { get { return "UnityAds"; } }

		private string GameId					{ get { return MobileAdsSettings.Instance.unityAdsConfig.GameId; } }
		private string BannerPlacement			{ get { return MobileAdsSettings.Instance.unityAdsConfig.BannerPlacement; } }
		private string InterstitialPlacement	{ get { return MobileAdsSettings.Instance.unityAdsConfig.InterstitialPlacement; } }
		private string RewardPlacement			{ get { return MobileAdsSettings.Instance.unityAdsConfig.RewardPlacement; } }

		#endregion

		#region Protected Methods

		protected override void DoInitialize()
		{
			Logger.Log(LogTag, "Initializing Unity Ads");

			#if BBG_UNITYADS

			Logger.Log(LogTag, "Game Id: " + GameId);

			Monetization.Initialize(GameId, MobileAdsSettings.Instance.unityAdsConfig.enableTestAds);

			isInitialized = true;

			CreateUnityAdsEventListener();

			SetConsentStatus();

			if (bannerAdsEnabled)
			{
				Logger.Log(LogTag, "Banner ads are enabled, placement: " + BannerPlacement);

				// Advertisement is only used for banners
				Advertisement.Initialize(GameId, MobileAdsSettings.Instance.unityAdsConfig.enableTestAds); 

				showBanner		= MobileAdsSettings.Instance.unityAdsConfig.ShowBannerOnAppStart;
				extraWaitTime	= 3;

				PreLoadBannerAd();
			}

			if (interstitialAdsEnabled)
			{
				Logger.Log(LogTag, "Interstitial ads are enabled, placement: " + InterstitialPlacement);

				PreLoadInterstitialAd();
			}

			if (rewardAdsEnabled)
			{
				Logger.Log(LogTag, "Reward ads are enabled, placement: " + RewardPlacement);

				PreLoadRewardAd();
			}

			#else

			Logger.LogError(LogTag, "Unity Ads has not been setup in Mobile Ads Settings");

			#endif
		}

		protected override void DoLoadBannerAd()
		{
			#if BBG_UNITYADS

			BannerAdState = AdState.Loading;
			NotifyBannerAdLoading();

			MobileAdsManager.Instance.BeginCoroutine(WaitForBannerAdReady(extraWaitTime));

			extraWaitTime = 0;

			#endif
		}

		protected override void DoShowBannerAd()
		{
			#if BBG_UNITYADS

			showBanner = true;

			switch (BannerAdState)
			{
				case AdState.Loaded:
					Advertisement.Banner.SetPosition(GetUnityAdsBannerPosition());
					Advertisement.Banner.Show(BannerPlacement);
					BannerAdState = AdState.Shown;
					NotifyBannerAdShown();
					break;
				case AdState.None:
					LoadBannerAd();
					break;
				default:
					Logger.Log(LogTag, "DoShowBannerAd: Nothing will happen because BannerAdState is: " + BannerAdState);
					break;
			}

			#endif
		}

		protected override void DoHideBannerAd()
		{
			#if BBG_UNITYADS

			showBanner = false;

			if (BannerAdState == AdState.Shown)
			{
				Advertisement.Banner.Hide();
				
				BannerAdState = AdState.Loaded;
				NotifyBannerAdHidden();
			}
			else
			{
				Logger.Log(LogTag, "DoHideBannerAd: Nothing will happen because BannerAdState is: " + BannerAdState);
			}

			#endif
		}

		protected override void DoLoadInterstitialAd()
		{
			#if BBG_UNITYADS

			NotifyInterstitialAdLoading();

			MobileAdsManager.Instance.BeginCoroutine(WaitForInterstitialAdReady());

			#endif
		}

		protected override void DoShowInterstitialAd()
		{
			#if BBG_UNITYADS

			NotifyInterstitialAdShowing();

			unityAdsEventListener.InterstitialAdShown();

			(Monetization.GetPlacementContent(InterstitialPlacement) as ShowAdPlacementContent).Show(unityAdsEventListener.InterstitialAdFinished);

			NotifyInterstitialAdShown();

			#endif
		}

		protected override void DoLoadRewardAd()
		{
			#if BBG_UNITYADS

			NotifyRewardAdLoading();

			MobileAdsManager.Instance.BeginCoroutine(WaitForRewardAdReady());

			#endif
		}

		protected override void DoShowRewardAd()
		{
			#if BBG_UNITYADS

			NotifyRewardAdShowing();

			unityAdsEventListener.RewardAdShown();

			(Monetization.GetPlacementContent(RewardPlacement) as ShowAdPlacementContent).Show(unityAdsEventListener.RewardAdFinished);

			NotifyRewardAdShown();

			#endif
		}

		protected override void DoAdsRemoved()
		{
			#if BBG_UNITYADS

			if (BannerAdState == AdState.Shown)
			{
				Advertisement.Banner.Hide(true);
			}

			BannerAdState		= AdState.None;
			InterstitialAdState	= AdState.None;
			RewardAdState		= AdState.None;

			#endif
		}

		protected override void ConsentStatusUpdated()
		{
			#if BBG_UNITYADS

			SetConsentStatus();

			#endif
		}

		protected override float DoGetBannerHeightInPixels()
		{
			return Mathf.RoundToInt(50 * UnityEngine.Screen.dpi / 160);
		}

		protected override MobileAdsSettings.BannerPosition DoGetBannerPosition()
		{
			return MobileAdsSettings.Instance.unityAdsConfig.BannerPosition;
		}

		#endregion

		#region Private Methods

		#if BBG_UNITYADS

		private void SetConsentStatus()
		{
			UnityEngine.Monetization.MetaData	modMetaData = new UnityEngine.Monetization.MetaData("gdpr");
			UnityEngine.Advertisements.MetaData	advMetaData = new UnityEngine.Advertisements.MetaData("gdpr");

			switch (MobileAdsManager.Instance.ConsentStatus)
			{
				case MobileAdsManager.ConsentType.NonPersonalized:
					modMetaData.Set("consent", "false");
					Monetization.SetMetaData(modMetaData);
					advMetaData.Set("consent", "false");
					Advertisement.SetMetaData(advMetaData);
					break;
				case MobileAdsManager.ConsentType.Personalized:
					modMetaData.Set("consent", "true");
					Monetization.SetMetaData(modMetaData);
					advMetaData.Set("consent", "true");
					Advertisement.SetMetaData(advMetaData);
					break;
			}

		}

		private void CreateUnityAdsEventListener()
		{
			// Create the MonoBehaviour that will notify finished events on the main thread
			unityAdsEventListener = new GameObject("UnityAdsEventListener").AddComponent<UnityAdsEventListener>();
			unityAdsEventListener.OnInterstitialAdFinished	= InterstitialAdFinished;
			unityAdsEventListener.OnRewardAdFinished		= RewardAdFinished;

			Object.DontDestroyOnLoad(unityAdsEventListener.gameObject);
		}

		private BannerPosition GetUnityAdsBannerPosition()
		{
			// Set the ads position
			switch (MobileAdsManager.Instance.BannerAdHandler.GetBannerPosition())
			{
				case MobileAdsSettings.BannerPosition.Top:
					return BannerPosition.TOP_CENTER;
				case MobileAdsSettings.BannerPosition.TopLeft:
					return BannerPosition.TOP_LEFT;
				case MobileAdsSettings.BannerPosition.TopRight:
					return BannerPosition.TOP_RIGHT;
				case MobileAdsSettings.BannerPosition.Bottom:
					return BannerPosition.BOTTOM_CENTER;
				case MobileAdsSettings.BannerPosition.BottomLeft:
					return BannerPosition.BOTTOM_LEFT;
				case MobileAdsSettings.BannerPosition.BottomRight:
					return BannerPosition.BOTTOM_RIGHT;
			}

			return BannerPosition.BOTTOM_CENTER;
		}

		private IEnumerator WaitForBannerAdReady(float waitTime)
		{
			if (!Advertisement.IsReady(BannerPlacement))
			{
				yield return null;
			}

			// We need to wait a couple more seconds right after the app starts before showing the banner or it wont actually show
			if (waitTime > 0)
			{
				yield return new WaitForSeconds(waitTime);
			}

			BannerAdState = AdState.Loaded;
			NotifyBannerAdLoaded();

			if (showBanner && preLoadBannerAds)
			{
				ShowBannerAd();
			}
		}

		private IEnumerator WaitForInterstitialAdReady()
		{
			if (!Monetization.IsReady(InterstitialPlacement))
			{
				yield return null;
			}

			NotifyInterstitialAdLoaded();
		}

		private IEnumerator WaitForRewardAdReady()
		{
			if (!Monetization.IsReady(RewardPlacement))
			{
				yield return null;
			}

			NotifyRewardAdLoaded();
		}

		private void InterstitialAdFinished(UnityEngine.Monetization.ShowResult showResult)
		{
			NotifyInterstitialAdClosed();

			PreLoadInterstitialAd();
		}

		private void RewardAdFinished(UnityEngine.Monetization.ShowResult showResult)
		{
			if (showResult == UnityEngine.Monetization.ShowResult.Finished)
			{
				string rewardId = (Monetization.GetPlacementContent(RewardPlacement) as ShowAdPlacementContent).rewardId;

				NotifyRewardAdGranted(rewardId, 0);
			}

			NotifyRewardAdClosed();

			PreLoadRewardAd();
		}

		#endif

		#endregion
	}
}
