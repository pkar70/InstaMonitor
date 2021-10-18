
Imports Windows.Media.Casting
Imports Windows.Storage.Streams
Imports Windows.System.UserProfile

Module InstaModule

    Dim msLastHtmlPage As String = ""

    Public Function ExtractInstaDataFromPage(sPage As String) As JSONinstagram

        Try

            If sPage.Contains("This Account is Private") Then
                ' pewnie nic nie bedzie, ale moze zawsze tak jest?
                Dim alamakota As Integer = 1
            End If

            Dim iInd As Integer = sPage.IndexOf(">window._sharedData")
            If iInd < 0 Then Return Nothing
            iInd = sPage.IndexOf("{", iInd)
            If iInd < 0 Then Return Nothing
            sPage = sPage.Substring(iInd)
            iInd = sPage.IndexOf(";</script>")
            If iInd < 0 Then Return Nothing
            sPage = sPage.Substring(0, iInd)

            Dim oJSON As JSONinstagram = Newtonsoft.Json.JsonConvert.DeserializeObject(sPage, GetType(JSONinstagram))
            Return oJSON

        Catch ex As Exception
            CrashMessageAdd("ExtractInstaDataFromPage", ex, True)
        End Try

        Return Nothing

    End Function

    Public Async Function ReadInstagramData(sChannel As String, oTBox As TextBlock) As Task(Of JSONinstagram)

        Dim sChannelUri As String = sChannel
        If Not sChannel.ToLower.Contains("instagram") Then
            sChannelUri = "https://www.instagram.com/" & sChannel
        End If

        Try
            Dim bMsg As Boolean = False
            If oTBox IsNot Nothing Then bMsg = True
            Dim sPage As String = Await HttpPageAsync(sChannelUri, sChannel, bMsg) ' , "", True) ' true - reset HttpClient
            If sPage = "" Then Return Nothing
            msLastHtmlPage = sPage

            If sPage.Contains("RecaptchaChallengeForm") Then
                If oTBox IsNot Nothing Then Await DialogBoxAsync("Got RecaptchaChallengeForm")
                Return Nothing
            End If

            If sPage.Contains("This Account is Private") Then
                If oTBox IsNot Nothing Then Await DialogBoxAsync(sChannel & " is private - trzeba follołować")
                Return Nothing
            End If

            Dim oTmp As JSONinstagram = ExtractInstaDataFromPage(sPage)
            If oTmp IsNot Nothing Then Return oTmp

            If oTBox IsNot Nothing Then Await DialogBoxAsync("Bad response from channel " & sChannel)
            Return Nothing

        Catch ex As Exception
            CrashMessageAdd("ReadInstagramData", ex, True)
        End Try
        Return Nothing

    End Function

    Public Async Function GetPicRootDirAsync() As Task(Of Windows.Storage.StorageFolder)
        Try
            Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.KnownFolders.PicturesLibrary
            oFold = Await oFold.CreateFolderAsync("InstaMonitor", Windows.Storage.CreationCollisionOption.OpenIfExists)
            Return oFold
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Public _kanaly As List(Of LocalChannel)  ' public dla wersji z RefreshWebView: kod na tamtej podstronie

    Public Function GetInstaChannelsList() As List(Of LocalChannel)
        Return _kanaly
    End Function

    Public Function GetInstaChannelItem(sChannel As String) As LocalChannel
        For Each oChannel As LocalChannel In _kanaly
            If oChannel.sChannel.ToLower = sChannel.ToLower Then Return oChannel
        Next

        Return Nothing
    End Function

    Public Async Function TryAddChannelAsync(sNewChannel As String, oTBox As TextBlock) As Task(Of String)
        If Not Await LoadChannelsAsync() Then
            Return "ERROR cannot load channel list"
        End If

        For Each oItem As LocalChannel In _kanaly
            If oItem.sChannel.ToLower = sNewChannel.ToLower Then
                Return "Taki kanal juz istnieje"
            End If
        Next

        Dim user As JSONinstaUser
#If False Then
        ' wersja poprzednia, anonymous
        Dim oJSON As JSONinstagram = Await ReadInstagramData(sNewChannel, False)

        If oJSON Is Nothing Then
            Return "Nie ma takiego kanału, albo błąd danych ('" & sNewChannel & "')"
        End If

        user = oJSON.entry_data?.ProfilePage?.ElementAt(0)?.graphql?.user
        If user Is Nothing Then
            Return "ProfilePage jest empty, pewnie konieczny login ('" & sNewChannel & "')"
        End If
#Else
        If oTBox IsNot Nothing Then oTBox.Text = "Logging..."
        If Not Await InstaNugetLoginAsync(oTBox) Then Return "Interactive login required"
        If oTBox IsNot Nothing Then oTBox.Text = "Getting data..."
        user = Await InstaNugetGetUserDataAsync(sNewChannel)
        If user Is Nothing Then Return "Nie mogę sprawdzić info o kanale, może go nie ma?"
        If oTBox IsNot Nothing Then oTBox.Text = "Following..."
        Await InstaNugetFollowAsync(user.id, oTBox)    ' i od razu robi follow
#End If

        Await AddChannelMain(sNewChannel, user)

        Return "OK, channel added"

    End Function

    Private Async Function AddChannelMain(sNewChannel As String, user As JSONinstaUser) As Task
        ' mamy więc dane kanału, dodajemy do listy
        Dim oNew As New LocalChannel With
            {
            .sChannel = sNewChannel,
            .sDirName = sNewChannel,
            .bEnabled = True,
            .sAdded = Date.Now.ToString("yyyy.MM.dd"),
            .sDisplayName = sNewChannel
            }

        If user IsNot Nothing Then
            oNew.sBiografia = user.biography
            oNew.sFullName = user.full_name
            oNew.iUserId = user.id
        End If

        _kanaly.Add(oNew)

        Await SaveChannelsAsync()

    End Function

    Public Async Function AddChannelFromRemote(sNewChannel As String, oTBox As TextBlock) As Task(Of String)

        Dim sRetMsg As String = Await TryAddChannelAsync(sNewChannel, oTBox)
        Return sRetMsg

    End Function


    Public Sub ZrobDymkiKanalow()

        For Each oChann As LocalChannel In _kanaly
            Dim sTmp As String
            sTmp = oChann.sChannel & vbCrLf
            If oChann.sAdded <> "" Then sTmp += "added: " & oChann.sAdded
            If oChann.iPicCnt > 0 Then sTmp += vbCrLf & "pic count: " & oChann.iPicCnt
            If oChann.iNewCnt > 0 Then sTmp += vbCrLf & "new pics count: " & oChann.iNewCnt
            If oChann.sFirstError <> "" Then sTmp += vbCrLf & "errors since: " & oChann.sFirstError

            oChann.sDymek = sTmp

        Next
    End Sub

    Public Async Function LoadChannelsAsync() As Task(Of Boolean)

        If _kanaly IsNot Nothing Then
            If _kanaly.Count > 1 Then Return True
        End If

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.LocalFolder.ReadAllTextFromFileAsync("channels.json")

        If String.IsNullOrEmpty(sTxt) Then
            _kanaly = New List(Of LocalChannel)
            Return False
        End If

        _kanaly = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(List(Of LocalChannel)))

        ZrobDymkiKanalow()

        Return True

        'Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder
        'Dim oFile As Windows.Storage.StorageFile = Await oFold.TryGetItemAsync("channels.json")
        'If oFile Is Nothing Then
        '    _kanaly = New List(Of LocalChannel)
        '    Return False
        'Else
        '    Dim sTxt As String = File.ReadAllText(oFile.Path)
        '    _kanaly = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(List(Of LocalChannel)))
        '    Return True
        'End If
    End Function

    Public Async Function SaveChannelsAsync() As Task(Of Boolean)
        If _kanaly.Count < 1 Then Return False

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(_kanaly, Newtonsoft.Json.Formatting.Indented)

        Dim oFile As Windows.Storage.StorageFile = Await oFold.TryGetItemAsync("channels.bak")
        If oFile IsNot Nothing Then Await oFile.DeleteAsync()
        oFile = Await oFold.TryGetItemAsync("channels.json")
        If oFile IsNot Nothing Then Await oFile.RenameAsync("channels.bak")

        Await oFold.WriteAllTextToFileAsync("channels.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        Return True
    End Function

    Public Async Function GetChannelDir(oChannel As LocalChannel, oTBox As TextBlock) As Task(Of Windows.Storage.StorageFolder)
        Dim oFoldRoot As Windows.Storage.StorageFolder = Await GetPicRootDirAsync()
        If oFoldRoot Is Nothing Then
            If oTBox IsNot Nothing Then Await DialogBoxAsync("Cannot get folder for pictures")
            Return Nothing
        End If

        Dim oFold As Windows.Storage.StorageFolder = Await oFoldRoot.TryGetItemAsync(oChannel.sDirName.Trim)
        If oFold IsNot Nothing Then Return oFold

        ' skoro nie ma, to zakladamy
        Try
            oFold = Await oFoldRoot.CreateFolderAsync(oChannel.sDirName, Windows.Storage.CreationCollisionOption.OpenIfExists)
        Catch
            oFold = Nothing
        End Try

        If oFold Is Nothing Then
            If oTBox IsNot Nothing Then Await DialogBoxAsync("Cannot get folder for pictures (" & oChannel.sDirName & ")")
            Return Nothing
        End If

        Dim sTxt As String = "Instagram user info" & vbCrLf & vbCrLf
        sTxt = sTxt & "User name: " & oChannel.sChannel & vbCrLf
        sTxt = sTxt & "Full name: " & oChannel.sFullName & vbCrLf & vbCrLf
        sTxt = sTxt & "Biografia: " & vbCrLf & oChannel.sBiografia

        Await oFold.WriteAllTextToFileAsync("userinfo.txt", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        'Dim sFileName As String = oFold.Path & "\userinfo.txt"
        'File.WriteAllText(sFileName, sTxt) ' UTF-8, overwrite

        Return oFold
    End Function

    Private Async Function LoadPictData(oFold As Windows.Storage.StorageFolder) As Task(Of List(Of LocalPictureData))

        Dim sTxt As String = Await oFold.ReadAllTextFromFileAsync("pictureData.json")
        If String.IsNullOrEmpty(sTxt) Then
            Return New List(Of LocalPictureData)
        Else
            Try
                Return Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(List(Of LocalPictureData)))
            Catch ex As Exception

            End Try
            DialogBox("Error reading pictureData")
            Return Nothing
        End If
    End Function

    Private Async Function DownloadPictureAsync(oFold As Windows.Storage.StorageFolder, sUrl As String) As Task(Of String)

        Dim iInd As Integer = sUrl.IndexOf("?")
        Dim sLocalFileName As String

        If iInd < 0 Then
            Await DialogBoxAsync("ERROR in DownloadPictureAsyn, iInd<0 " & vbCrLf & sUrl)
            Return ""
        End If

        sLocalFileName = sUrl.Substring(0, iInd)
        iInd = sLocalFileName.LastIndexOf("/")
        sLocalFileName = sLocalFileName.Substring(iInd + 1)

        Dim oResp As Windows.Web.Http.HttpResponseMessage = Nothing
        Dim oHttp As Windows.Web.Http.HttpClient = New Windows.Web.Http.HttpClient

        Dim sError As String = ""
        Try
            oResp = Await oHttp.GetAsync(New Uri(sUrl))
            If oResp.StatusCode > 290 Then Return ""
        Catch ex As Exception
            sError = ex.Message
        End Try

        If sError <> "" Then
            Await DialogBoxAsync("ERROR in DownloadPictureAsyn, while getting page " & vbCrLf & sUrl)
            Return ""
        End If


        Dim oFile As Windows.Storage.StorageFile = Nothing
        Dim oStream As Stream = Nothing
        Try
            oFile = Await oFold.CreateFileAsync(sLocalFileName, Windows.Storage.CreationCollisionOption.ReplaceExisting)
            oStream = Await oFile.OpenStreamForWriteAsync()
            Await oResp.Content.WriteToStreamAsync(oStream.AsOutputStream)
        Catch ex As Exception
            sError = ex.Message
        End Try

        If sError <> "" Then
            Await DialogBoxAsync("ERROR in DownloadPictureAsyn, while saving to " & vbCrLf & sLocalFileName)
        End If

        oStream?.Flush()
        oStream?.Dispose()
        Return sLocalFileName
    End Function

    Public Async Function LoadPictData(oChannel As LocalChannel, oTBox As TextBlock) As Task(Of List(Of LocalPictureData))
        Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oChannel, oTBox)
        If oFold Is Nothing Then Return Nothing

        ' list of pictures (with data)
        Dim oPicList As List(Of LocalPictureData) = Await LoadPictData(oFold)
        Return oPicList
    End Function

    Public Async Function SavePictData(oChannel As LocalChannel, oPicList As List(Of LocalPictureData), oTBox As TextBlock) As Task
        Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oChannel, oTBox)
        If oFold Is Nothing Then Return

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(From c In oPicList Select c Distinct, Newtonsoft.Json.Formatting.Indented)

        ' Dim oFile As Windows.Storage.StorageFile = Await oFold.CreateFileAsync("pictureData.json")
        Await oFold.WriteAllTextToFileAsync("pictureData.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

    End Function

    Public Async Function GetInstagramFeedFromJSON(oJSON As JSONinstagram, oChannel As LocalChannel, oTBox As TextBlock) As Task(Of Integer)

        Try

            ' folder for pictures
            Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oChannel, oTBox)
            If oFold Is Nothing Then Return -1

            ' list of pictures (with data)
            Dim oPicList As List(Of LocalPictureData) = Await LoadPictData(oFold)
            Dim bPicListDirty As Boolean = False

            Dim oJUser As JSONinstaUser = oJSON?.entry_data?.ProfilePage?.ElementAt(0)?.graphql?.user

            If oJUser Is Nothing Then
                If oTBox IsNot Nothing Then Await DialogBoxAsync("Bad JSONinstaUser" & vbCrLf & oChannel.sChannel)
                Return -1
            End If

            Dim sLastGuid As String = oChannel.sLastId
            Dim bFirst As Boolean = True
            Dim iNewPicCount As Integer = 0

            If oJUser?.edge_owner_to_timeline_media?.edges Is Nothing Then
                If oTBox IsNot Nothing Then Await DialogBoxAsync("Bad JSONinstaUser..edges" & vbCrLf & oChannel.sChannel)
                Return -1
            End If

            For Each oEdge As JSONinstaPicEdge In oJUser.edge_owner_to_timeline_media.edges
                Dim oItem As JSONinstaPicNode = oEdge?.node

                If oItem Is Nothing Then
                    If oTBox IsNot Nothing Then Await DialogBoxAsync("Bad oItem" & vbCrLf & oChannel.sChannel)
                    Continue For
                End If

                If oItem.id = sLastGuid Then Exit For
                If bFirst Then
                    oChannel.sLastId = oItem.id
                    bFirst = False
                End If

                Dim oNew As LocalPictureData = New LocalPictureData
                oNew.iTimestamp = oItem.taken_at_timestamp
                oNew.sPlace = oItem.location?.name
                oNew.sCaptionAccessib = oItem.accessibility_caption
                oNew.sData = DateTime.Now.ToString("yyyy-MM-dd")
                oNew.sCaption = ""
                If oItem?.edge_media_to_caption?.edges IsNot Nothing Then
                    For Each oCapt As JSONinstaNodeCaption In oItem.edge_media_to_caption.edges
                        oNew.sCaption = oNew.sCaption & oCapt.node.text & vbCrLf
                    Next
                End If

                oNew.sFileName = Await DownloadPictureAsync(oFold, oItem.display_url)
                If oNew.sFileName = "" Then
                    If oTBox IsNot Nothing Then Await DialogBoxAsync("Cannot download picture from channel" & vbCrLf & oChannel.sChannel)
                Else
                    ' tylko gdy dodany został jakiś obrazek
                    bPicListDirty = True
                    iNewPicCount += 1
                    oPicList.Insert(0, oNew)

                    '' aktualizacja listy nowości
                    'Dim oNewPicData As LocalNewPicture = New LocalNewPicture
                    'oNewPicData.oPicture = oNew
                    'oNewPicData.oChannel = oChannel
                    'App._gNowosci.Add(oNewPicData)

                End If
            Next

            If Not bPicListDirty Then Return 0

            Await SavePictData(oChannel, oPicList, oTBox)
            'Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(From c In oPicList Select c Distinct, Newtonsoft.Json.Formatting.Indented)
            'Await oFold.WriteAllTextToFileAsync("pictureData.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)


            Return iNewPicCount

        Catch ex As Exception
            CrashMessageAdd("GetInstagramFeedFromJSON", ex, True)
        End Try

        Return -1   ' signal error
    End Function



    Public Async Function GetInstagramFeed(oChannel As LocalChannel, oTBox As TextBlock) As Task(Of Integer)
        ' RET: -1: error, 0: nic nowego, 1: są nowe
        ' sprawsdzic czy zmiana sie dokonuje w oChannel piętro wyżej!

        Dim oJSON As JSONinstagram = Await ReadInstagramData(oChannel.sChannel, oTBox)
        If oJSON Is Nothing Then
            ' If bShowMsg Then Await DialogBoxAsync("Bad response from channel" & vbCrLf & oChannel.sChannel)
            Return -1
        End If

        Return Await GetInstagramFeedFromJSON(oJSON, oChannel, oTBox)
    End Function

    Public Async Function GetAllFeedsAsync(oTB As TextBlock) As Task(Of Boolean)
        If Not Await LoadChannelsAsync() Then
            If oTB IsNot Nothing Then Await DialogBoxAsync("Empty channel list")
            Return False
        End If

        ProgRingShow(True, False, 0, _kanaly.Count)

        Dim bChannelsDirty As Boolean = False
        Dim lsToastErrors As List(Of String) = New List(Of String)
        Dim lsToastNews As List(Of String) = New List(Of String)
        Dim iNewsCount As Integer = 0

        Dim iErrCntToStop As Integer = 10

        For Each oItem As LocalChannel In From c In _kanaly Order By c.sChannel
            ProgRingInc()
            If Not oItem.bEnabled Then Continue For

            If oTB IsNot Nothing Then oTB.Text = oItem.sChannel
            Dim iRet As Integer = Await GetInstagramFeed(oItem, Nothing) ' oTB IsNot Nothing
            Await Task.Delay(2000)  ' bylo 500, ale to przestaje dzialac chyba po 3..4, więc zwiekszam (2020.11.29)
            If iRet < 0 Then
                oItem.iNewCnt = -1
                If oItem.sFirstError = "" Then oItem.sFirstError = DateTime.Now.ToString("dd MM yyyy")
                lsToastErrors.Add(oItem.sChannel)
                iErrCntToStop -= 1
                If iErrCntToStop < 0 Then
                    ' skoro tak duzo bledow pod rząd, to pewnie nie ma sensu nic dalej sciagac
                    ClipPut(msLastHtmlPage)
                    If oTB IsNot Nothing Then Await DialogBoxAsync("za duzo błędów pod rząd, poddaję się; w ClipBoard ostatni HTML")
                    Exit For
                End If
            ElseIf iRet > 0 Then
                iErrCntToStop = 10
                oItem.sFirstError = ""  ' kanał już nie daje błędów
                lsToastNews.Add(oItem.sChannel & " (" & iRet & ")")
                oItem.iNewCnt = iRet
                iNewsCount += iRet
                bChannelsDirty = True
            End If
            ' zero: nothing - nic nowego, ale też bez błędu
            ' ewentualnie kiedyś sprawdzanie, że dawno nic nie było
        Next

        ' te dwie rzeczy były na koncu, ale wtedy czasem nie zapisuje (jak wylatuje) - wiec zrobmy to teraz, jakby crash byl ponizej (a nie w samym sciaganiu)
        SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
        If bChannelsDirty Then Await SaveChannelsAsync()

        ProgRingShow(False)

        Await PrzygotujPokazInfo(lsToastErrors, lsToastNews, iNewsCount, oTB)
        ' przeniesione przed przygotowanie komunikatu
        'SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))

        If Not bChannelsDirty Then Return False

        ' przeniesione przed przygotowanie komunikatu
        'Await SaveChannelsAsync()

        Return True

    End Function

    Public Async Function PrzygotujPokazInfo(lsToastErrors As List(Of String), lsToastNews As List(Of String), iNewsCount As Integer, oTB As TextBlock) As Task
        ' przygotowanie informacji do pokazania
        Dim sToast As String = ""
        Dim sToastSummary As String = ""

        If lsToastErrors.Count > 0 Then
            sToast = "ERRORs:" & vbCrLf
            For Each sTmp As String In From c In lsToastErrors Order By c
                sToast = sToast & sTmp & vbCrLf
            Next
            sToastSummary = sToastSummary & "Errors: " & lsToastErrors.Count & vbCrLf
        End If
        If lsToastNews.Count > 0 Then
            If sToast <> "" Then sToast = sToast & "NEWs:" & vbCrLf
            For Each sTmp As String In From c In lsToastNews Order By c
                sToast = sToast & sTmp & vbCrLf
            Next
            sToastSummary = sToastSummary & "News: " & lsToastNews.Count & vbCrLf
        End If

        If sToast <> "" Then
            If oTB Is Nothing Then
                MakeToast(sToastSummary)
            Else
                Await DialogBoxAsync(sToastSummary)   ' podczas gdy sobie odklikuję, to on zapisuje
                ClipPut(sToast) ' do Clip idzie pełna wersja (z wymienionymi kanałami)
            End If
        End If

        If oTB IsNot Nothing Then
            oTB.Text = iNewsCount & " new pics"
        End If

    End Function

#Region "ramtinak/InstagramApiSharp"

    Private mInstaApi As InstagramApiSharp.API.IInstaApi = Nothing

    Public Async Function InstaNugetLoginAsync(oTBox As TextBlock) As Task(Of Boolean)
        If mInstaApi IsNot Nothing Then Return True

        Dim mUserLogin As InstagramApiSharp.Classes.UserSessionData = New InstagramApiSharp.Classes.UserSessionData
        mUserLogin.UserName = GetSettingsString("uiUserName", App.gmDefaultLoginName)
        mUserLogin.Password = GetSettingsString("uiPassword")

        If mUserLogin.Password = "" Then
            If oTBox IsNot Nothing Then Await DialogBoxAsync("No password set!")
            Return False
        End If

        Dim _instaApiBuilder As InstagramApiSharp.API.Builder.IInstaApiBuilder =
            InstagramApiSharp.API.Builder.InstaApiBuilder.CreateBuilder().SetUser(mUserLogin)
        mInstaApi = _instaApiBuilder.Build()

        ' 2021.09.29 bo jak nie ma ustalonego, to jest random!
        ' https://github.com/ramtinak/InstagramApiSharp/blob/master/src/InstagramApiSharp/API/Builder/InstaApiBuilder.cs
        ' if (_device == null) _device = AndroidDeviceGenerator.GetRandomAndroidDevice();
        Dim oAndroid As InstagramApiSharp.Classes.Android.DeviceInfo.AndroidDevice =
            InstagramApiSharp.Classes.Android.DeviceInfo.AndroidDeviceGenerator.GetByName(
                InstagramApiSharp.Classes.Android.DeviceInfo.AndroidDevices.HTC_ONE_PLUS)
        mInstaApi.SetDevice(oAndroid)

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalCacheFolder
        Dim oFile As Windows.Storage.StorageFile = Await oFold.TryGetItemAsync("sessionState.bin")
        If oFile IsNot Nothing Then
            '2021.09.26 ogranicznik czasu logowania
            If oFile.DateCreated.AddHours(6) < DateTime.Now Then
                If oTBox IsNot Nothing Then oTBox.Text = "Removing old sessionState..."
                Await oFile.DeleteAsync
            Else
                If oTBox IsNot Nothing Then oTBox.Text = "Reading sessionState..."
                Dim oStreamIn As Stream = Await oFile.OpenStreamForReadAsync
                Dim oReader = New StreamReader(oStreamIn)
                mInstaApi.LoadStateDataFromString(oReader.ReadToEnd())
                oReader.Dispose()
                oStreamIn.Dispose()
            End If
        End If

        If Not mInstaApi.IsUserAuthenticated Then
            If oTBox Is Nothing Then
                mInstaApi = Nothing
                Return False
            End If

            '// Call this function before calling LoginAsync
            If oTBox IsNot Nothing Then oTBox.Text = "SendRequestsBeforeLoginAsync..."
            Await mInstaApi.SendRequestsBeforeLoginAsync()
            '// wait 5 seconds
            Await Task.Delay(5000)

            If oTBox IsNot Nothing Then oTBox.Text = "LoginAsync..."
            Dim logInResult = Await mInstaApi.LoginAsync()
            If Not logInResult.Succeeded Then

                If logInResult.Value <> InstagramApiSharp.Classes.InstaLoginResult.ChallengeRequired Then
                    Await DialogBoxAsync("Cannot login! reason:  " & logInResult.Value.ToString)
                    mInstaApi = Nothing
                    Return False
                End If

                ' czyli nie udane zalogowanie, i chcą challenge
                Dim bEmail As Boolean = Await DialogBoxYNAsync("Challenge musi być, jakie chcesz?", "email", "SMS")
                Dim challMethods = Await mInstaApi.GetChallengeRequireVerifyMethodAsync()
                If Not challMethods.Succeeded Then
                    Await DialogBoxAsync("Cannot get challenge methods! reason: " & challMethods.Value.ToString)
                    mInstaApi = Nothing
                    Return False
                End If

                If bEmail Then
                    Dim emailChall = Await mInstaApi.RequestVerifyCodeToEmailForChallengeRequireAsync()
                    If Not emailChall.Succeeded Then
                        Await DialogBoxAsync("Cannot get email challenge! reason: " & emailChall.Value.ToString)
                        mInstaApi = Nothing
                        Return False
                    End If

                Else
                    Dim smsChall = Await mInstaApi.RequestVerifyCodeToSMSForChallengeRequireAsync()
                    If Not smsChall.Succeeded Then
                        Await DialogBoxAsync("Cannot get SMS challenge! reason: " & smsChall.Value.ToString)
                        mInstaApi = Nothing
                        Return False
                    End If
                End If

                Dim smsCode As String = Await DialogBoxInputDirectAsync("Podaj kod:")
                If smsCode = "" Then
                    Await DialogBoxAsync("No to nie bedzie loginu (bez challenge ani rusz)")
                    mInstaApi = Nothing
                    Return False
                End If

                Dim challResp = Await mInstaApi.VerifyCodeForChallengeRequireAsync(smsCode)
                If Not challResp.Succeeded Then
                    Await DialogBoxAsync("Challenge error! reason: " & challResp.Value.ToString)
                    mInstaApi = Nothing
                    Return False
                End If


            End If
        End If


        Dim sNewState As String = mInstaApi.GetStateDataAsString()
        oFile = Await oFold.CreateFileAsync("sessionState.bin", Windows.Storage.CreationCollisionOption.ReplaceExisting)
        Await oFile.WriteAllTextAsync(sNewState)

        Return True
    End Function

    Public Async Function InstaNugetGetUserDataAsync(sChannel As String) As Task(Of JSONinstaUser)
        If mInstaApi Is Nothing Then Return Nothing
        If Not mInstaApi.IsUserAuthenticated Then Return Nothing

        Try
            Dim userInfo = Await mInstaApi.UserProcessor.GetUserInfoByUsernameAsync(sChannel)
            If Not userInfo.Succeeded Then Return Nothing
            Dim oNew As JSONinstaUser = New JSONinstaUser
            oNew.biography = userInfo.Value.Biography
            oNew.full_name = userInfo.Value.FullName
            oNew.id = userInfo.Value.Pk
            Return oNew
        Catch ex As Exception
            Return Nothing
        End Try

    End Function

    Public Async Function InstaNugetFollowAsync(iUserId As Long, oTBox As TextBlock) As Task(Of Boolean)
        If mInstaApi Is Nothing Then Return False
        If Not mInstaApi.IsUserAuthenticated Then Return False

        Try
            Dim oRes = Await mInstaApi.UserProcessor.FollowUserAsync(iUserId)
            If oRes.Succeeded Then Return True

            If oTBox IsNot Nothing Then Await DialogBoxAsync("Error trying to Follow")

        Catch ex As Exception
        End Try
        Return False
    End Function

    Private Function InstaNugetGetCurrentUserId() As Long
        If mInstaApi Is Nothing Then Return 0
        If Not mInstaApi.IsUserAuthenticated Then Return 0

        If mInstaApi.GetLoggedUser.LoggedInUser.Pk > 0 Then Return mInstaApi.GetLoggedUser.LoggedInUser.Pk

        'Dim oCurrUsers = Await mInstaApi.GetLoggedUser.LoggedInUser  ' .GetCurrentUserAsync()
        'If Not oCurrUsers.Succeeded Then Return 0

        'Return oCurrUsers.Value.Pk
        Return 0
    End Function

    Public Async Function InstaNugetGetFollowingi(oTBox As TextBlock) As Task(Of List(Of LocalChannel))
        Dim oRet As List(Of LocalChannel) = New List(Of LocalChannel)

        Dim iUserId As Long = GetSettingsLong("instagramUserId")
        If iUserId = 0 Then
            iUserId = InstaNugetGetCurrentUserId()
            If iUserId = 0 Then
                If oTBox IsNot Nothing Then Await DialogBoxAsync("ERROR: nie moge dostac sie do currentUserId")
                Return Nothing
            End If
            SetSettingsLong("instagramUserId", iUserId)
        End If

        Dim oPaging As InstagramApiSharp.PaginationParameters = InstagramApiSharp.PaginationParameters.MaxPagesToLoad(20)   ' przy 10 jest chyba OK, ale warto miec rezerwę
        Dim sSearchQuery As String = ""
        Dim oRes = Await mInstaApi.UserProcessor.GetUserFollowingByIdAsync(iUserId, oPaging, sSearchQuery)
        If Not oRes.Succeeded Then Return Nothing

        If oRes.Value Is Nothing Then Return Nothing

        For Each oFoll As InstagramApiSharp.Classes.Models.InstaUserShort In oRes.Value
            Dim bMam As Boolean = False
            For Each oItem As LocalChannel In _kanaly
                If oItem.sChannel.ToLower = oFoll.UserName.ToLower Then
                    If oItem.iUserId < 10 Then oItem.iUserId = oFoll.Pk
                    oRet.Add(oItem)
                    bMam = True
                End If
            Next
            If Not bMam Then
                If oTBox Is Nothing Then Continue For
                If Not Await DialogBoxYNAsync("Following '" & oFoll.UserName & "' - nie ma kanału, dodać?") Then
                    Continue For
                End If
                Dim sAdded As String = Await TryAddChannelAsync(oFoll.UserName, oTBox)
                If Not sAdded.StartsWith("OK") Then Continue For
            End If
        Next

        Return oRet

    End Function

    Public Async Function InstaNugetRefreshAll(oTB As TextBlock) As Task(Of Integer)
        Dim bMsg As Boolean = False
        If oTB IsNot Nothing Then bMsg = True

        ProgRingShow(True)
        Dim bLoadOk As Boolean = Await LoadChannelsAsync()
        ProgRingShow(False)

        If Not bLoadOk Then
            If bMsg Then Await DialogBoxAsync("Empty channel list")
            Return False
        End If

        ' sprawdzamy tylko followingi - omijając w ten sposób mechanizm blokowania kanałów z aplikacji, jako że teraz to jest rozsynchronizowane
        ProgRingShow(True)
        If Not Await InstaNugetLoginAsync(oTB) Then Return -1

        Dim oFollowingi As List(Of LocalChannel) = Await InstaNugetGetFollowingi(oTB)
        ProgRingShow(False)
        If oFollowingi Is Nothing Then Return -1

        ' ponizsza czesc bedzie wspolna dla RefreshAll (wedle Following), oraz periodycznego (wedle InstaNugetGetRecentActivity)
        Dim bChannelsDirty As Boolean = False
        Dim lsToastErrors As List(Of String) = New List(Of String)
        Dim lsToastNews As List(Of String) = New List(Of String)
        Dim iNewsCount As Integer = 0

        Dim iErrCntToStop As Integer = 10

        ProgRingShow(True, False, 0, oFollowingi.Count)
        For Each oChannel As LocalChannel In From c In oFollowingi Order By c.sChannel
            ProgRingInc()

            If oTB IsNot Nothing Then oTB.Text = oChannel.sChannel
            Dim iRet As Integer = Await InstaNugetCheckNewsFromUserAsync(oChannel, oTB)   ' w serii, wiec bez czekania na klikanie błędów
            Await Task.Delay(3000)  ' tak samo jak w wersji anonymous, jednak czekamy troche, nawet więcej (3 nie 2 sek) - i tak idziemy tylko po follow, nieistniejące są usunięte automatycznie
            If iRet < 0 Then
                oChannel.iNewCnt = -1
                If oChannel.sFirstError = "" Then oChannel.sFirstError = DateTime.Now.ToString("dd MM yyyy")
                lsToastErrors.Add(oChannel.sChannel)
                iErrCntToStop -= 1
                If iErrCntToStop < 0 Then
                    ' skoro tak duzo bledow pod rząd, to pewnie nie ma sensu nic dalej sciagac
                    ClipPut(msLastHtmlPage)
                    If oTB IsNot Nothing Then Await DialogBoxAsync("za duzo błędów pod rząd, poddaję się; w ClipBoard ostatni HTML")
                    Exit For
                End If
                bChannelsDirty = True
            ElseIf iRet > 0 Then
                iErrCntToStop = 10
                oChannel.sFirstError = ""  ' kanał już nie daje błędów
                lsToastNews.Add(oChannel.sChannel & " (" & iRet & ")")
                oChannel.iNewCnt = iRet
                iNewsCount += iRet
                bChannelsDirty = True
            End If
            ' zero: nothing - nic nowego, ale też bez błędu
            ' ewentualnie kiedyś sprawdzanie, że dawno nic nie było
        Next

        ' te dwie rzeczy były na koncu, ale wtedy czasem nie zapisuje (jak wylatuje) - wiec zrobmy to teraz, jakby crash byl ponizej (a nie w samym sciaganiu)
        SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
        If bChannelsDirty Then Await SaveChannelsAsync()

        ProgRingShow(False)

        Await PrzygotujPokazInfo(lsToastErrors, lsToastNews, iNewsCount, oTB)
        ' przeniesione przed przygotowanie komunikatu
        'SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))

        If Not bChannelsDirty Then Return False

        Return True

    End Function

    ' wersja 2, teoretycznie szybsza: foreach(user in getRecentActivity) do checkUserActivity()
    ' wersja 3, podobna do powyzej: foreach(activity in getRecentActivity) do sciagnijObrazek
    ' ale co jak jest kilka obrazkow po kolei w jednym? to jest ten story?
    ' ile wpisów jest na page? 12, jak przy user (ze nigdy wiecej nie pokazalo obrazkow niz 12?)
    ' to może być na timer, np. godzinny
    Public Async Function InstaNugetGetRecentActivity() As Task
        If mInstaApi Is Nothing Then Return
        If Not mInstaApi.IsUserAuthenticated Then Return

        Dim oPaging As InstagramApiSharp.PaginationParameters = InstagramApiSharp.PaginationParameters.MaxPagesToLoad(10)
        Dim oRes = Await mInstaApi.UserProcessor.GetFollowingRecentActivityFeedAsync(oPaging)

        ' *TODO*

    End Function


    Public Async Function InstaNugetCheckNewsFromUserAsync(oChannel As LocalChannel, oTBox As TextBlock) As Task(Of Integer)
        Try

            ' folder for pictures
            Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oChannel, oTBox)
            If oFold Is Nothing Then Return -1

            Dim bRet As Boolean
            If oTBox IsNot Nothing Then ProgRingShow(True, False, 0, 0)
            bRet = Await InstaNugetLoginAsync(oTBox)
            If oTBox IsNot Nothing Then ProgRingShow(False)
            If Not bRet Then Return Nothing

            ' mozna id wziąć z oItem
            If oChannel.iUserId < 10 Then
                If oTBox IsNot Nothing Then ProgRingShow(True, False, 0, 0)
                Dim userInfo = Await mInstaApi.UserProcessor.GetUserInfoByUsernameAsync(oChannel.sChannel)
                If oTBox IsNot Nothing Then ProgRingShow(False)
                If Not userInfo.Succeeded Then Return Nothing
                oChannel.iUserId = userInfo.Value.Pk
                ' kanały są do późniejszego ZAPISU!! choćby dlatego że lastId sie zmienia
            End If

            If oTBox IsNot Nothing Then ProgRingShow(True, False, 0, 0)
            Dim userFullInfo = Await mInstaApi.UserProcessor.GetFullUserInfoAsync(oChannel.iUserId)
            If oTBox IsNot Nothing Then ProgRingShow(False)
            If Not userFullInfo.Succeeded Then Return Nothing

            ' przetworzenie listy z FEED
            Dim oWpisy As List(Of InstagramApiSharp.Classes.Models.InstaMedia) = userFullInfo.Value?.Feed?.Items
            If oWpisy Is Nothing Then
                If oTBox IsNot Nothing Then Await DialogBoxAsync("Bad List(Of InstaMedia)" & vbCrLf & oChannel.sChannel)
                Return -1
            End If

            ' list of pictures (with data)
            If oTBox IsNot Nothing Then ProgRingShow(True, False, 0, 0)
            Dim oPicList As List(Of LocalPictureData) = Await LoadPictData(oFold)
            If oTBox IsNot Nothing Then ProgRingShow(False)
            Dim bPicListDirty As Boolean = False

            Dim sLastGuid As String = oChannel.sLastId
            Dim bFirst As Boolean = True
            Dim iNewPicCount As Integer = 0

            For Each oMedia As InstagramApiSharp.Classes.Models.InstaMedia In oWpisy

                If oMedia.Images Is Nothing Then Continue For
                If oMedia.Images.Count < 1 Then Continue For

                Dim oPic = oMedia.Images.ElementAt(0) ' teoretycznie pierwszy jest najwiekszy, ale...
                For Each oPicLoop In oMedia.Images
                    If oPicLoop.Height > oPic.Height Then oPic = oPicLoop
                Next

                If oMedia.Pk = sLastGuid Then Exit For
                If bFirst Then
                    oChannel.sLastId = oMedia.Pk
                    bFirst = False
                End If

                Dim oNew As LocalPictureData = New LocalPictureData
                oNew.iTimestamp = oMedia.TakenAtUnix
                If oMedia.Location IsNot Nothing Then oNew.sPlace = oMedia.Location?.Name
                ' oNew.sCaptionAccessib = oItem.accessibility_caption ' tego pola nie ma?
                oNew.sData = DateTime.Now.ToString("yyyy-MM-dd") ' data wczytania obrazka, nie data obrazka!
                If oMedia.Caption IsNot Nothing Then oNew.sCaption = oMedia.Caption.Text
                'If oItem?.edge_media_to_caption?.edges IsNot Nothing Then
                '    For Each oCapt As JSONinstaNodeCaption In oItem.edge_media_to_caption.edges
                '        oNew.sCaption = oNew.sCaption & oCapt.node.text & vbCrLf
                '    Next
                'End If

                oNew.sFileName = Await DownloadPictureAsync(oFold, oPic.Uri)
                If oNew.sFileName = "" Then
                    If oTBox IsNot Nothing Then Await DialogBoxAsync("Cannot download picture from channel" & vbCrLf & oChannel.sChannel)
                Else
                    ' tylko gdy dodany został jakiś obrazek
                    bPicListDirty = True
                    iNewPicCount += 1
                    oPicList.Insert(0, oNew)

                    '' aktualizacja listy nowości
                    'Dim oNewPicData As LocalNewPicture = New LocalNewPicture
                    'oNewPicData.oPicture = oNew
                    'oNewPicData.oChannel = oChannel
                    'App._gNowosci.Add(oNewPicData)

                End If
            Next

            If Not bPicListDirty Then Return 0

            Await SavePictData(oChannel, oPicList, oTBox)
            'Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(From c In oPicList Select c Distinct, Newtonsoft.Json.Formatting.Indented)
            'Await oFold.WriteAllTextToFileAsync("pictureData.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)


            Return iNewPicCount

        Catch ex As Exception
            CrashMessageAdd("GetInstagramFeedFromJSON", ex, True)
        End Try

        Return -1   ' signal error


    End Function


#End Region

End Module
