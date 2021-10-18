
Public NotInheritable Class InstaFeeds
    Inherits Page

    Dim _kanaly As ObservableCollection(Of LocalChannel)
    Dim _oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder

    'Private Sub ProgresywnyRing(sStart As Boolean)
    '    If sStart Then
    '        Dim dVal As Double
    '        dVal = (Math.Min(uiGrid.ActualHeight, uiGrid.ActualWidth)) / 2
    '        uiProcesuje.Width = dVal
    '        uiProcesuje.Height = dVal

    '        uiProcesuje.Visibility = Visibility.Visible
    '        uiProcesuje.IsActive = True
    '    Else
    '        uiProcesuje.IsActive = False
    '        uiProcesuje.Visibility = Visibility.Collapsed
    '    End If
    'End Sub



    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        GetAppVers(uiVers)
        ProgRingInit(True, False)

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.LocalFolder.ReadAllTextFromFileAsync("channels.json")

        If String.IsNullOrEmpty(sTxt) Then
            _kanaly = New ObservableCollection(Of LocalChannel)
        Else
            _kanaly = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of LocalChannel)))
        End If

        'Dim oFile As Windows.Storage.StorageFile = Await _oFold.TryGetItemAsync("channels.json")
        'If oFile Is Nothing Then
        '    _kanaly = New ObservableCollection(Of LocalChannel)
        'Else
        '    Dim sTxt As String = File.ReadAllText(oFile.Path)
        '    _kanaly = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of LocalChannel)))
        'End If
        uiListItems.ItemsSource = From c In _kanaly Order By c.sChannel

        GetSettingsString(uiUserName, "uiUserName", App.gmDefaultLoginName)
        GetSettingsString(uiPassword, "uiPassword")

    End Sub

    Private Async Sub uiSave_Click(sender As Object, e As RoutedEventArgs)
        If _kanaly.Count < 1 Then
            If Not Await DialogBoxYNAsync("Pusta lista! Zapisać ją?") Then Me.Frame.GoBack()
        End If

        'Await SaveChannelsAsync() - nie moze tak byc, bo tu mamy lokalną listę (kopię ze zmianami)

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(_kanaly, Newtonsoft.Json.Formatting.Indented)

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder

        Dim oFile As Windows.Storage.StorageFile = Await oFold.TryGetItemAsync("channels.bak")
        If oFile IsNot Nothing Then Await oFile.DeleteAsync()
        oFile = Await oFold.TryGetItemAsync("channels.json")
        If oFile IsNot Nothing Then Await oFile.RenameAsync("channels.bak")

        Await oFold.WriteAllTextToFileAsync("channels.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        Me.Frame.GoBack()
    End Sub

    Private Async Function AddChannelMain(sChannel As String, oTB As TextBlock) As Task(Of Boolean)

        ProgRingShow(True)

        Dim sRetMsg As String = Await TryAddChannelAsync(sChannel, oTB)
        Dim bRetOk As Boolean = sRetMsg.StartsWith("OK")

        If Not bRetOk Then Await DialogBoxAsync(sRetMsg)

        ProgRingShow(False)

        Return bRetOk

        'For Each oItem As LocalChannel In _kanaly
        '    If oItem.sChannel = sChannel Then
        '        DialogBox("taki kanał już istnieje!")
        '        Return True
        '    End If
        'Next

        'Dim oJSON As JSONinstagram = Await ReadInstagramData(sChannel, True)

        'If oJSON Is Nothing Then
        '    Await DialogBoxAsync("Nie ma takiego kanału, albo błąd danych" & vbCrLf & sChannel)
        '    Return False
        'End If

        'Dim user As JSONinstaUser = oJSON.entry_data?.ProfilePage?.ElementAt(0)?.graphql?.user
        'If user Is Nothing Then
        '    Await DialogBoxAsync("ProfilePage jest empty, pewnie konieczny login" & vbCrLf & sChannel)
        '    Return False
        'End If

        '' mamy więc dane kanału, dodajemy do listy
        'Await InstaModule.AddChannelMain(sChannel, user)

        'Return True
    End Function

    Private Async Sub uiAdd_Click(sender As Object, e As RoutedEventArgs)
        Dim sChannel As String = Await DialogBoxInputDirectAsync("Podaj nazwę kanału:")
        If sChannel = "" Then Return

        Await AddChannelMain(sChannel, Nothing)
    End Sub

    Private Sub uiDelChannel_Click(sender As Object, e As RoutedEventArgs)

    End Sub

#If False Then ' tego juz nie ma , było w poprzedniej wersji gdy w ClipBoard była lista kanałów. Teraz to robi się via RemoteSystem
    ' tego już nie ma, było w poprzedniej wersji gdy w ClipBoard była lista kanałów. Teraz to robi się via RemoteSystem
    '
    'Private Async Sub uiFromClip_Click(sender As Object, e As RoutedEventArgs)
    '    Dim sClip As String = Await pkar.ClipGetAsync
    '    If String.IsNullOrEmpty(sClip) Then
    '        DialogBox("Pusty clipboard?")
    '        Return
    '    End If

    '    Dim aArr As String() = sClip.Split(vbCrLf)

    '    uiProgBar.Minimum = 0
    '    uiProgBar.Value = 0
    '    uiProgBar.Maximum = aArr.Count
    '    uiProgBar.Visibility = Visibility.Visible


    '    For Each sAdd As String In aArr
    '        If sAdd.Length > 4 Then

    '            If Not Await AddChannelMain(sAdd.Trim) Then
    '                Await DialogBoxAsync("Skipping next...")
    '                uiProgBar.Visibility = Visibility.Collapsed
    '                Return
    '            End If
    '        End If

    '        uiProgBar.Value += 1

    '    Next
    '    uiProgBar.Visibility = Visibility.Collapsed

    'End Sub
#End If

#Region "recovering utracony plik kanałów"

    Private Async Function RecoverChannels(oFoldRoot As Windows.Storage.StorageFolder, lDirNames As List(Of String)) As Task(Of ObservableCollection(Of LocalChannel))

        ' na wejsciu ma liste podkatalogów
        ' ma skorzystać z plików:
        ' userinfo.txt
        '
        '   'Instagram user info
        '   '
        '   'User Name: penciloveee
        '   'Full Name:  🎨✍❤
        '   '
        '   'Biografia:
        ' pictureData.json
        ' zeby wyciagnac [{"sFileName":"103705063_863973984093905_3131773345113521040_n.jpg" jako lastId

        Dim kanaly = New ObservableCollection(Of LocalChannel)

        For Each sDir As String In lDirNames
            If sDir.Length < 3 Then Continue For
            Dim oFold As Windows.Storage.StorageFolder = Await oFoldRoot.TryGetItemAsync(sDir)
            If oFold Is Nothing Then
                Await DialogBoxAsync("Folder '" & sDir & "' cannot be opened? Skipping...")
                Continue For
            End If

            Dim sText As String = Await oFold.ReadAllTextFromFileAsync("userinfo.txt")
            If String.IsNullOrEmpty(sText) Then
                Await DialogBoxAsync("Folder '" & sDir & "', cannot open userinfo.txt? Skipping...")
                Continue For
            End If

            Dim oItem As LocalChannel = New LocalChannel
            oItem.bEnabled = True
            oItem.sDirName = sDir

            For Each sLine As String In sText.Split(vbCrLf)
                sLine = sLine.Trim
                If sLine.StartsWith("User name: ") Then oItem.sChannel = sLine.Substring("User name: ".Length)
                If sLine.StartsWith("Full name: ") Then oItem.sFullName = sLine.Substring("Full name: ".Length)
            Next

            Dim iInd As Integer = sText.IndexOf(vbCrLf & "Biografia:")  ' jest spacja po ":", ale czy zawsze?
            If iInd > 0 Then
                iInd = sText.IndexOf(vbCr, iInd + 5)
                oItem.sBiografia = sText.Substring(iInd).Trim
            End If


            kanaly.Add(oItem)
            'Public Property sLastId As String

        Next

        Return kanaly
    End Function


    Private Async Function RecoverUsingGetFolders(oFoldRoot As Windows.Storage.StorageFolder) As Task(Of List(Of String))
        Dim kanaly = New ObservableCollection(Of LocalChannel)

        Dim sError As String = ""
        Dim lDirNames As List(Of String) = New List(Of String)

        Try
            For Each oFold As Windows.Storage.StorageFolder In Await oFoldRoot.GetFoldersAsync
                lDirNames.Add(oFold.Name)
            Next
        Catch ex As Exception
            sError = ex.Message
        End Try

        If Not String.IsNullOrEmpty(sError) Then
            Await DialogBoxAsync(sError)
            Return Nothing
        End If

        Return lDirNames

    End Function

    Private Async Function RecoverUsingDirectory(oFoldRoot As Windows.Storage.StorageFolder) As Task(Of List(Of String))
        Dim kanaly = New ObservableCollection(Of LocalChannel)

        Dim sError As String = ""
        Dim lDirNames As List(Of String) = New List(Of String)

        Try
            For Each sFold As String In Directory.EnumerateDirectories(oFoldRoot.Path)
                lDirNames.Add(sFold)
            Next
        Catch ex As Exception
            sError = ex.Message
        End Try

        If Not String.IsNullOrEmpty(sError) Then
            Await DialogBoxAsync(sError)
            Return Nothing
        End If

        Return lDirNames

    End Function

    Private Async Function RecoverUsingCmdLine(oFoldRoot As Windows.Storage.StorageFolder) As Task(Of List(Of String))
        Dim kanaly = New ObservableCollection(Of LocalChannel)

        ClipPut("dir /b /a:d > dirki.txt")
        Await DialogBoxAsync("za chwilę zrób cmdline z clipboard")

        Windows.System.Launcher.LaunchFolderAsync(oFoldRoot)

        Await DialogBoxAsync("naciśnij po wykonaniu komendy (gdy będzie plik - indeks")

        Dim sIndeks As String = Await oFoldRoot.ReadAllTextFromFileAsync("dirki.txt")
        If String.IsNullOrEmpty(sIndeks) Then Return Nothing
        Dim aArr As String() = sIndeks.Split(vbCrLf)

        Dim lDirNames As List(Of String) = New List(Of String)

        For Each sFold As String In aArr
            sFold = sFold.Trim
            ' ostatnia linia jest pusta - wiec ją trzeba zignorować
            If sFold <> "" Then lDirNames.Add(sFold.Trim)
        Next

        Return lDirNames
    End Function

    Private Async Function TryRecoverAnyMethod(oFoldRoot As Windows.Storage.StorageFolder) As Task(Of ObservableCollection(Of LocalChannel))

        Dim lDirNames As List(Of String) = Nothing

        ' lista folderów odtworzenie uzywajac iterowania folderów (UWP) - E_FAIL
        ' lDirNames = Await RecoverUsingGetFolders(oFoldRoot)

        ' odtworzenie uzywajac iterowania folderów (System.IO) - Unknown Error
        ' If lDirNames Is Nothing Then lDirNames = Await RecoverUsingDirectory(oFoldRoot)

        ' odtworzenie uzywajac dir/b
        If lDirNames Is Nothing Then lDirNames = Await RecoverUsingCmdLine(oFoldRoot)

        If lDirNames Is Nothing OrElse lDirNames.Count = 0 Then Return Nothing

        Return Await RecoverChannels(oFoldRoot, lDirNames)

    End Function

    Private Async Sub uiRepair_Click(sender As Object, e As RoutedEventArgs)
        If Not Await pkar.DialogBoxYNAsync("Odtwarzać plik channels.json?") Then Return

        Dim oFoldRoot As Windows.Storage.StorageFolder = Await GetPicRootDirAsync()
        If oFoldRoot Is Nothing Then
            Await DialogBoxAsync("Cannot get folder for pictures")
            Return
        End If

        Dim kanaly As ObservableCollection(Of LocalChannel)
        kanaly = Await TryRecoverAnyMethod(oFoldRoot)

        If kanaly Is Nothing Then
            DialogBox("Sorry, ale się nie udało w żaden sposób")
        Else
            Await DialogBoxAsync("Odzyskałem, próbuję zapisać...")
            _kanaly = kanaly
            Await SaveChannelsAsync()

            uiListItems.ItemsSource = _kanaly

        End If

    End Sub

#End Region

    Private Sub uiFiltr_TextChanged(sender As Object, e As TextChangedEventArgs)
        If uiFiltr.Text.Length > 0 And uiFiltr.Text.Length < 3 Then Return  ' 0: czyli pokaz wszystko, 3: juz filtruj
        uiListItems.ItemsSource = From c In _kanaly Order By c.sChannel Where c.sChannel.Contains(uiFiltr.Text)
    End Sub

    Private Sub uiSaveLoginData_Click(sender As Object, e As RoutedEventArgs)
        SetSettingsString(uiUserName, "uiUserName")
        SetSettingsString(uiPassword, "uiPassword")
    End Sub
End Class
