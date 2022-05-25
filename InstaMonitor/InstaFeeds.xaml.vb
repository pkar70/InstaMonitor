
Imports vb14 = Vblib.pkarlibmodule14


Public NotInheritable Class InstaFeeds
    Inherits Page

    Dim _kanaly As ObservableCollection(Of Vblib.LocalChannel)
    Dim _oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder


    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        Me.ShowAppVers()
        Me.ProgRingInit(True, False)

        Dim sTxt As String = Await Windows.Storage.ApplicationData.Current.LocalFolder.ReadAllTextFromFileAsync("channels.json")

        If String.IsNullOrEmpty(sTxt) Then
            _kanaly = New ObservableCollection(Of Vblib.LocalChannel)
        Else
            _kanaly = Newtonsoft.Json.JsonConvert.DeserializeObject(sTxt, GetType(ObservableCollection(Of Vblib.LocalChannel)))
        End If

        uiListItems.ItemsSource = From c In _kanaly Order By c.sChannel

        uiUserName.GetSettingsString("uiUserName", Vblib.App.gmDefaultLoginName)
        uiPassword.GetSettingsString()

    End Sub

    Private Async Sub uiSave_Click(sender As Object, e As RoutedEventArgs)
        If _kanaly.Count < 1 Then
            If Not Await vb14.DialogBoxYNAsync("Pusta lista! Zapisać ją?") Then Me.Frame.GoBack()
        End If

        'Await SaveChannelsAsync() - nie moze tak byc, bo tu mamy lokalną listę (kopię ze zmianami)

        Dim sTxt As String = Newtonsoft.Json.JsonConvert.SerializeObject(_kanaly, Newtonsoft.Json.Formatting.Indented)

        Dim oFold As Windows.Storage.StorageFolder = Windows.Storage.ApplicationData.Current.LocalFolder

        Dim oFile As Windows.Storage.StorageFile = Await oFold.TryGetItemAsync("channels.bak")
        If oFile IsNot Nothing Then Await oFile.DeleteAsync()
        oFile = Await oFold.TryGetItemAsync("channels.json")
        If oFile IsNot Nothing Then Await oFile.RenameAsync("channels.bak")

        Await oFold.WriteAllTextToFileAsync("channels.json", sTxt, Windows.Storage.CreationCollisionOption.ReplaceExisting)

        Me.GoBack()
    End Sub

    Private Async Function AddChannelMain(sChannel As String) As Task(Of Boolean)

        Me.ProgRingShow(True)

        ' Dim sRetMsg As String = Await App.gInstaModule.TryAddChannelAsync(sChannel, Nothing)
        Dim sRetMsg As String = Await TryAddChannelAsync(sChannel, Nothing)
        Dim bRetOk As Boolean = sRetMsg.StartsWith("OK")

        If Not bRetOk Then Await vb14.DialogBoxAsync(sRetMsg)

        Me.ProgRingShow(False)

        Return bRetOk

    End Function

    Private Async Sub uiAdd_Click(sender As Object, e As RoutedEventArgs)
        Dim sChannel As String = Await vb14.DialogBoxInputDirectAsync("Podaj nazwę kanału:")
        If sChannel = "" Then Return

        Await AddChannelMain(sChannel)
    End Sub

    Private Sub uiDelChannel_Click(sender As Object, e As RoutedEventArgs)

    End Sub


#Region "recovering utracony plik kanałów"

    Private Async Function RecoverChannels(sFoldRoot As Windows.Storage.StorageFolder, lDirNames As List(Of String)) As Task(Of ObservableCollection(Of Vblib.LocalChannel))

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

        Dim kanaly = New ObservableCollection(Of Vblib.LocalChannel)

        For Each sDir As String In lDirNames
            If sDir.Length < 3 Then Continue For
            ' Dim sFold As String = IO.Path.Combine(sFoldRoot, sDir)
            Dim sFold As Windows.Storage.StorageFolder = Await sFoldRoot.TryGetItemAsync(sDir)
            ' If Not IO.Directory.Exists(sFold) Then
            If sFold Is Nothing Then
                Await vb14.DialogBoxAsync("Folder '" & sDir & "' cannot be opened? Skipping...")
                Continue For
            End If

            ' Dim sFile As String = IO.Path.Combine(sFold, "userinfo.txt")
            Dim sFile As Windows.Storage.StorageFile = Await sFold.TryGetItemAsync("userinfo.txt")
            Dim sText As String = ""
            ' If IO.File.Exists(sFile) Then sText = IO.File.ReadAllText(sFile)
            If sFile IsNot Nothing Then sText = Await sFile.ReadAllTextAsync
            If String.IsNullOrEmpty(sText) Then
                Await vb14.DialogBoxAsync("Folder '" & sDir & "', cannot open userinfo.txt? Skipping...")
                Continue For
            End If

            Dim oItem As New Vblib.LocalChannel
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
        Dim kanaly As New ObservableCollection(Of Vblib.LocalChannel)

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
            Await vb14.DialogBoxAsync(sError)
            Return Nothing
        End If

        Return lDirNames

    End Function

    Private Async Function RecoverUsingDirectory(oFoldRoot As Windows.Storage.StorageFolder) As Task(Of List(Of String))
        Dim kanaly As New ObservableCollection(Of Vblib.LocalChannel)

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
            Await vb14.DialogBoxAsync(sError)
            Return Nothing
        End If

        Return lDirNames

    End Function

    Private Async Function RecoverUsingCmdLine(oFoldRoot As Windows.Storage.StorageFolder) As Task(Of List(Of String))
        Dim kanaly As New ObservableCollection(Of Vblib.LocalChannel)

        vb14.ClipPut("dir /b /a:d > dirki.txt")
        Await vb14.DialogBoxAsync("za chwilę zrób cmdline z clipboard")

        ' Dim oFoldRoot As Windows.Storage.StorageFolder = Await Windows.Storage.StorageFolder.GetFolderFromPathAsync(sFoldRoot)
        oFoldRoot.OpenExplorer()

        Await vb14.DialogBoxAsync("naciśnij po wykonaniu komendy (gdy będzie plik - indeks")

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

    Private Async Function TryRecoverAnyMethod(sFoldRoot As Windows.Storage.StorageFolder) As Task(Of ObservableCollection(Of Vblib.LocalChannel))

        Dim lDirNames As List(Of String) = Nothing

        ' lista folderów odtworzenie uzywajac iterowania folderów (UWP) - E_FAIL
        ' lDirNames = Await RecoverUsingGetFolders(oFoldRoot)

        ' odtworzenie uzywajac iterowania folderów (System.IO) - Unknown Error
        ' If lDirNames Is Nothing Then lDirNames = Await RecoverUsingDirectory(oFoldRoot)

        ' odtworzenie uzywajac dir/b
        If lDirNames Is Nothing Then lDirNames = Await RecoverUsingCmdLine(sFoldRoot)

        If lDirNames Is Nothing OrElse lDirNames.Count = 0 Then Return Nothing

        Return Await RecoverChannels(sFoldRoot, lDirNames)

    End Function

    Private Async Sub uiRepair_Click(sender As Object, e As RoutedEventArgs)
        If Not Await vb14.DialogBoxYNAsync("Odtwarzać plik channels.json?") Then Return

        ' Dim sFoldRoot As String = App.gInstaModule.GetPicRootDir()
        Dim sFoldRoot As Windows.Storage.StorageFolder = Await GetPicRootDirAsync()
        ' If sFoldRoot = "" Then
        If sFoldRoot Is Nothing Then
            Await vb14.DialogBoxAsync("Cannot get folder for pictures")
            Return
        End If

        Dim kanaly As ObservableCollection(Of Vblib.LocalChannel)
        kanaly = Await TryRecoverAnyMethod(sFoldRoot)

        If kanaly Is Nothing Then
            vb14.DialogBox("Sorry, ale się nie udało w żaden sposób")
        Else
            Await vb14.DialogBoxAsync("Odzyskałem, próbuję zapisać... ALE NIE UMIEM!")
            _kanaly = kanaly
            ' Await SaveChannelsAsync()

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
