﻿Imports vb14 = Vblib.pkarlibmodule14

Partial NotInheritable Class App
    Inherits Application

    Protected Async Function OnLaunchFragment(aes As ApplicationExecutionState) As Task(Of Frame)
        Dim mRootFrame As Frame = TryCast(Window.Current.Content, Frame)

        ' Do not repeat app initialization when the Window already has content,
        ' just ensure that the window is active

        If mRootFrame Is Nothing Then
            ' Create a Frame to act as the navigation context and navigate to the first page
            mRootFrame = New Frame()

            AddHandler mRootFrame.NavigationFailed, AddressOf OnNavigationFailed

            ' PKAR added wedle https://stackoverflow.com/questions/39262926/uwp-hardware-back-press-work-correctly-in-mobile-but-error-with-pc
            AddHandler mRootFrame.Navigated, AddressOf OnNavigatedAddBackButton
            AddHandler Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested, AddressOf OnBackButtonPressed

            ' Place the frame in the current Window
            Window.Current.Content = mRootFrame
        End If

        InitLib(Environment.GetCommandLineArgs.ToList)
        'Await InitPicPath()
        Return mRootFrame
    End Function

#Region "autogenerated"
    Protected Overrides Async Sub OnLaunched(e As Windows.ApplicationModel.Activation.LaunchActivatedEventArgs)
        Dim rootFrame As Frame = Await OnLaunchFragment(e.PreviousExecutionState)

        If e.PrelaunchActivated = False Then
            If rootFrame.Content Is Nothing Then
                ' When the navigation stack isn't restored navigate to the first page,
                ' configuring the new page by passing required information as a navigation
                ' parameter
                rootFrame.Navigate(GetType(MainPage), e.Arguments)
            End If

            ' Ensure the current window is active
            Window.Current.Activate()
        End If
    End Sub

    ''' <summary>
    ''' Invoked when Navigation to a certain page fails
    ''' </summary>
    ''' <param name="sender">The Frame which failed navigation</param>
    ''' <param name="e">Details about the navigation failure</param>
    Private Sub OnNavigationFailed(sender As Object, e As NavigationFailedEventArgs)
        Throw New Exception("Failed to load Page " + e.SourcePageType.FullName)
    End Sub

    ''' <summary>
    ''' Invoked when application execution is being suspended.  Application state is saved
    ''' without knowing whether the application will be terminated or resumed with the contents
    ''' of memory still intact.
    ''' </summary>
    ''' <param name="sender">The source of the suspend request.</param>
    ''' <param name="e">Details about the suspend request.</param>
    Private Sub OnSuspending(sender As Object, e As SuspendingEventArgs) Handles Me.Suspending
        Dim deferral As SuspendingDeferral = e.SuspendingOperation.GetDeferral()
        ' TODO: Save application state and stop any background activity
        deferral.Complete()
    End Sub

    ' RemoteSystems, Timer
    Protected Overrides Async Sub OnBackgroundActivated(args As BackgroundActivatedEventArgs)
        moTaskDeferal = args.TaskInstance.GetDeferral() ' w pkarmodule.App

        Dim bNoComplete As Boolean = False
        Dim bObsluzone As Boolean = False

        ' Await InitPicPath()

        If args.TaskInstance.Task.Name = "InstaMonitorTimer" Then
            ' Await App.gInstaModule.GetAllFeedsTimerAsync()
            Await GetAllFeedsTimerAsync()
            bObsluzone = True
        End If

        ' lista komend danej aplikacji
        Dim sLocalCmds As String = "add CHANNEL" & vbTab & "dodanie kanału"

        ' zwroci false gdy to nie jest RemoteSystem; gdy true, to zainicjalizowało odbieranie
        If Not bObsluzone Then bNoComplete = RemSysInit(args, sLocalCmds)

        If Not bNoComplete Then moTaskDeferal.Complete()

    End Sub

    Private Async Function AppServiceLocalCommand(sCommand As String) As Task(Of String)
        If Not sCommand.ToLower.StartsWith("add ") Then Return ""
        If sCommand.Length < 5 Then Return "ERROR: too short channel name"

        Dim sNewChannel As String = sCommand.Substring(4)

        ' Dim sResult As String = Await App.gInstaModule.AddChannelFromRemote(sNewChannel)
        Dim sResult As String = Await AddChannelFromRemote(sNewChannel, Nothing)
        If sResult.StartsWith("OK") Then
            Dim iNewCnt As Integer = vb14.GetSettingsInt("addedChannels")
            iNewCnt += 1
            vb14.SetSettingsInt("addedChannels", iNewCnt)
            If iNewCnt > 9 AndAlso iNewCnt Mod 5 = 0 Then
                MakeToast("Dużo dodanych kanałów (" & iNewCnt & ") bez Refresh")
            End If
        End If

        Return sResult
    End Function


#End Region

#Region "remote system"
    'Private moAppConn As AppService.AppServiceConnection

    'Private Sub OnTaskCanceled(sender As Background.IBackgroundTaskInstance, reason As Background.BackgroundTaskCancellationReason)
    '    If moTaskDeferal IsNot Nothing Then
    '        moTaskDeferal.Complete()
    '        moTaskDeferal = Nothing
    '    End If
    'End Sub


    'Private Async Sub OnRequestReceived(sender As AppService.AppServiceConnection, args As AppService.AppServiceRequestReceivedEventArgs)
    '    'Get a deferral so we can use an awaitable API to respond to the message 
    '    Dim messageDeferral As AppService.AppServiceDeferral = args.GetDeferral()
    '    Dim oInputMsg As ValueSet = args.Request.Message
    '    Dim oResultMsg As ValueSet = New ValueSet()
    '    Dim sResult As String = "ERROR while processing command"
    '    Try
    '        Dim sCommand As String = CType(oInputMsg("command"), String)

    '        Select Case sCommand.ToLower
    '            Case "ver"
    '                sResult = Package.Current.Id.Version.Major & "." &
    '                    Package.Current.Id.Version.Minor & "." & Package.Current.Id.Version.Build
    '            Case "add"
    '                ' odpowiednik msgboxu
    '                Dim sNewChannel As String = ""
    '                Try
    '                    sNewChannel = CType(oInputMsg("channel"), String).Trim
    '                Catch ex As Exception
    '                End Try

    '                If sNewChannel = "" Then
    '                    sResult = "ERROR empty channel name?"
    '                Else
    '                    sResult = Await AddChannelFromRemote(sNewChannel)
    '                    If sResult.StartsWith("OK") Then
    '                        Dim iNewCnt As Integer = GetSettingsInt("addedChannels")
    '                        iNewCnt += 1
    '                        SetSettingsInt("addedChannels", iNewCnt)
    '                        If iNewCnt > 9 AndAlso iNewCnt Mod 5 = 0 Then
    '                            MakeToast("Dużo dodanych kanałów (" & iNewCnt & ") bez Refresh")
    '                        End If
    '                    End If
    '                End If

    '            Case Else
    '                sResult = "ERROR unknown command"

    '        End Select
    '    Catch ex As Exception

    '    End Try

    '    ' odsylamy cokolwiek - zeby "tamta strona" cos zobaczyla
    '    oResultMsg.Add("result", CType(sResult, String))
    '    Await args.Request.SendResponseAsync(oResultMsg)

    '    messageDeferral.Complete()
    'End Sub


#End Region


    'Public Shared _gNowosci As List(Of LocalNewPicture) = New List(Of LocalNewPicture)


    ' wedle https://docs.microsoft.com/en-us/windows/uwp/design/shell/tiles-and-notifications/send-local-toast
    ' foreground activation

    Protected Overrides Async Sub OnActivated(args As IActivatedEventArgs)

        If args.Kind = ActivationKind.CommandLineLaunch Then

            Dim commandLine As CommandLineActivatedEventArgs = TryCast(args, CommandLineActivatedEventArgs)
            Dim operation As CommandLineActivationOperation = commandLine?.Operation
            Dim strArgs As String = operation?.Arguments

            InitLib(strArgs.Split(" ").ToList)
            ' App.gInstaModule.SetPicPath(Windows.Storage.KnownFolders.PicturesLibrary.Path)

            If Not String.IsNullOrEmpty(strArgs) Then
                Await ObsluzCommandLine(strArgs)
                Window.Current.Close()
                Return
            End If
        End If

        ' jesli nie cmdline (a np. toast), albo cmdline bez parametrow, to pokazujemy okno
        Dim rootFrame As Frame = Await OnLaunchFragment(args.PreviousExecutionState)

        rootFrame.Navigate(GetType(MainPage))

        Window.Current.Activate()

    End Sub


    Public Shared gInstaModule As New Vblib.InstaModule(Windows.Storage.ApplicationData.Current.LocalFolder.Path, Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path)

    'Private Shared Async Function InitPicPath() As Task
    '    Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.KnownFolders.PicturesLibrary
    '    oFold = Await oFold.GetFolderAsync("InstaMonitor")
    '    ' musi tak być, bo PicturesLibrary nie ma PATH!
    '    App.gInstaModule.SetPicPath(oFold.Path)
    'End Function


End Class
