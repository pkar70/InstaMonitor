' The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

''' <summary>
''' An empty page that can be used on its own or navigated to within a Frame.
''' </summary>
Public NotInheritable Class RefreshWebView
    Inherits Page

    Private mbRunning As Boolean = False
    Private mMyChannels As List(Of LocalChannel) = Nothing
    Private mCurrentChannel As LocalChannel = Nothing

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        ProgRingInit(True, True)
        uiWebView.Navigate(New Uri("https://www.instagram.com/"))   ' szansa na zalogowanie
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        mbRunning = False
    End Sub

    Private Async Sub uiStart_Click(sender As Object, e As RoutedEventArgs) Handles uiStart.Click
        ' Dim sPage As String = await uiWebView.GetDocumentHtml
        ' Dim sPage As String = Await uiWebView.InvokeScriptAsync("eval", New String() {"document.documentElement.outerHTML;"})

        'Await uiWebView.InvokeScriptAsync("eval", New String() {"document.getElementsByName('username')[0].focus;"})
        'Await uiWebView.InvokeScriptAsync("eval", New String() {"window.dispatchEvent(new KeyboardEvent('keydown', {'key': 'm'}));"})

        Await uiWebView.InvokeScriptAsync("eval", New String() {"document.getElementsByName('username')[0].value='" & App.gmDefaultLoginName & "@outlook.com';"})

        Await uiWebView.InvokeScriptAsync("eval", New String() {"document.getElementsByName('password')[0].value='" & App.gmDefaulPswdName & "';"})

        Await uiWebView.InvokeScriptAsync("eval", New String() {"document.getElementById('loginForm').submit;"})


        'uiStart.Visibility = Visibility.Collapsed
        'uiWebView.Visibility = Visibility.Visible

        'If Not Await LoadChannelsAsync() Then
        '    Await DialogBoxAsync("Empty channel list")
        '    Return
        'End If

        'mMyChannels = New List(Of LocalChannel)
        'For Each oItem As LocalChannel In From c In _kanaly Order By c.sChannel
        '    mMyChannels.Add(oItem)
        'Next

        'mbRunning = True

        'ProgRingShow(True, False, 0, _kanaly.Count)

        'Await WebGoNext("")

    End Sub


    Private Async Sub uiWebView_NavigationCompleted(sender As WebView, args As WebViewNavigationCompletedEventArgs) Handles uiWebView.NavigationCompleted
        If Not mbRunning Then Return

        If mCurrentChannel Is Nothing Then Return

        'Dim sUri As String = args.Uri.LocalPath

        Await Task.Delay(1000)

        Dim iRet As Integer = Await WebExtract()

        If iRet < 0 Then
            mCurrentChannel.iNewCnt = -1
            If mCurrentChannel.sFirstError = "" Then mCurrentChannel.sFirstError = DateTime.Now.ToString("dd MM yyyy")
            lsToastErrors.Add(mCurrentChannel.sChannel)
            iErrCntToStop -= 1
            If iErrCntToStop < 0 Then
                ' skoro tak duzo bledow pod rząd, to pewnie nie ma sensu nic dalej sciagac
                Await DialogBoxAsync("za duzo błędów pod rząd, poddaję się")
                ProgRingShow(False)
                mbRunning = False

                ' te dwie rzeczy były na koncu, ale wtedy czasem nie zapisuje (jak wylatuje) - wiec zrobmy to teraz, jakby crash byl ponizej (a nie w samym sciaganiu)
                SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
                If bChannelsDirty Then Await SaveChannelsAsync()

                Return
            End If
        ElseIf iRet > 0 Then
            iErrCntToStop = 5
            mCurrentChannel.sFirstError = ""  ' kanał już nie daje błędów
            lsToastNews.Add(mCurrentChannel.sChannel & " (" & iRet & ")")
            mCurrentChannel.iNewCnt = iRet
            iNewsCount += iRet
            bChannelsDirty = True
        End If


        Await WebGoNext(mCurrentChannel.sChannel) 'bylo: sUri

    End Sub

    Private bChannelsDirty As Boolean = False
    Private lsToastErrors As List(Of String) = New List(Of String)
    Private lsToastNews As List(Of String) = New List(Of String)
    Private iNewsCount As Integer = 0

    Dim iErrCntToStop As Integer = 5
    Private Async Function WebGoNext(sName As String) As Task

        ProgRingInc()

        Dim iLoop As Integer
        Dim oItem As LocalChannel

        iLoop = 0

        If sName <> "" Then

            For iLoop = 0 To mMyChannels.Count - 1
                oItem = mMyChannels.ElementAt(iLoop)

                If Not oItem.bEnabled Then Continue For
                If oItem.sChannel = sName Then Exit For
            Next
            iLoop += 1
            If iLoop >= mMyChannels.Count Then
                ProgRingShow(False)
                mbRunning = False

                Await PrzygotujPokazInfo(lsToastErrors, lsToastNews, iNewsCount, uiMsg)

                ' te dwie rzeczy były na koncu, ale wtedy czasem nie zapisuje (jak wylatuje) - wiec zrobmy to teraz, jakby crash byl ponizej (a nie w samym sciaganiu)
                SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
                If bChannelsDirty Then Await SaveChannelsAsync()

                Return
            End If
        End If

        ' gdy wchodzimy tu bez nazwy (czyli "first"), to iLoop = 0
        ' gdy z nazwą, to iLoop = następny index po tej nazwie

        mCurrentChannel = mMyChannels.ElementAt(iLoop)
        uiMsg.Text = mCurrentChannel.sChannel

        Dim sChannel As String = mCurrentChannel.sChannel
        If Not sChannel.ToLower.Contains("instagram") Then
            sChannel = "https://www.instagram.com/" & sChannel
        End If
        uiWebView.Navigate(New Uri(sChannel))


    End Function

    Public Async Function WebExtract() As Task(Of Integer)
        ' RET: -1: error, 0: nic nowego, 1: są nowe
        ' sprawsdzic czy zmiana sie dokonuje w oChannel piętro wyżej!

        Dim sPage As String = Await uiWebView.GetDocumentHtml

        Dim oJSON As JSONinstagram = ExtractInstaDataFromPage(sPage)
        If oJSON Is Nothing Then
            Await DialogBoxAsync("Bad response from channel" & vbCrLf & mCurrentChannel.sChannel)
            Return -1
        End If

        Return Await GetInstagramFeedFromJSON(oJSON, mCurrentChannel, Nothing)

    End Function


End Class
