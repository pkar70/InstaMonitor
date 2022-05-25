

Public Class InstaModule

    Const CURR_INDEX As String = "channels.json"
    Const BAK_INDEX As String = "channels.bak"
    'Const USER_INFO As String = "userinfo.txt"
    Const PICT_DATA As String = "pictureData.json"

    Dim msLastHtmlPage As String = ""
    Dim msIndexFilePathname As String = ""
    Dim msIndexFileBakPathname As String = ""
    Dim msLocalCacheFolder As String = ""
    'Dim msPictLibrPathname As String = ""


    Public Delegate Sub UItBoxSetText(sTxt As String)
    Private oTBoxText As UItBoxSetText

    '''' <summary>
    ''''  Ścieżki do katalogów brane z Windows
    '''' </summary>
    '''' <param name="sIndexDirPath">Windows.Storage.ApplicationData.Current.LocalFolder.Path</param>
    '''' <param name="sPicLibDirPath">Path,Windows.Storage.KnownFolders.PicturesLibrary.Path</param>
    Public Sub New(sIndexDirPath As String, sLocalCacheFolder As String)
        If Not IO.Directory.Exists(sIndexDirPath) Then Throw New Exception("InstaModule:New(), ale IndexDir nie istnieje")
        msIndexFilePathname = IO.Path.Combine(sIndexDirPath, CURR_INDEX)
        msIndexFileBakPathname = IO.Path.Combine(sIndexDirPath, BAK_INDEX)

        msLocalCacheFolder = sLocalCacheFolder
    End Sub

    'Public Sub SetPicPath(sPicLibDirPath As String)
    '    If Not IO.Directory.Exists(sPicLibDirPath) Then Throw New Exception("InstaModule:SetPicPath(dir), ale PiclibDir nie istnieje")
    'End Sub


    Private Shared Function ExtractInstaDataFromPage(sPage As String) As JSONinstagram

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

    Public Async Function ReadInstagramData(sChannel As String, bMsg As Boolean) As Task(Of JSONinstagram)

        Dim sChannelUri As String = sChannel
        If Not sChannel.ToLower.Contains("instagram") Then
            sChannelUri = "https://www.instagram.com/" & sChannel
        End If

        Try
            Dim sPage As String = Await HttpPageAsync(New Uri(sChannelUri)) ' sChannel, bMsg) ' , "", True) ' true - reset HttpClient
            If sPage = "" Then Return Nothing
            msLastHtmlPage = sPage

            If sPage.Contains("RecaptchaChallengeForm") Then
                If bMsg Then Await DialogBoxAsync("Got RecaptchaChallengeForm")
                Return Nothing
            End If

            If sPage.Contains("This Account is Private") Then
                If bMsg Then Await DialogBoxAsync(sChannel & " is private - trzeba follołować")
                Return Nothing
            End If

            Dim oTmp As JSONinstagram = ExtractInstaDataFromPage(sPage)
            If oTmp IsNot Nothing Then Return oTmp

            If bMsg Then Await DialogBoxAsync("Bad response from channel " & sChannel)
            Return Nothing

        Catch ex As Exception
            CrashMessageAdd("ReadInstagramData", ex, True)
        End Try
        Return Nothing

    End Function

    'Public Function GetPicRootDir() As String
    '    Return msPictLibrPathname
    'End Function

    'Public Async Function GetPicRootDirAsync() As Task(Of Windows.Storage.StorageFolder)
    '    Try
    '        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.KnownFolders.PicturesLibrary
    '        oFold = Await oFold.CreateFolderAsync("InstaMonitor", Windows.Storage.CreationCollisionOption.OpenIfExists)
    '        Return oFold
    '    Catch ex As Exception
    '        Return Nothing
    '    End Try
    'End Function

    Private _kanaly As List(Of LocalChannel)  ' public dla wersji z RefreshWebView: kod na tamtej podstronie

    Public Function GetInstaChannelsList() As List(Of LocalChannel)
        Return _kanaly
    End Function

    Public Function GetInstaChannelItem(sChannel As String) As LocalChannel
        For Each oChannel As LocalChannel In _kanaly
            If oChannel.sChannel.ToLower = sChannel.ToLower Then Return oChannel
        Next

        Return Nothing
    End Function

    '    Public Async Function TryAddChannelAsync(sNewChannel As String, oTBoxSetText As UItBoxSetText) As Task(Of String)
    '        If Not LoadChannels() Then
    '            Return "ERROR cannot load channel list"
    '        End If

    '        For Each oItem As LocalChannel In _kanaly
    '            If oItem.sChannel.ToLower = sNewChannel.ToLower Then
    '                Return "Taki kanal juz istnieje"
    '            End If
    '        Next

    '        Dim user As JSONinstaUser
    '#If False Then
    '                ' wersja poprzednia, anonymous
    '                Dim oJSON As JSONinstagram = Await ReadInstagramData(sNewChannel, False)

    '                If oJSON Is Nothing Then
    '                    Return "Nie ma takiego kanału, albo błąd danych ('" & sNewChannel & "')"
    '                End If

    '                user = oJSON.entry_data?.ProfilePage?.ElementAt(0)?.graphql?.user
    '                If user Is Nothing Then
    '                    Return "ProfilePage jest empty, pewnie konieczny login ('" & sNewChannel & "')"
    '                End If
    '#Else
    '        If oTBoxSetText IsNot Nothing Then oTBoxSetText("Logging...")
    '        If Not Await InstaNugetLoginAsync(oTBoxSetText) Then Return "Interactive login required"
    '        If oTBoxSetText IsNot Nothing Then oTBoxSetText("Getting data...")
    '        user = Await InstaNugetGetUserDataAsync(sNewChannel)
    '        If user Is Nothing Then Return "Nie mogę sprawdzić info o kanale, może go nie ma?"
    '        If oTBoxSetText IsNot Nothing Then oTBoxSetText("Following...")
    '        Await InstaNugetFollowAsync(user.id, (oTBoxSetText IsNot Nothing))    ' i od razu robi follow
    '#End If

    '        AddChannelMain(sNewChannel, user)

    '        Return "OK, channel added"

    '    End Function

    Public Sub AddChannelMain(sNewChannel As String, user As JSONinstaUser)
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

        SaveChannels()

    End Sub

    'Public Async Function AddChannelFromRemote(sNewChannel As String) As Task(Of String)

    '    Dim sRetMsg As String = Await TryAddChannelAsync(sNewChannel, Nothing)
    '    Return sRetMsg

    'End Function


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

    Public Function LoadChannels() As Boolean

        If _kanaly IsNot Nothing Then
            If _kanaly.Count > 1 Then Return True
        End If

        Dim sTxt As String = ""
        If IO.File.Exists(msIndexFilePathname) Then
            sTxt = IO.File.ReadAllText(msIndexFilePathname)
        End If

        If String.IsNullOrEmpty(sTxt) Then
            _kanaly = New List(Of LocalChannel)
            Return False
        End If

        _kanaly = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(List(Of LocalChannel)))

        ZrobDymkiKanalow()

        Return True

    End Function


    Public Function SaveChannels() As Boolean
        If _kanaly.Count < 1 Then Return False

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(_kanaly, Newtonsoft.Json.Formatting.Indented)
        If IO.File.Exists(msIndexFileBakPathname) Then IO.File.Delete(msIndexFileBakPathname)
        If IO.File.Exists(msIndexFilePathname) Then IO.File.Move(msIndexFilePathname, msIndexFileBakPathname)

        IO.File.WriteAllText(msIndexFilePathname, sTxt)

        Return True
    End Function

    '    Public Async Function GetChannelDir(oChannel As LocalChannel, bMsg As Boolean) As Task(Of String)

    '        Dim sChanDir As String = IO.Path.Combine(msPictLibrPathname, oChannel.sDirName.Trim)
    '        If IO.Directory.Exists(sChanDir) Then Return sChanDir

    '        ' skoro nie ma, to zakladamy
    '        Try
    '            IO.Directory.CreateDirectory(sChanDir)
    '        Catch
    '            sChanDir = ""
    '        End Try

    '        If sChanDir = "" Then
    '            If bMsg Then Await DialogBoxAsync("Cannot get folder for pictures (" & oChannel.sDirName & ")")
    '            Return ""
    '        End If

    '        Dim sTxt As String = "Instagram user info" & vbCrLf & vbCrLf
    '        sTxt = sTxt & "User name: " & oChannel.sChannel & vbCrLf
    '        sTxt = sTxt & "Full name: " & oChannel.sFullName & vbCrLf & vbCrLf
    '        sTxt = sTxt & "Biografia: " & vbCrLf & oChannel.sBiografia

    '        Dim sUserInfo As String = IO.Path.Combine(sChanDir, USER_INFO)
    '        IO.File.WriteAllText(sUserInfo, sTxt)

    '        Return sChanDir
    '    End Function

    'Public Shared Function LoadPictData(sFold As String) As List(Of LocalPictureData)
    '    Dim sFile As String = IO.Path.Combine(sFold, PICT_DATA)
    '    Dim sTxt As String
    '    If IO.File.Exists(sFile) Then
    '        sTxt = IO.File.ReadAllText(sFile)
    '    Else
    '        sTxt = ""
    '    End If
    '    If String.IsNullOrEmpty(sTxt) Then
    '        Return New List(Of LocalPictureData)
    '    Else

    '        Try
    '            Return Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(List(Of LocalPictureData)))
    '        Catch ex As Exception

    '        End Try
    '        DialogBox("Error reading pictureData")
    '        Return Nothing
    '    End If
    'End Function

    '    Private Async Function DownloadPictureAsync(sFold As String, sUrl As String) As Task(Of String)

    '        Dim iInd As Integer = sUrl.IndexOf("?")
    '        Dim sLocalFileName As String

    '        If iInd < 0 Then
    '            Await DialogBoxAsync("ERROR in DownloadPictureAsyn, iInd<0 " & vbCrLf & sUrl)
    '            Return ""
    '        End If

    '        sLocalFileName = sUrl.Substring(0, iInd)
    '        iInd = sLocalFileName.LastIndexOf("/")
    '        sLocalFileName = sLocalFileName.Substring(iInd + 1)

    '        Dim oResp As Net.Http.HttpResponseMessage = Nothing
    '        Dim oHttp As New Net.Http.HttpClient

    '        Dim sError As String = ""
    '        Try
    '            oResp = Await oHttp.GetAsync(New Uri(sUrl))
    '            If oResp.StatusCode > 290 Then Return ""
    '        Catch ex As Exception
    '            sError = ex.Message
    '        End Try

    '        If sError <> "" Then
    '            Await DialogBoxAsync("ERROR in DownloadPictureAsyn, while getting page " & vbCrLf & sUrl)
    '            Return ""
    '        End If


    '        Dim sFile As String = IO.Path.Combine(sFold, sLocalFileName)
    '        IO.File.Delete(sFile) ' nie ma Exception jak nie ma pliku
    '        Dim oStream As IO.Stream = Nothing
    '        Try
    '            oStream = IO.File.OpenWrite(sFile)
    '            Await oResp.Content.CopyToAsync(oStream)
    '        Catch ex As Exception
    '            sError = ex.Message
    '        End Try

    '        If sError <> "" Then
    '            Await DialogBoxAsync("ERROR in DownloadPictureAsyn, while saving to " & vbCrLf & sLocalFileName)
    '        End If

    '        oStream?.Flush()
    '        oStream?.Dispose()
    '        Return sLocalFileName
    '    End Function

    '    Public Async Function LoadPictData(oChannel As LocalChannel, bMsg As Boolean) As Task(Of List(Of LocalPictureData))
    '        Dim sFold As String = Await GetChannelDir(oChannel, bMsg)
    '        If sFold = "" Then Return Nothing

    '        ' list of pictures (with data)
    '        Dim oPicList As List(Of LocalPictureData) = LoadPictData(sFold)
    '        Return oPicList
    '    End Function

    '    Public Async Function SavePictData(oChannel As LocalChannel, oPicList As List(Of LocalPictureData), bMsg As Boolean) As Task
    '        Dim sFold As String = Await GetChannelDir(oChannel, bMsg)
    '        If sFold = "" Then Return

    '        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(From c In oPicList Select c Distinct, Newtonsoft.Json.Formatting.Indented)
    '        Dim sFile As String = IO.Path.Combine(sFold, PICT_DATA)
    '        IO.File.WriteAllText(sFile, sTxt)

    '    End Function

    'Public Async Function GetInstagramFeedFromJSON(oJSON As JSONinstagram, oChannel As LocalChannel, bMsg As Boolean) As Task(Of Integer)

    '    Try

    '        ' folder for pictures
    '        Dim sFold As String = Await GetChannelDir(oChannel, bMsg)
    '        If sFold = "" Then Return -1

    '        ' list of pictures (with data)
    '        Dim oPicList As List(Of LocalPictureData) = LoadPictData(sFold)
    '        Dim bPicListDirty As Boolean = False

    '        Dim oJUser As JSONinstaUser = oJSON?.entry_data?.ProfilePage?.ElementAt(0)?.graphql?.user

    '        If oJUser Is Nothing Then
    '            If bMsg Then Await DialogBoxAsync("Bad JSONinstaUser" & vbCrLf & oChannel.sChannel)
    '            Return -1
    '        End If

    '        Dim sLastGuid As String = oChannel.sLastId
    '        Dim bFirst As Boolean = True
    '        Dim iNewPicCount As Integer = 0

    '        If oJUser?.edge_owner_to_timeline_media?.edges Is Nothing Then
    '            If bMsg Then Await DialogBoxAsync("Bad JSONinstaUser..edges" & vbCrLf & oChannel.sChannel)
    '            Return -1
    '        End If

    '        For Each oEdge As JSONinstaPicEdge In oJUser.edge_owner_to_timeline_media.edges
    '            Dim oItem As JSONinstaPicNode = oEdge?.node

    '            If oItem Is Nothing Then
    '                If bMsg Then Await DialogBoxAsync("Bad oItem" & vbCrLf & oChannel.sChannel)
    '                Continue For
    '            End If

    '            If oItem.id = sLastGuid Then Exit For
    '            If bFirst Then
    '                oChannel.sLastId = oItem.id
    '                bFirst = False
    '            End If

    '            Dim oNew As LocalPictureData = New LocalPictureData
    '            oNew.iTimestamp = oItem.taken_at_timestamp
    '            oNew.sPlace = oItem.location?.name
    '            oNew.sCaptionAccessib = oItem.accessibility_caption
    '            oNew.sData = DateTime.Now.ToString("yyyy-MM-dd")
    '            oNew.sCaption = ""
    '            If oItem?.edge_media_to_caption?.edges IsNot Nothing Then
    '                For Each oCapt As JSONinstaNodeCaption In oItem.edge_media_to_caption.edges
    '                    oNew.sCaption = oNew.sCaption & oCapt.node.text & vbCrLf
    '                Next
    '            End If

    '            oNew.sFileName = Await DownloadPictureAsync(sFold, oItem.display_url)
    '            If oNew.sFileName = "" Then
    '                If bMsg Then Await DialogBoxAsync("Cannot download picture from channel" & vbCrLf & oChannel.sChannel)
    '            Else
    '                    ' tylko gdy dodany został jakiś obrazek
    '                    bPicListDirty = True
    '                iNewPicCount += 1
    '                oPicList.Insert(0, oNew)

    '                '' aktualizacja listy nowości
    '                'Dim oNewPicData As LocalNewPicture = New LocalNewPicture
    '                'oNewPicData.oPicture = oNew
    '                'oNewPicData.oChannel = oChannel
    '                'App._gNowosci.Add(oNewPicData)

    '            End If
    '        Next

    '        If Not bPicListDirty Then Return 0

    '        Await SavePictData(oChannel, oPicList, bMsg)
    '        'Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(From c In oPicList Select c Distinct, Newtonsoft.Json.Formatting.Indented)
    '        'Await oFold.WriteAllTextToFileAsync("pictureData.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)


    '        Return iNewPicCount

    '    Catch ex As Exception
    '        CrashMessageAdd("GetInstagramFeedFromJSON", ex, True)
    '    End Try

    '    Return -1   ' signal error
    'End Function


    'Public Async Function GetInstagramFeed(oChannel As LocalChannel, bMsg As Boolean) As Task(Of Integer)
    '    ' RET: -1: error, 0: nic nowego, 1: są nowe
    '    ' sprawsdzic czy zmiana sie dokonuje w oChannel piętro wyżej!

    '    Dim oJSON As JSONinstagram = Await ReadInstagramData(oChannel.sChannel, bMsg)
    '    If oJSON Is Nothing Then
    '        ' If bShowMsg Then Await DialogBoxAsync("Bad response from channel" & vbCrLf & oChannel.sChannel)
    '        Return -1
    '    End If

    '    Return Await GetInstagramFeedFromJSON(oJSON, oChannel, bMsg)
    'End Function

    'Public Async Function GetAllFeedsTimerAsync() As Task(Of Boolean)
    '    ' wywoływane z Timer, poprzednio GetAllFeedsAsync(nothing, nothing) - wersja beznugetowa
    '    ' *TODO* gdyby chcieć mieć Nugetową, to trzeba to napisać od nowa
    'End Function

    'Public Async Function GetAllFeedsAsync(oTBoxSetText As UItBoxSetText) As Task(Of Boolean)
    ' poprzednia wersja, która nie korzysta z Nugeta
    '    If Not LoadChannels() Then
    '        If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("Empty channel list")
    '        Return False
    '    End If

    '    ProgRingShow(True, False, 0, _kanaly.Count)

    '    Dim bChannelsDirty As Boolean = False
    '    Dim lsToastErrors As List(Of String) = New List(Of String)
    '    Dim lsToastNews As List(Of String) = New List(Of String)
    '    Dim iNewsCount As Integer = 0

    '    Dim iErrCntToStop As Integer = 10

    '    For Each oItem As LocalChannel In From c In _kanaly Order By c.sChannel
    '        ProgRingInc()
    '        If Not oItem.bEnabled Then Continue For

    '        If oTBoxSetText IsNot Nothing Then oTBoxSetText(oItem.sChannel)
    '        Dim iRet As Integer = Await GetInstagramFeed(oItem, Nothing) ' oTB IsNot Nothing
    '        Await Task.Delay(2000)  ' bylo 500, ale to przestaje dzialac chyba po 3..4, więc zwiekszam (2020.11.29)
    '        If iRet < 0 Then
    '            oItem.iNewCnt = -1
    '            If oItem.sFirstError = "" Then oItem.sFirstError = DateTime.Now.ToString("dd MM yyyy")
    '            lsToastErrors.Add(oItem.sChannel)
    '            iErrCntToStop -= 1
    '            If iErrCntToStop < 0 Then
    '                ' skoro tak duzo bledow pod rząd, to pewnie nie ma sensu nic dalej sciagac
    '                ClipPut(msLastHtmlPage)
    '                If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("za duzo błędów pod rząd, poddaję się; w ClipBoard ostatni HTML")
    '                Exit For
    '            End If
    '        ElseIf iRet > 0 Then
    '            iErrCntToStop = 10
    '            oItem.sFirstError = ""  ' kanał już nie daje błędów
    '            lsToastNews.Add(oItem.sChannel & " (" & iRet & ")")
    '            oItem.iNewCnt = iRet
    '            iNewsCount += iRet
    '            bChannelsDirty = True
    '        End If
    '        ' zero: nothing - nic nowego, ale też bez błędu
    '        ' ewentualnie kiedyś sprawdzanie, że dawno nic nie było
    '    Next

    '    ' te dwie rzeczy były na koncu, ale wtedy czasem nie zapisuje (jak wylatuje) - wiec zrobmy to teraz, jakby crash byl ponizej (a nie w samym sciaganiu)
    '    SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
    '    If bChannelsDirty Then SaveChannels()

    '    ProgRingShow(False)

    '    Await PrzygotujPokazInfo(lsToastErrors, lsToastNews, (oTBoxSetText IsNot Nothing))
    '    ' przeniesione przed przygotowanie komunikatu
    '    'SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))

    '    If oTBoxSetText IsNot Nothing Then oTBoxSetText(iNewsCount & " new pics")

    '    If Not bChannelsDirty Then Return False

    '    ' przeniesione przed przygotowanie komunikatu
    '    'Await SaveChannelsAsync()

    '    Return True

    'End Function

    Public Shared Async Function PrzygotujPokazInfo(lsToastErrors As List(Of String), lsToastNews As List(Of String), bMsg As Boolean) As Task
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
            If Not bMsg Then
                MakeToast(sToastSummary)
            Else
                Await DialogBoxAsync(sToastSummary)   ' podczas gdy sobie odklikuję, to on zapisuje
                ClipPut(sToast) ' do Clip idzie pełna wersja (z wymienionymi kanałami)
            End If
        End If

    End Function

#Region "ramtinak/InstagramApiSharp"

    Public mInstaApi As InstagramApiSharp.API.IInstaApi = Nothing

    Public Async Function InstaNugetLoginAsync(oTBoxSetText As UItBoxSetText) As Task(Of Boolean)
        If mInstaApi IsNot Nothing Then Return True

        Dim mUserLogin As InstagramApiSharp.Classes.UserSessionData = New InstagramApiSharp.Classes.UserSessionData
        mUserLogin.UserName = GetSettingsString("uiUserName", App.gmDefaultLoginName)
        mUserLogin.Password = GetSettingsString("uiPassword")

        If mUserLogin.Password = "" Then
            If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("No password set!")
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

        Dim sSessionFile As String = IO.Path.Combine(msLocalCacheFolder, "sessionState.bin")
        If IO.File.Exists(sSessionFile) Then
            '2021.09.26 ogranicznik czasu logowania
            If IO.File.GetCreationTime(sSessionFile).AddHours(6) < DateTime.Now Then
                If oTBoxSetText IsNot Nothing Then oTBoxSetText("Removing old sessionState...")
                IO.File.Delete(sSessionFile)
            Else
                If oTBoxSetText IsNot Nothing Then oTBoxSetText("Reading sessionState...")
                mInstaApi.LoadStateDataFromString(IO.File.ReadAllText(sSessionFile))
            End If
        End If

        If Not mInstaApi.IsUserAuthenticated Then
            If oTBoxSetText Is Nothing Then
                mInstaApi = Nothing
                Return False
            End If

            '// Call this function before calling LoginAsync
            If oTBoxSetText IsNot Nothing Then oTBoxSetText("SendRequestsBeforeLoginAsync...")
            Await mInstaApi.SendRequestsBeforeLoginAsync()
            '// wait 5 seconds
            Await Task.Delay(5000)

            If oTBoxSetText IsNot Nothing Then oTBoxSetText("LoginAsync...")
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
        IO.File.WriteAllText(sSessionFile, sNewState)

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

    Public Async Function InstaNugetFollowAsync(iUserId As Long, bMsg As Boolean) As Task(Of Boolean)
        If mInstaApi Is Nothing Then Return False
        If Not mInstaApi.IsUserAuthenticated Then Return False

        Try
            Dim oRes = Await mInstaApi.UserProcessor.FollowUserAsync(iUserId)
            If oRes.Succeeded Then Return True

            If bMsg Then Await DialogBoxAsync("Error trying to Follow")

        Catch ex As Exception
        End Try
        Return False
    End Function

    Public Function InstaNugetGetCurrentUserId() As Long
        If mInstaApi Is Nothing Then Return 0
        If Not mInstaApi.IsUserAuthenticated Then Return 0

        If mInstaApi.GetLoggedUser.LoggedInUser.Pk > 0 Then Return mInstaApi.GetLoggedUser.LoggedInUser.Pk

        'Dim oCurrUsers = Await mInstaApi.GetLoggedUser.LoggedInUser  ' .GetCurrentUserAsync()
        'If Not oCurrUsers.Succeeded Then Return 0

        'Return oCurrUsers.Value.Pk
        Return 0
    End Function

    'Public Async Function InstaNugetGetFollowingi(oTBoxSetText As UItBoxSetText) As Task(Of List(Of LocalChannel))
    '    Dim oRet As List(Of LocalChannel) = New List(Of LocalChannel)

    '    Dim iUserId As Long = GetSettingsLong("instagramUserId")
    '    If iUserId = 0 Then
    '        iUserId = InstaNugetGetCurrentUserId()
    '        If iUserId = 0 Then
    '            If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("ERROR: nie moge dostac sie do currentUserId")
    '            Return Nothing
    '        End If
    '        SetSettingsLong("instagramUserId", iUserId)
    '    End If

    '    Dim oPaging As InstagramApiSharp.PaginationParameters = InstagramApiSharp.PaginationParameters.MaxPagesToLoad(20)   ' przy 10 jest chyba OK, ale warto miec rezerwę
    '    Dim sSearchQuery As String = ""
    '    Dim oRes = Await mInstaApi.UserProcessor.GetUserFollowingByIdAsync(iUserId, oPaging, sSearchQuery)
    '    If Not oRes.Succeeded Then Return Nothing

    '    If oRes.Value Is Nothing Then Return Nothing

    '    ' 2021.12.14 - jedno pytanie tylko...
    '    Dim bDodajFollow As Boolean = False

    '    For Each oFoll As InstagramApiSharp.Classes.Models.InstaUserShort In oRes.Value
    '        Dim bMam As Boolean = False
    '        For Each oItem As LocalChannel In _kanaly
    '            If oItem.sChannel.ToLower = oFoll.UserName.ToLower Then
    '                If oItem.iUserId < 10 Then oItem.iUserId = oFoll.Pk
    '                oRet.Add(oItem)
    '                bMam = True
    '            End If
    '        Next
    '        If Not bMam Then
    '            If oTBoxSetText Is Nothing Then Continue For

    '            If Not bDodajFollow Then
    '                If Not Await DialogBoxYNAsync("Following '" & oFoll.UserName & "' - nie ma kanału, dodać?") Then
    '                    Continue For
    '                End If
    '                bDodajFollow = True
    '            End If
    '            Dim sAdded As String = Await TryAddChannelAsync(oFoll.UserName, oTBoxSetText)
    '            If Not sAdded.StartsWith("OK") Then Continue For
    '        End If
    '    Next

    '    Return oRet

    'End Function

    'Public Delegate Sub UIProgRingShow(bVisible As Boolean, dMax As Double)
    'Public Delegate Sub UIProgRingMaxVal(iMax As Integer)
    'Public Delegate Sub UIProgRingInc()

    'Public Async Function InstaNugetRefreshAll(oTBoxSetText As UItBoxSetText,
    '              oProgRingShow As UIProgRingShow, oProgRingMaxVal As UIProgRingMaxVal, oProgRingInc As UIProgRingInc) As Task(Of Integer)
    '    'Dim bMsg As Boolean = False
    '    'If oTB IsNot Nothing Then bMsg = True

    '    oProgRingShow(True, 0)
    '    Dim bLoadOk As Boolean = LoadChannels()
    '    oProgRingShow(False, 0)

    '    If Not bLoadOk Then
    '        If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("Empty channel list")
    '        Return False
    '    End If

    '    ' sprawdzamy tylko followingi - omijając w ten sposób mechanizm blokowania kanałów z aplikacji, jako że teraz to jest rozsynchronizowane
    '    oProgRingShow(True, 0)
    '    If Not Await InstaNugetLoginAsync(oTBoxSetText) Then Return -1

    '    Dim oFollowingi As List(Of LocalChannel) = Await InstaNugetGetFollowingi(oTBoxSetText)
    '    oProgRingShow(False, 0)
    '    If oFollowingi Is Nothing Then Return -1

    '    ' ponizsza czesc bedzie wspolna dla RefreshAll (wedle Following), oraz periodycznego (wedle InstaNugetGetRecentActivity)
    '    Dim bChannelsDirty As Boolean = False
    '    Dim lsToastErrors As List(Of String) = New List(Of String)
    '    Dim lsToastNews As List(Of String) = New List(Of String)
    '    Dim iNewsCount As Integer = 0

    '    Dim iErrCntToStop As Integer = 10

    '    oProgRingShow(True, oFollowingi.Count)
    '    oProgRingMaxVal(oFollowingi.Count)   ' bo to zagnieżdżone jest w PRShow, czyli sam z siebie nie zmieni Max

    '    For Each oChannel As LocalChannel In From c In oFollowingi Order By c.sChannel
    '        oProgRingInc()

    '        If oTBoxSetText IsNot Nothing Then oTBoxSetText(oChannel.sChannel)

    '        oProgRingShow(True, 0)
    '        Dim iRet As Integer = Await InstaNugetCheckNewsFromUserAsync(oChannel, oTBoxSetText)   ' w serii, wiec bez czekania na klikanie błędów
    '        oProgRingShow(False, 0)

    '        Await Task.Delay(3000)  ' tak samo jak w wersji anonymous, jednak czekamy troche, nawet więcej (3 nie 2 sek) - i tak idziemy tylko po follow, nieistniejące są usunięte automatycznie
    '        If iRet < 0 Then
    '            oChannel.iNewCnt = -1
    '            If oChannel.sFirstError = "" Then oChannel.sFirstError = DateTime.Now.ToString("dd MM yyyy")
    '            lsToastErrors.Add(oChannel.sChannel)
    '            iErrCntToStop -= 1
    '            If iErrCntToStop < 0 Then
    '                ' skoro tak duzo bledow pod rząd, to pewnie nie ma sensu nic dalej sciagac
    '                ClipPut(msLastHtmlPage)
    '                If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("za duzo błędów pod rząd, poddaję się; w ClipBoard ostatni HTML")
    '                Exit For
    '            End If
    '            bChannelsDirty = True
    '        ElseIf iRet > 0 Then
    '            iErrCntToStop = 10
    '            oChannel.sFirstError = ""  ' kanał już nie daje błędów
    '            lsToastNews.Add(oChannel.sChannel & " (" & iRet & ")")
    '            oChannel.iNewCnt = iRet
    '            iNewsCount += iRet
    '            bChannelsDirty = True
    '        End If
    '        ' zero: nothing - nic nowego, ale też bez błędu
    '        ' ewentualnie kiedyś sprawdzanie, że dawno nic nie było
    '    Next

    '    ' te dwie rzeczy były na koncu, ale wtedy czasem nie zapisuje (jak wylatuje) - wiec zrobmy to teraz, jakby crash byl ponizej (a nie w samym sciaganiu)
    '    SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
    '    If bChannelsDirty Then SaveChannels()

    '    oProgRingShow(False, 0)

    '    Await PrzygotujPokazInfo(lsToastErrors, lsToastNews, (oTBoxSetText IsNot Nothing))
    '    ' przeniesione przed przygotowanie komunikatu
    '    'SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))

    '    If Not bChannelsDirty Then Return False

    '    Return True

    'End Function

    '' wersja 2, teoretycznie szybsza: foreach(user in getRecentActivity) do checkUserActivity()
    '' wersja 3, podobna do powyzej: foreach(activity in getRecentActivity) do sciagnijObrazek
    '' ale co jak jest kilka obrazkow po kolei w jednym? to jest ten story?
    '' ile wpisów jest na page? 12, jak przy user (ze nigdy wiecej nie pokazalo obrazkow niz 12?)
    '' to może być na timer, np. godzinny
    'Public Async Function InstaNugetGetRecentActivity() As Task
    '    If mInstaApi Is Nothing Then Return
    '    If Not mInstaApi.IsUserAuthenticated Then Return

    '    Dim oPaging As InstagramApiSharp.PaginationParameters = InstagramApiSharp.PaginationParameters.MaxPagesToLoad(10)
    '    Dim oRes = Await mInstaApi.UserProcessor.GetFollowingRecentActivityFeedAsync(oPaging)

    '    ' *TODO*

    'End Function


    'Public Async Function InstaNugetCheckNewsFromUserAsync(oChannel As LocalChannel, oTBoxSetText As UItBoxSetText) As Task(Of Integer)
    '    Try

    '        ' folder for pictures
    '        Dim sFold As String = Await GetChannelDir(oChannel, (oTBoxSetText IsNot Nothing))
    '        If sFold = "" Then Return -1

    '        Dim bRet As Boolean
    '        bRet = Await InstaNugetLoginAsync(oTBoxSetText)
    '        If Not bRet Then Return Nothing

    '        ' mozna id wziąć z oItem
    '        If oChannel.iUserId < 10 Then
    '            Dim userInfo = Await mInstaApi.UserProcessor.GetUserInfoByUsernameAsync(oChannel.sChannel)
    '            If Not userInfo.Succeeded Then Return Nothing
    '            oChannel.iUserId = userInfo.Value.Pk
    '            ' kanały są do późniejszego ZAPISU!! choćby dlatego że lastId sie zmienia
    '        End If

    '        Dim userFullInfo = Await mInstaApi.UserProcessor.GetFullUserInfoAsync(oChannel.iUserId)
    '        If Not userFullInfo.Succeeded Then Return Nothing

    '        ' przetworzenie listy z FEED
    '        Dim oWpisy As List(Of InstagramApiSharp.Classes.Models.InstaMedia) = userFullInfo.Value?.Feed?.Items
    '        If oWpisy Is Nothing Then
    '            If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("Bad List(Of InstaMedia)" & vbCrLf & oChannel.sChannel)
    '            Return -1
    '        End If

    '        ' list of pictures (with data)
    '        Dim oPicList As List(Of LocalPictureData) = LoadPictData(sFold)
    '        Dim bPicListDirty As Boolean = False

    '        Dim sLastGuid As String = oChannel.sLastId
    '        Dim bFirst As Boolean = True
    '        Dim iNewPicCount As Integer = 0

    '        For Each oMedia As InstagramApiSharp.Classes.Models.InstaMedia In oWpisy

    '            If oMedia.Images Is Nothing Then Continue For
    '            If oMedia.Images.Count < 1 Then Continue For

    '            Dim oPic = oMedia.Images.ElementAt(0) ' teoretycznie pierwszy jest najwiekszy, ale...
    '            For Each oPicLoop In oMedia.Images
    '                If oPicLoop.Height > oPic.Height Then oPic = oPicLoop
    '            Next

    '            If oMedia.Pk = sLastGuid Then Exit For
    '            If bFirst Then
    '                oChannel.sLastId = oMedia.Pk
    '                bFirst = False
    '            End If

    '            Dim oNew As LocalPictureData = New LocalPictureData
    '            oNew.iTimestamp = oMedia.TakenAtUnix
    '            If oMedia.Location IsNot Nothing Then oNew.sPlace = oMedia.Location?.Name
    '            ' oNew.sCaptionAccessib = oItem.accessibility_caption ' tego pola nie ma?
    '            oNew.sData = DateTime.Now.ToString("yyyy-MM-dd") ' data wczytania obrazka, nie data obrazka!
    '            If oMedia.Caption IsNot Nothing Then oNew.sCaption = oMedia.Caption.Text
    '            'If oItem?.edge_media_to_caption?.edges IsNot Nothing Then
    '            '    For Each oCapt As JSONinstaNodeCaption In oItem.edge_media_to_caption.edges
    '            '        oNew.sCaption = oNew.sCaption & oCapt.node.text & vbCrLf
    '            '    Next
    '            'End If

    '            oNew.sFileName = Await DownloadPictureAsync(sFold, oPic.Uri)
    '            If oNew.sFileName = "" Then
    '                If oTBoxSetText IsNot Nothing Then Await DialogBoxAsync("Cannot download picture from channel" & vbCrLf & oChannel.sChannel)
    '            Else
    '                ' tylko gdy dodany został jakiś obrazek
    '                bPicListDirty = True
    '                iNewPicCount += 1
    '                oPicList.Insert(0, oNew)

    '                '' aktualizacja listy nowości
    '                'Dim oNewPicData As LocalNewPicture = New LocalNewPicture
    '                'oNewPicData.oPicture = oNew
    '                'oNewPicData.oChannel = oChannel
    '                'App._gNowosci.Add(oNewPicData)

    '            End If
    '        Next

    '        If Not bPicListDirty Then Return 0

    '        Await SavePictData(oChannel, oPicList, (oTBoxSetText IsNot Nothing))
    '        'Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(From c In oPicList Select c Distinct, Newtonsoft.Json.Formatting.Indented)
    '        'Await oFold.WriteAllTextToFileAsync("pictureData.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)


    '        Return iNewPicCount

    '    Catch ex As Exception
    '        CrashMessageAdd("GetInstagramFeedFromJSON", ex, True)
    '    End Try

    '    Return -1   ' signal error


    'End Function


#End Region

End Class
