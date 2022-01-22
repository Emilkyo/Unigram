﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Services.ViewService;
using Unigram.Views;
using Unigram.Views.Host;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Foundation;
using Windows.Media;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Resources;

namespace Unigram
{
    sealed partial class App : BootStrapper
    {
        public static ShareOperation ShareOperation { get; set; }
        public static Window ShareWindow { get; set; }

        public bool IsBackground { get; private set; }

        public static ConcurrentDictionary<long, DataPackageView> DataPackages { get; } = new ConcurrentDictionary<long, DataPackageView>();

        public static AppServiceConnection Connection { get; private set; }
        public static BackgroundTaskDeferral Deferral { get; private set; }

        private ExtendedExecutionSession _extendedSession;
        private readonly MediaExtensionManager _mediaExtensionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            TLContainer.Current.Configure(out int count);

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();

            try
            {
                //_mediaExtensionManager = new MediaExtensionManager();
                //_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.Media.OpusByteStreamHandler", ".ogg", "audio/ogg");
                //_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.Media.OpusByteStreamHandler", ".oga", "audio/ogg");
            }
            catch
            {
                // User won't be able to play and record voice messages, but it still better than not being able to use the app at all.
            }

            InactivityHelper.Detected += Inactivity_Detected;

            UnhandledException += (s, args) =>
            {
                args.Handled = true;

                if (args.Exception is not NotSupportedException)
                {
                    Client.Execute(new AddLogMessage(1, "Unhandled exception:\n" + args.Exception.ToString()));
                }
            };

#if !DEBUG
            Microsoft.AppCenter.AppCenter.Start(Constants.AppCenterId,
                typeof(Microsoft.AppCenter.Analytics.Analytics),
                typeof(Microsoft.AppCenter.Crashes.Crashes));

            Microsoft.AppCenter.Crashes.Crashes.ShouldProcessErrorReport = error =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                return error.Exception is not NotSupportedException;
#pragma warning restore CS0618 // Type or member is obsolete
            };

            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("Windows",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "DeviceFamily", AnalyticsInfo.VersionInfo.DeviceFamily },
                    { "Architecture", Package.Current.Id.Architecture.ToString() }
                });

            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("Instance",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "ActiveSessions", $"{count}" },
                });

            Client.SetLogMessageCallback(0, FatalErrorCallback);

            var lastMessage = SettingsService.Current.Diagnostics.LastErrorMessage;
            if (lastMessage != null && lastMessage.Length > 0 && SettingsService.Current.Diagnostics.LastErrorVersion == Package.Current.Id.Version.Build)
            {
                SettingsService.Current.Diagnostics.LastErrorMessage = null;
                SettingsService.Current.Diagnostics.IsLastErrorDiskFull = TdException.IsDiskFullError(lastMessage);
                Microsoft.AppCenter.Crashes.Crashes.TrackError(TdException.FromMessage(lastMessage));
            }
#endif

            EnteredBackground += OnEnteredBackground;
            LeavingBackground += OnLeavingBackground;
        }

        private void FatalErrorCallback(int verbosityLevel, string message)
        {
            if (verbosityLevel == 0)
            {
                message += Environment.NewLine;
                message += "Application version: " + SettingsPage.GetVersion();
                message += Environment.NewLine;
                message += "Entered background: " + IsBackground;

                SettingsService.Current.Diagnostics.LastErrorMessage = message;
                SettingsService.Current.Diagnostics.LastErrorVersion = Package.Current.Id.Version.Build;
            }
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            IsBackground = true;
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            IsBackground = false;
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            args.Window.Activated += Window_Activated;

            //var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            //args.Window.CoreWindow.FlowDirection = flowDirectionSetting == "RTL" ? CoreWindowFlowDirection.RightToLeft : CoreWindowFlowDirection.LeftToRight;

            //Theme.Current.Initialize();
            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            base.OnWindowCreated(args);
        }

        protected override WindowContext CreateWindowWrapper(Window window)
        {
            return new TLWindowContext(window, ApplicationView.GetApplicationViewIdForWindow(window.CoreWindow));
        }

        private void Inactivity_Detected(object sender, EventArgs e)
        {
            WindowContext.Default().Dispatcher.Dispatch(() =>
            {
                var passcode = TLContainer.Current.Passcode;
                if (passcode != null)
                {
                    passcode.Lock();
                    ShowPasscode(false);
                }
            });
        }

        [ThreadStatic]
        private static bool _passcodeShown;
        public static async void ShowPasscode(bool biometrics)
        {
            if (_passcodeShown)
            {
                return;
            }

            _passcodeShown = true;

            // This is a rare case, but it can happen.
            var content = Window.Current.Content;
            if (content != null)
            {
                content.Visibility = Visibility.Collapsed;
            }

            var dialog = new PasscodePage(biometrics);
            void handler(ContentDialog s, ContentDialogClosingEventArgs args)
            {
                dialog.Closing -= handler;

                // This is a rare case, but it can happen.
                var content = Window.Current.Content;
                if (content != null)
                {
                    content.Visibility = Visibility.Visible;
                }
            }

            dialog.Closing += handler;
            var result = await dialog.ShowQueuedAsync();

            _passcodeShown = false;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            HandleActivated(Window.Current.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground);
            SettingsService.Current.Appearance.UpdateTimer();
        }

        private void HandleActivated(bool active)
        {
            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            if (aggregator != null)
            {
                aggregator.Publish(new UpdateWindowActivated(active));
            }

            var cacheService = TLContainer.Current.Resolve<ICacheService>();
            if (cacheService != null)
            {
                cacheService.Options.Online = active;
            }
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails appService && string.Equals(appService.CallerPackageFamilyName, Package.Current.Id.FamilyName))
            {
                Connection = appService.AppServiceConnection;
                Deferral = args.TaskInstance.GetDeferral();

                appService.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;
                args.TaskInstance.Canceled += (s, e) =>
                {
                    Deferral.Complete();
                };
            }
            else
            {
                var deferral = args.TaskInstance.GetDeferral();

                if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail triggerDetail)
                {
                    var data = Toast.GetData(triggerDetail);
                    if (data == null)
                    {
                        deferral.Complete();
                        return;
                    }

                    var session = TLContainer.Current.Lifetime.ActiveItem.Id;
                    if (data.TryGetValue("session", out string value) && int.TryParse(value, out int result))
                    {
                        session = result;
                    }

                    if (TLContainer.Current.TryResolve(session, out INotificationsService service))
                    {
                        await service.ProcessAsync(data);
                    }
                }

                deferral.Complete();
            }
        }

        private void AppServiceConnection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.TryGetValue("Exit", out _))
            {
                Application.Current.Exit();
            }
        }

        public override void OnInitialize(IActivatedEventArgs args)
        {
            //Locator.Configure();
            //UnigramContainer.Current.ResolveType<IGenerationService>();

            if (TLContainer.Current.Passcode.IsEnabled)
            {
                TLContainer.Current.Passcode.Lock();
                InactivityHelper.Initialize(TLContainer.Current.Passcode.AutolockTimeout);
            }
        }

        public override void OnStart(StartKind startKind, IActivatedEventArgs args)
        {
            if (TLContainer.Current.Passcode.IsLockscreenRequired)
            {
                ShowPasscode(true);
            }

            if (startKind == StartKind.Activate)
            {
                var lifetime = TLContainer.Current.Lifetime;
                var sessionId = lifetime.ActiveItem.Id;

                var id = Toast.GetSession(args);
                if (id != null)
                {
                    lifetime.ActiveItem = lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? lifetime.ActiveItem;
                }

                if (sessionId != TLContainer.Current.Lifetime.ActiveItem.Id)
                {
                    var root = Window.Current.Content as RootPage;
                    root.Switch(lifetime.ActiveItem);
                }
            }

            var navService = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId($"{TLContainer.Current.Lifetime.ActiveItem.Id}");
            var service = TLContainer.Current.Resolve<IProtoService>();
            if (service == null)
            {
                return;
            }

            var state = service.GetAuthorizationState();
            if (state == null)
            {
                return;
            }

            TLWindowContext.GetForCurrentView().SetActivatedArgs(args, navService);
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Task.Run(() => OnStartSync());
            //return Task.CompletedTask;
        }

        public override UIElement CreateRootElement(IActivatedEventArgs e)
        {
            var id = Toast.GetSession(e);
            if (id != null)
            {
                TLContainer.Current.Lifetime.ActiveItem = TLContainer.Current.Lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? TLContainer.Current.Lifetime.ActiveItem;
            }

            var sessionId = TLContainer.Current.Lifetime.ActiveItem.Id;

            if (e is ContactPanelActivatedEventArgs /*|| (e is ProtocolActivatedEventArgs protocol && protocol.Uri.PathAndQuery.Contains("domain=telegrampassport", StringComparison.OrdinalIgnoreCase))*/)
            {
                var navigationFrame = new Frame { FlowDirection = ApiInfo.FlowDirection };
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"Main{sessionId}", false) as NavigationService;

                return navigationFrame;
            }
            else
            {
                var navigationFrame = new Frame();
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"{sessionId}", true) as NavigationService;

                return new RootPage(navigationService) { FlowDirection = ApiInfo.FlowDirection };
            }
        }

        public override UIElement CreateRootElement(INavigationService navigationService)
        {
            return new StandalonePage(navigationService) { FlowDirection = ApiInfo.FlowDirection };
        }

        protected override INavigationService CreateNavigationService(Frame frame, int session, string id, bool root)
        {
            if (root)
            {
                return new TLRootNavigationService(TLContainer.Current.Resolve<ISessionService>(session), frame, session, id);
            }

            return new TLNavigationService(TLContainer.Current.Resolve<IProtoService>(session), TLContainer.Current.Resolve<IViewService>(session), frame, session, id);
        }

        private async void OnStartSync()
        {
            await RequestExtendedExecutionSessionAsync();

            //#if DEBUG
            //await VoIPConnection.Current.ConnectAsync();
            //#endif

            await Toast.RegisterBackgroundTasks();

            try
            {
                TileUpdateManager.CreateTileUpdaterForApplication("App").Clear();
            }
            catch { }

            try
            {
                ToastNotificationManager.History.Clear("App");
            }
            catch { }

#if !DEBUG
            if (SettingsService.Current.IsTrayVisible
                && Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                try
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                }
                catch
                {
                    // The app has been compiled without desktop bridge
                }
            }
#endif

            Windows.ApplicationModel.Core.CoreApplication.EnablePrelaunch(true);
        }

        private async Task RequestExtendedExecutionSessionAsync()
        {
            if (_extendedSession == null && AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
            {
                var session = new ExtendedExecutionSession();
                session.Reason = ExtendedExecutionReason.Unspecified;
                session.Revoked += ExtendedExecutionSession_Revoked;

                var result = await session.RequestExtensionAsync();
                if (result == ExtendedExecutionResult.Allowed)
                {
                    _extendedSession = session;

                    Logs.Logger.Info(Logs.LogTarget.Lifecycle, "ExtendedExecutionResult.Allowed");
                }
                else
                {
                    session.Revoked -= ExtendedExecutionSession_Revoked;
                    session.Dispose();

                    Logs.Logger.Warning(Logs.LogTarget.Lifecycle, "ExtendedExecutionResult.Denied");
                }
            }
        }

        private void ExtendedExecutionSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Logs.Logger.Warning(Logs.LogTarget.Lifecycle, "ExtendedExecutionSession.Revoked");

            if (_extendedSession != null)
            {
                _extendedSession.Dispose();
                _extendedSession = null;
            }
        }

        public override async void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Logs.Logger.Info(Logs.LogTarget.Lifecycle, "OnResuming");

            // #1225: Will this work? No one knows.
            foreach (var network in TLContainer.Current.ResolveAll<INetworkService>())
            {
                network.Reconnect();
            }

            // #2034: Will this work? No one knows.
            SettingsService.Current.Appearance.UpdateNightMode();

            await RequestExtendedExecutionSessionAsync();
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            Logs.Logger.Info(Logs.LogTarget.Lifecycle, "OnSuspendingAsync");

            TLContainer.Current.Passcode.CloseTime = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        public override INavigable ViewModelForPage(Page page, INavigationService navigationService)
        {
            return page switch
            {
                Unigram.Views.ChatsNearbyPage => TLContainer.Current.Resolve<Unigram.ViewModels.ChatsNearbyViewModel>(navigationService.SessionId),
                Unigram.Views.DiagnosticsPage => TLContainer.Current.Resolve<Unigram.ViewModels.DiagnosticsViewModel>(navigationService.SessionId),
                Unigram.Views.LogOutPage => TLContainer.Current.Resolve<Unigram.ViewModels.LogOutViewModel>(navigationService.SessionId),
                Unigram.Views.ProfilePage profile => TLContainer.Current.Resolve<Unigram.ViewModels.ProfileViewModel, Unigram.ViewModels.Delegates.IProfileDelegate>(profile, navigationService.SessionId),
                Unigram.Views.InstantPage => TLContainer.Current.Resolve<Unigram.ViewModels.InstantViewModel>(navigationService.SessionId),
                //
                Unigram.Views.MainPage => TLContainer.Current.Resolve<Unigram.ViewModels.MainViewModel>(navigationService.SessionId),
                Unigram.Views.SettingsPage settings => TLContainer.Current.Resolve<Unigram.ViewModels.SettingsViewModel, Unigram.ViewModels.Delegates.ISettingsDelegate>(settings, navigationService.SessionId),
                Unigram.Views.Users.UserCommonChatsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Users.UserCommonChatsViewModel>(navigationService.SessionId),
                Unigram.Views.Users.UserCreatePage => TLContainer.Current.Resolve<Unigram.ViewModels.Users.UserCreateViewModel>(navigationService.SessionId),
                //
                Unigram.Views.Supergroups.SupergroupAddAdministratorPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupAddAdministratorViewModel>(navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupAddRestrictedPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupAddRestrictedViewModel>(navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupAdministratorsPage supergroupAdministrators => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupAdministratorsViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupAdministrators, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupBannedPage supergroupBanned => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupBannedViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupBanned, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupEditAdministratorPage supergroupEditAdministrator => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel, Unigram.ViewModels.Delegates.IMemberDelegate>(supergroupEditAdministrator, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupEditLinkedChatPage supergroupEditLinkedChat => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupEditLinkedChat, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupEditRestrictedPage supergroupEditRestricted => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel, Unigram.ViewModels.Delegates.IMemberDelegate>(supergroupEditRestricted, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupEditStickerSetPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel>(navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupEditTypePage supergroupEditType => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditTypeViewModel, Unigram.ViewModels.Delegates.ISupergroupEditDelegate>(supergroupEditType, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupEditPage supergroupEdit => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditViewModel, Unigram.ViewModels.Delegates.ISupergroupEditDelegate>(supergroupEdit, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupMembersPage supergroupMembers => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupMembersViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupMembers, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupPermissionsPage supergroupPermissions => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupPermissionsViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupPermissions, navigationService.SessionId),
                Unigram.Views.Supergroups.SupergroupReactionsPage supergroupReactions => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupReactionsViewModel, Unigram.ViewModels.Delegates.IChatDelegate>(supergroupReactions, navigationService.SessionId),
                //
                Unigram.Views.SignIn.SignInRecoveryPage => TLContainer.Current.Resolve<Unigram.ViewModels.SignIn.SignInRecoveryViewModel>(navigationService.SessionId),
                Unigram.Views.SignIn.SignUpPage => TLContainer.Current.Resolve<Unigram.ViewModels.SignIn.SignUpViewModel>(navigationService.SessionId),
                Unigram.Views.SignIn.SignInPasswordPage => TLContainer.Current.Resolve<Unigram.ViewModels.SignIn.SignInPasswordViewModel>(navigationService.SessionId),
                Unigram.Views.SignIn.SignInSentCodePage => TLContainer.Current.Resolve<Unigram.ViewModels.SignIn.SignInSentCodeViewModel>(navigationService.SessionId),
                Unigram.Views.SignIn.SignInPage signIn => TLContainer.Current.Resolve<Unigram.ViewModels.SignIn.SignInViewModel, Unigram.ViewModels.Delegates.ISignInDelegate>(signIn, navigationService.SessionId),
                //
                Unigram.Views.Folders.FoldersPage => TLContainer.Current.Resolve<Unigram.ViewModels.Folders.FoldersViewModel>(navigationService.SessionId),
                Unigram.Views.Folders.FolderPage => TLContainer.Current.Resolve<Unigram.ViewModels.Folders.FolderViewModel>(navigationService.SessionId),
                Unigram.Views.Channels.ChannelCreateStep1Page => TLContainer.Current.Resolve<Unigram.ViewModels.Channels.ChannelCreateStep1ViewModel>(navigationService.SessionId),
                Unigram.Views.Channels.ChannelCreateStep2Page channelCreateStep2 => TLContainer.Current.Resolve<Unigram.ViewModels.Channels.ChannelCreateStep2ViewModel, Unigram.ViewModels.Delegates.ISupergroupEditDelegate>(channelCreateStep2, navigationService.SessionId),
                Unigram.Views.BasicGroups.BasicGroupCreateStep1Page => TLContainer.Current.Resolve<Unigram.ViewModels.BasicGroups.BasicGroupCreateStep1ViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsBlockedChatsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsBlockedChatsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsStickersPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsStickersViewModel>(navigationService.SessionId),
                //
                Unigram.Views.Settings.SettingsThemePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsThemeViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsPhoneSentCodePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPhoneSentCodeViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsPhonePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPhoneViewModel>(navigationService.SessionId),
                //
                Unigram.Views.Settings.SettingsAdvancedPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsAdvancedViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsAppearancePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsAppearanceViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsBackgroundsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsBackgroundsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsDataAndStoragePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsDataAndStorageViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsDataAutoPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsDataAutoViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsLanguagePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsLanguageViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsNetworkPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNetworkViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsNightModePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNightModeViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsNotificationsExceptionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsPasscodePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPasscodeViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsPasswordPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPasswordViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsPrivacyAndSecurityPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsProxiesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsProxiesViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsShortcutsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsShortcutsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsThemesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsThemesViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsWebSessionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsWebSessionsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsNotificationsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNotificationsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsSessionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsSessionsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsStoragePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsStorageViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.SettingsUsernamePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsUsernameViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowCallsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowChatInvitesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowP2PCallsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowForwardedPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowPhonePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowPhotoPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowStatusPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Password.SettingsPasswordConfirmPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordConfirmViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Password.SettingsPasswordCreatePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordCreateViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Password.SettingsPasswordDonePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordDoneViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Password.SettingsPasswordEmailPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordEmailViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Password.SettingsPasswordHintPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordHintViewModel>(navigationService.SessionId),
                Unigram.Views.Settings.Password.SettingsPasswordIntroPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordIntroViewModel>(navigationService.SessionId),
                Unigram.Views.Payments.PaymentFormPage => TLContainer.Current.Resolve<Unigram.ViewModels.Payments.PaymentFormViewModel>(navigationService.SessionId),
                Unigram.Views.Chats.MessageStatisticsPage messageStatistics => TLContainer.Current.Resolve<Unigram.ViewModels.Chats.MessageStatisticsViewModel, Unigram.ViewModels.Delegates.IChatDelegate>(messageStatistics, navigationService.SessionId),
                Unigram.Views.Chats.ChatInviteLinkPage => TLContainer.Current.Resolve<Unigram.ViewModels.Chats.ChatInviteLinkViewModel>(navigationService.SessionId),
                Unigram.Views.Chats.ChatStatisticsPage chatStatistics => TLContainer.Current.Resolve<Unigram.ViewModels.Chats.ChatStatisticsViewModel, Unigram.ViewModels.Delegates.IChatDelegate>(chatStatistics, navigationService.SessionId),
                _ => null
            };
        }
    }
}
