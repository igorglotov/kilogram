﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Windows.Phone.Networking.NetworkOperators;
using Windows.UI.Core;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Telegram.MTProto;
using Telegram.Notifications;
using Telegram.Platform;
using Telegram.UI.Flows;
using Telegram.Utils;

namespace Telegram.UI {
    public partial class SignupPhone : PhoneApplicationPage {
        public enum ScreenState {
            PhoneScreen = 0,
            CodeScreen = 1,
            NameScreen = 2
        };

        private ScreenState _currentScreenState = 0;

        private readonly TelegramSession session;
        private readonly Login flow;
        private int _timerSeconds = 60;
        private Popup _progressPopup;
        private ProgressIndicator progressIndicator;

        public SignupPhone() {
            InitializeComponent();
            session = TelegramSession.Instance;
            flow = new Login(session, "en");
            
            ShowPhoneScene();

            this.BackKeyPress += delegate {
                Application.Current.Terminate();
            };

            Login();
        }

        private async Task Login() {
            flow.NeedCodeEvent += delegate(Login login) {
                Deployment.Current.Dispatcher.BeginInvoke(
                    delegate {
                        RestartTimer();
                        ShowCodeScene();
                        HeaderBlock.Text = "Your code";
                    });

            };

            flow.WrongCodeEvent += delegate(Login login) {
                Deployment.Current.Dispatcher.BeginInvoke(
                    delegate {
                        ShowCodeScene();
                        HeaderBlock.Text = "Your code";
                        codeControl.SetCodeInvalid();
                    });
            };

            flow.NeedSignupEvent += delegate(Login login) {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    HeaderBlock.Text = "Your name";
                    ShowNameScene();
                });
            };

            flow.LoginSuccessEvent += delegate(Login login) {
                Deployment.Current.Dispatcher.BeginInvoke(
                    delegate {

                        HideProgress();
                        new NotificationManager().RegisterPushNotifications();
                        NavigationService.Navigate(new Uri("/UI/Pages/StartPage.xaml", UriKind.Relative));



                        ContactManager cm = new ContactManager();
                        Task.Run(() => cm.SyncContacts());

                        PhotoResult avatarSource = nameControl.GetAvatarSource();
                        
                        if (avatarSource != null) { 
                            Task.Run(async delegate {
                                InputFile file =
                                await TelegramSession.Instance.Files.UploadFile(avatarSource.OriginalFileName, avatarSource.ChosenPhoto, (progress) => { });
                                
                                photos_Photo photo =
                                    await
                                        TelegramSession.Instance.Api.photos_uploadProfilePhoto(file, "", TL.inputGeoPointEmpty(),
                                            TL.inputPhotoCropAuto());

                                Photos_photoConstructor photoConstructor = (Photos_photoConstructor)photo;

                                foreach (var user in photoConstructor.users) {
                                    TelegramSession.Instance.SaveUser(user);
                                }
                            });
                        }
                    });
            };

            Task.Run(() => flow.Start());
//            flow.Start();
        }

        private void nextButton_Click(object sender, RoutedEventArgs e) {
            switch (_currentScreenState) {
                case ScreenState.PhoneScreen:
                    ShowProgress("activating phone...");
                    flow.SetPhone(phoneControl.GetPhone());
                    break;
                case ScreenState.CodeScreen:
                    ShowProgress("processing code...");
                    flow.SetCode(codeControl.GetCode());
                    break;
                case ScreenState.NameScreen:
                    ShowProgress("registering user...");
                    flow.SetSignUp(nameControl.GetFirstName(), nameControl.GetLastName());
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine("Unknown screen state");
                    break;
            }
        }

        private void ShowPhoneScene() {
            Debug.WriteLine("ShowPhoneScene");
            phoneControl.Visibility = System.Windows.Visibility.Visible;
            codeControl.Visibility = System.Windows.Visibility.Collapsed;
            nameControl.Visibility = System.Windows.Visibility.Collapsed;

            _currentScreenState = ScreenState.PhoneScreen;
        }

        private void ShowCodeScene() {
            Debug.WriteLine("ShowCodeScene");
            
            GuideTextBlock.Text = "";
            GuideTextBlock.Inlines.Clear();
            GuideTextBlock.Inlines.Add(new Run() { Text = "We have sent an SMS with an activation code to your phone " });
            GuideTextBlock.Inlines.Add(new Run() { Text = Formatters.FormatPhoneNumber("+" + phoneControl.GetPhone()), FontWeight = FontWeights.SemiBold });

            HideProgress();

            if (!phoneControl.FormValid()) {
                phoneControl.PhoneNumberHinTextBlock.Foreground = new SolidColorBrush(Colors.Red);

                return;
            }

            phoneControl.Visibility = System.Windows.Visibility.Collapsed;
            codeControl.Visibility = System.Windows.Visibility.Visible;
            nameControl.Visibility = System.Windows.Visibility.Collapsed;

            _currentScreenState = ScreenState.CodeScreen;
        }

        private void RestartTimer() {
            _timerSeconds = 60;
            StartTimer();
        }

        private void HaltTimer() {
            _timerSeconds = 0;
        }

        private async void StartTimer() {
            while (_timerSeconds != 0) {
                _timerSeconds--;
                codeControl.SetTimerTime(_timerSeconds);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private void ShowProgress(string text) {
            this.IsEnabled = false;

            if (progressIndicator == null) {
                progressIndicator = new ProgressIndicator();
                SystemTray.SetProgressIndicator(this, progressIndicator);
            }

            progressIndicator.Text = text;
            progressIndicator.IsIndeterminate = true;
            progressIndicator.IsVisible = true;

        }

        private void HideProgress() {
            progressIndicator.IsVisible = false;
            this.IsEnabled = true;
        }

        private void ShowNameScene() {
            Debug.WriteLine("ShowNameScene");

            GuideTextBlock.Inlines.Clear();
            GuideTextBlock.Text = "Enter your name and add a profile picture.";

            HideProgress();
            
            phoneControl.Visibility = System.Windows.Visibility.Collapsed;
            codeControl.Visibility = System.Windows.Visibility.Collapsed;
            nameControl.Visibility = System.Windows.Visibility.Visible;

            HaltTimer();

            _currentScreenState = ScreenState.NameScreen;
        }
    }
}