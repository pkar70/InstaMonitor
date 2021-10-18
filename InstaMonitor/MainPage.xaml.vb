
' 2021.10.14: w Settings (InstaFeeds.xaml) jest flyout do zmiany hasła (za często się pojawiała konieczność zmiany)

'russian_barbie
'Angelica Kenova

'https://www.instagram.com/russian_barbie/

' jak mozna przenosic app z UWP do WinUI - przez biblioteke, wspólną dla obu
' https://nicksnettravels.builttoroam.com/upgrade-uwp-to-winui/

Imports Windows.ApplicationModel.Background

Public NotInheritable Class MainPage
    Inherits Page

    Public _listaPickow As List(Of LocalNewPicture)
    Private _lastPicShowed As LocalNewPicture = Nothing

    Private Sub PrzekonwertujNaListaPickow(lZNowosci As List(Of LocalNewPicture))
        If lZNowosci Is Nothing Then
            _listaPickow = New List(Of LocalNewPicture)
            Return
        End If

        _listaPickow = lZNowosci

    End Sub
    ''' <summary>
    ''' przepisz z wybranego channel wszystkie obrazki do _listaPickow, sortując DESC wedle iTimestamp (w pliku .json)
    ''' (sprawdzajac, czy picek istnieje na dysku)
    ''' </summary>
    Private Async Function PrzekonwertujNaListaPickow(oChannel As LocalChannel) As Task(Of Integer)
        _listaPickow = New List(Of LocalNewPicture)

        If oChannel Is Nothing Then Return 0

        Dim oPicki As List(Of LocalPictureData) = Await LoadPictData(oChannel, Nothing)
        If oPicki Is Nothing Then Return 0

        Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oChannel, Nothing)

        For Each oItem As LocalPictureData In From c In oPicki Order By c.iTimestamp Descending
            Dim bBylo As Boolean = False
            For Each oJuz As LocalNewPicture In _listaPickow
                If oJuz.oPicture.sFileName = oItem.sFileName Then
                    bBylo = True
                    Exit For
                End If
            Next

            If bBylo Then Continue For

            ' weryfikacja istnienia pliku
            If oFold IsNot Nothing Then
                If Not Await oFold.FileExistsAsync(oItem.sFileName) Then Continue For
            End If

            ' 2021.02.25: dodaję linijkę z iTimestamp, by wyłapać czemu nie działa tak jak trzeba
            Dim oNew As New LocalNewPicture With
                {
                .oChannel = oChannel,
                .oPicture = oItem,
                .sDymek = oItem.sData & vbCrLf & "(" & oItem.iTimestamp & "=" &
                    DateTimeOffset.FromUnixTimeSeconds(oItem.iTimestamp).ToString("yyyy-MM-dd") & ")",
                .sData = oItem.sData
                }
            _listaPickow.Add(oNew)
        Next

        Return _listaPickow.Count
    End Function

    Private Async Function DoczytajPicki(iIlePickow As Integer) As Task(Of Integer)
        ' 2021.02.25: ProgRing(Bar) sobie idzie

        Dim iPicCnt As Integer = 0
        Dim sFiltr As String = ""
        If iIlePickow < 1 Then
            iIlePickow = 10000 ' gdy kanał ma błąd (-1), albo gdy nie ma nowych (0), pokaż wszystkie
            ProgRingMaxVal(_listaPickow.Count)
        Else
            ProgRingMaxVal(iIlePickow)
            sFiltr = DateTime.Now.ToString("yyyy-MM-dd")
        End If
        ProgRingVal(0)  ' ustawiamy na początek (to powinno i tak się zdarzyć w ProgRingShow)

        'Dim iMaxCnt As Integer = _listaPickow.Count

        ' 2021.02.25: obrócenie kolejności - wszak "sort od tyłu" jest już zrobione krok wcześniej
        ' For iLp As Integer = _listaPickow.Count - 1 To 0 Step -1
        For iLp As Integer = 0 To _listaPickow.Count - 1
            Dim oItem As LocalNewPicture = _listaPickow.ElementAt(iLp)

            '2021.09.26 filtrujemy wedle dzisiaj, jeśli mamy ogranicznik i jeśli obrazek ma datę
            If sFiltr <> "" AndAlso Not String.IsNullOrEmpty(oItem.oPicture.sData) AndAlso
                oItem.oPicture.sData <> sFiltr Then Continue For

            ' For Each oItem In _listaPickow - poprzednia wersja, ktora robila obrazki od najstarszego!

            Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oItem.oChannel, Nothing)
            If oFold IsNot Nothing Then
                ' błąd, ale nie RETURN, kontynuuj, bo może reszta będzie dobrze

                Dim oFile As Windows.Storage.StorageFile = Await oFold.TryGetItemAsync(oItem.oPicture.sFileName)
                If oFile IsNot Nothing Then
                    ' pliku może nie być - usunięty obrazek, ale nie usunięte dane z JSON
                    iPicCnt += 1

                    oItem.oImageSrc = New BitmapImage
                    Dim oStream As Stream = Await oFile.OpenStreamForReadAsync
                    ' zawisa na tym setsource
                    If oStream IsNot Nothing Then
                        Await oItem.oImageSrc.SetSourceAsync(oStream.AsRandomAccessStream)
                        oStream.Dispose()
                    End If

                    ' 2021.02.25: uzupełnienie pola, którego czasami nie ma
                    If String.IsNullOrEmpty(oItem.oPicture.sData) Then
                        Dim dData As DateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(oItem.oPicture.iTimestamp)
                        oItem.oPicture.sData = dData.ToString("yyyy-MM-dd")
                    End If

                    iIlePickow -= 1
                    ' i jesli wczytał wszystkie plus jeden (na wszelki wypadek), to kończ waść - szybsze przy wczytywaniu podczas kontroli co nowego
                    If iIlePickow <0 Then Exit For
                    ProgRingInc()
                End If

            End If

        Next

        Return iPicCnt

    End Function

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        uiClockRead.IsChecked = GetSettingsBool("autoRead")
        uiLastRun.Text = GetSettingsString("lastRun")

        ProgRingInit(True, True)

        Await CrashMessageShowAsync()


        If Not Await LoadChannelsAsync() Then
            DialogBox("Empty channel list")
            Return
        End If

        UpdateDisplayName()   ' wraz z licznikiem nowości

        ' jesli wracam tu z RefreshWebView to gdzies pewnie bedzie  c.iNewCnt <> 0 - wtedy pokazuj tylko zmienione
        Dim bShowAll As Boolean = True
        For Each oItem As LocalChannel In GetInstaChannelsList()
            If oItem.iNewCnt <> 0 Then
                bShowAll = False
                Exit For
            End If
        Next
        uiFiltr.IsChecked = bShowAll


        ShowListaKanalow()

        'If App._gNowosci.Count > 0 Then
        '    PrzekonwertujNaListaPickow(App._gNowosci)
        '    Await DoczytajPicki(0)  ' 0: wszystkie picki
        '    App._gNowosci.Clear()
        '    uiChannelName.Text = "** nowości **"
        '    uiPicList.ItemsSource = From c In _listaPickow Where c.oImageSrc IsNot Nothing
        'End If

    End Sub

    Private miLastNew As Integer = 0
    Private Sub ShowListaKanalow()

        uiMsg.Text = ""

        If uiFiltr.IsChecked Then
            uiChannelsList.ItemsSource = From c In GetInstaChannelsList() Order By c.sChannel
        Else
            uiChannelsList.ItemsSource = From c In GetInstaChannelsList() Where c.iNewCnt <> 0 Order By c.sChannel   ' c.bEnabled = True And 
            Dim iNewCnt As Integer = 0
            For Each oItem As LocalChannel In _kanaly
                If oItem.iNewCnt > 0 Then iNewCnt += oItem.iNewCnt
            Next
            If iNewCnt = 0 AndAlso miLastNew <> 0 Then
                '                ' jeśli doszliśmy do zera, to zapisać trzeba (że nie ma nic nowego)
                '#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
                '                SaveChannelsAsync()
                '#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
            Else
                miLastNew = iNewCnt
                uiMsg.Text = iNewCnt & " new pics"
            End If
        End If

    End Sub

    Private Sub CreateScheduledToast()
        Dim oDate As DateTime = New DateTime(Date.Now.Year, Date.Now.Month, DateTime.Now.Day, 14, 0, 0)
        oDate = oDate.AddDays(7)

        MakeToast(oDate, "Długo nie było ściągania InstaMonitor", "")
    End Sub

    Private Async Function EwentualnieBlokujNieaktywne() As Task
        Dim iErrors As Integer = 0
        Dim sGranica As String = DateTime.Now.AddDays(-45).ToString("yyyyMMdd")


        For Each oChan In GetInstaChannelsList()
            ' jesli mamy pierwszy błąd zapisany
            If oChan.iNewCnt < 0 AndAlso Not String.IsNullOrEmpty(oChan.sFirstError) Then
                ' mamy zapisane dd mm yyyy
                Dim sDateErr As String = oChan.sFirstError.Substring(6, 4) & oChan.sFirstError.Substring(3, 2) & oChan.sFirstError.Substring(0, 2)
                If sDateErr < sGranica Then iErrors += 1
            End If
        Next

        If iErrors < 1 Then Return

        If Not Await DialogBoxYNAsync("Disable " & iErrors & " kanałów błędnych ponad 45 dni?") Then Return

        For Each oChan In GetInstaChannelsList()
            ' jesli mamy pierwszy błąd zapisany
            If oChan.iNewCnt < 0 AndAlso Not String.IsNullOrEmpty(oChan.sFirstError) Then
                ' mamy zapisane dd mm yyyy
                Dim sDateErr As String = oChan.sFirstError.Substring(6, 4) & oChan.sFirstError.Substring(3, 2) & oChan.sFirstError.Substring(0, 2)
                If sDateErr < sGranica Then oChan.bEnabled = False
            End If
        Next

        Await SaveChannelsAsync()

    End Function
    Private Sub uiRefreshWebView_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(RefreshWebView))
    End Sub

#Region "Skopiowane z Ballots, odpowiednio"

    Private Async Function GetDownloadsFolder() As Task(Of Windows.Storage.StorageFolder)
        Dim oFold As Windows.Storage.StorageFolder
        Try
            oFold = Await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFolderAsync("PickedFolderToken")
        Catch ex As Exception
            Return Nothing
        End Try
        Return oFold
    End Function

    Private Async Function PickDownFolder() As Task(Of Windows.Storage.StorageFolder)

        Await DialogBoxAsync("Wskaz Downloads folder")

        Dim picker = New Windows.Storage.Pickers.FolderPicker
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail

        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder
        picker.FileTypeFilter.Add(".jpg")

        Dim oFold As Windows.Storage.StorageFolder
        oFold = Await picker.PickSingleFolderAsync
        If oFold Is Nothing Then Return Nothing

        Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", oFold)

        Return oFold
    End Function
#End Region

    Private Async Sub uiRefreshFromDown_Click(sender As Object, e As RoutedEventArgs)
        ' skoro nie działa ani normalne czytanie (tylko 3 pierwsze jest ok), ani webView (czarny prostokąt), to trzeba to omijać wersją własną

        If Not Await LoadChannelsAsync() Then
            Await DialogBoxAsync("Empty channel list")
            Return
        End If

        ' z katalogu Downloads, pliki "insta.*", wrzuca po kolei do katalogów kanałów, uzupełniając listę.json?
        ' w ten sposob nie trzeba byloby zmieniac struktury Channel.OnClick, na doczytywanie/iterowanie z katalogu

        Dim oFoldFrom As Windows.Storage.StorageFolder = Await GetDownloadsFolder()
        If oFoldFrom Is Nothing Then
            oFoldFrom = Await PickDownFolder()
            If oFoldFrom Is Nothing Then
                DialogBox("No nie dam rady, coś nie tak z Downloads folder")
            End If
        End If

        ProgRingShow(True)

        ' iterowanie plików
        Dim oNewFiles As IReadOnlyList(Of Windows.Storage.StorageFile) = Await oFoldFrom.GetFilesAsync()
        Dim oPicList As List(Of LocalPictureData) = Nothing

        Dim sLastChann As String = ""
        Dim oChannel As LocalChannel = Nothing
        Dim bDirty As Boolean = False
        Dim oFoldTo As Windows.Storage.StorageFolder = Nothing

        'w ten sposób lista sie zmieni, nawet jak pliki będziem przenosic
        For Each oNewFile As Windows.Storage.StorageFile In oNewFiles
            If oNewFile.Name.ToLower.StartsWith("insta.") Then
                ' to prawdopodobnie nasze

                ' wyciągnij nazwę kanału
                Dim sChannel As String = oNewFile.Name.ToLower.Substring("insta.".Length)
                Dim iInd As Integer = sChannel.IndexOf("..")
                If iInd < 1 Then
                    Await DialogBoxAsync("Nie ma .. w nazwie obrazka" & vbCrLf & oNewFile.Name)
                    Continue For
                End If
                sChannel = sChannel.Substring(0, iInd)

                uiMsg.Text = sChannel   ' nie obrazek, tylko kanał - tak ładniej

                If sChannel <> sLastChann Then
                    If sLastChann <> "" Then
                        ' zapisz plik JSON wedle oPicList
                        Await SavePictData(oChannel, oPicList, uiMsg)
                    End If
                    sLastChann = sChannel

                    ' znajdz oItem do tego
                    oChannel = GetInstaChannelItem(sChannel)
                    If oChannel Is Nothing Then
                        If Not Await DialogBoxYNAsync("Nie moge znalezc Item dla kanału " & sChannel & vbCrLf & "Dodac?") Then
                            Continue For
                        Else
                            Await TryAddChannelAsync(sChannel, uiMsg)
                            oChannel = GetInstaChannelItem(sChannel)
                        End If
                    End If

                    ' wczytaj plik json kanalu
                    oPicList = Await LoadPictData(oChannel, uiMsg)
                    If oPicList Is Nothing Then oPicList = New List(Of LocalPictureData)

                    ' znajdz katalog do tego
                    oFoldTo = Await GetChannelDir(oChannel, uiMsg)
                    If oFoldTo Is Nothing Then
                        ProgRingShow(False)
                        Return
                    End If
                End If

                ' uzupelnij plik json
                Dim oNew As LocalPictureData = New LocalPictureData
                oNew.sFileName = oNewFile.Name
                oNew.iTimestamp = oNewFile.DateCreated.ToUnixTimeSeconds ' to jest wazne, bo to jest dla sort!
                ' 1624301998 dla 2021-06-23
                oNew.sPlace = "via Edge"
                oNew.sCaptionAccessib = ""
                oNew.sCaption = ""
                oNew.sData = oNewFile.DateCreated.ToString("yyyy-MM-dd")
                oPicList.Add(oNew)

                ' move pliku obrazka
                Await oNewFile.MoveAsync(oFoldTo)

                ' dodaj jedynke do licznika new kanalu 
                oChannel.iNewCnt += 1
                bDirty = True

            End If
        Next

        If bDirty Then
            Await SaveChannelsAsync() ' zapisz kanały - bo zmienione jest (iNewCnt)
        End If

        UpdateDisplayName()   ' wraz z licznikiem nowości
        uiFiltr.IsChecked = False   ' tylko z nowosciami
        ShowListaKanalow()

        ProgRingShow(False)
    End Sub


    Private Async Function SelectChannel(oItem As LocalChannel) As Task
        ProgRingShow(True)

        uiFullPicture.Visibility = Visibility.Collapsed
        uiPicOpis.Text = ""
        uiPicDel.IsEnabled = False

        uiChannelName.Text = oItem.sChannel & " (" & oItem.sFullName & ")"

        oItem.iPicCnt = Await PrzekonwertujNaListaPickow(oItem)
        ' ewentualnie moze to wydluza? 

        Await DoczytajPicki(oItem.iNewCnt)

        ZrobDymkiKanalow()  ' ale to i tak nie poprawia nic... bo nie ma aktualizacji ItemsSource po zmianie dymków

        uiPicList.ItemsSource = From c In _listaPickow Where c.oImageSrc IsNot Nothing Order By c.oPicture.iTimestamp Descending Distinct

        oItem.iNewCnt = 0

        Dim iTotalNew As Integer = 0
        ' musimy ominąć iNewCnt = -1 (sygnalizacja błędu)
        For Each oChan In From c In _kanaly Where c.iNewCnt > 0
            iTotalNew += oChan.iNewCnt
        Next

        ProgRingShow(False)
    End Function


    Private Async Sub uiPicDel_Click(sender As Object, e As RoutedEventArgs)
        ' DataContext ustawiany przy uiPictureClick
        Dim oButt As Button = sender
        If oButt.DataContext Is Nothing Then Return

        Dim oItem As LocalNewPicture = oButt.DataContext

        ' usun plik
        ' 2021.01.20: zamiast IFów jest ?
        Dim oFold As Windows.Storage.StorageFolder = Await GetChannelDir(oItem.oChannel, Nothing)
        Dim oFile As Windows.Storage.StorageFile = Await oFold?.TryGetItemAsync(oItem.oPicture.sFileName)
#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        oFile?.DeleteAsync() ' kontynuuj usuwanie sobie w tle...
#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed

        ' znajdz następny do pokazania (2021.01.20)
        Dim oListek = From c In _listaPickow Where c.oImageSrc IsNot Nothing Order By c.oPicture.iTimestamp Descending
        Dim bFound As Boolean = False
        Dim oNextItem As LocalNewPicture = Nothing
        Dim iLoop As Integer
        For iLoop = 0 To oListek.Count - 2
            If oListek.ElementAt(iLoop).oPicture.sFileName = oItem.oPicture.sFileName Then
                bFound = True
                Exit For
            End If
        Next
        If bFound Then oNextItem = oListek.ElementAt(iLoop + 1)

        ' usun go z listy do pokazywania
        oItem.oImageSrc = Nothing
        uiPicDel.IsEnabled = False

        ' no i przestan pokazywac
        uiFullPicture.Source = Nothing

        uiPicList.ItemsSource = From c In _listaPickow Where c.oImageSrc IsNot Nothing Order By c.oPicture.iTimestamp Descending

        ' ze niby od razu klikam na następnym - czyli poniekąd kopia uiPictureClick (2021.01.20)
        If oNextItem IsNot Nothing Then
            uiFullPicture.Visibility = Visibility.Visible
            uiFullPicture.Source = oNextItem.oImageSrc
            uiPicOpis.Text = oNextItem.oPicture.sCaption & " (" & oNextItem.oPicture.sPlace & ")"
            uiPicDel.IsEnabled = True
            uiPicDel.DataContext = oNextItem
            _lastPicShowed = oNextItem
        End If

    End Sub


    Private Sub uiCopyOpis_Click(sender As Object, e As RoutedEventArgs)
        If uiPicDel.DataContext Is Nothing Then Return

        Dim oItem As LocalNewPicture = uiPicDel.DataContext
        ClipPut(uiPicOpis.Text)
    End Sub

    Private Sub UpdateDisplayName()
        For Each oItem As LocalChannel In GetInstaChannelsList()
            If oItem.iNewCnt > 0 Then
                oItem.sDisplayName = oItem.sChannel & " (" & oItem.iNewCnt & ")"
            Else
                oItem.sDisplayName = oItem.sChannel
            End If
        Next
    End Sub


#Region "Bottom Command Bar"
    Private Async Sub uiAddChannel_Click(sender As Object, e As RoutedEventArgs)
        Dim sNewChannel As String = Await DialogBoxInputDirectAsync("Podaj nazwę kanału:")
        If sNewChannel = "" Then Return

        For Each oItem As LocalChannel In _kanaly
            If oItem.sChannel.ToLower = sNewChannel.ToLower Then
                DialogBox("Taki kanał już istnieje!")
                Return
            End If
        Next

        ProgRingShow(True)
        Dim sError As String = Await TryAddChannelAsync(sNewChannel, uiMsg)  ' w srodku jest zapis kanałów
        ProgRingShow(False)
        If Not sError.StartsWith("OK") Then
            DialogBox("Error: " & sError)
            Return
        End If

        If Not Await DialogBoxYNAsync("Wczytać fotki?") Then Return

        For Each oItem As LocalChannel In _kanaly
            If oItem.sChannel.ToLower = sNewChannel.ToLower Then
                ProgRingShow(True)
                Await InstaNugetCheckNewsFromUserAsync(oItem, uiMsg)
                Await SelectChannel(oItem)
                ProgRingShow(False)
                Return
            End If
        Next

    End Sub

    Private Sub uiFiltr_Click(sender As Object, e As RoutedEventArgs)
        ShowListaKanalow()
#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        If uiFiltr.IsChecked Then SaveChannelsAsync()
#Enable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
    End Sub


    Private Async Sub uiClockRead_Click(sender As Object, e As RoutedEventArgs)

        If Not Await CanRegisterTriggersAsync() Then
            DialogBox("Background unavailable for app")
            Return
        End If

        UnregisterTriggers("InstaMonitor")  ' usuwamy istniejące - ewentualnie zarejestrujemy ponownie

        If uiClockRead.IsChecked Then
            RegisterTimerTrigger("InstaMonitorTimer", GetSettingsInt("TimerInterval", 120))
        End If
        SetSettingsBool("autoRead", uiClockRead.IsChecked)
    End Sub

    Private Async Sub uiOpenExpl_Click(sender As Object, e As RoutedEventArgs)
        Dim oFold As Windows.Storage.StorageFolder = Await GetPicRootDirAsync()
        oFold.OpenExplorer
    End Sub
    Private Sub uiSetup_Click(sender As Object, e As RoutedEventArgs)
        Me.Frame.Navigate(GetType(InstaFeeds))
    End Sub

    Private Async Sub uiRefresh_Click(sender As Object, e As RoutedEventArgs)
        'Me.Frame.Navigate(GetType(RefreshWebView))
        ProgRingShow(True)

        RemoveScheduledToasts()

        'Await GetAllFeedsAsync(uiMsg)
        Await InstaNugetRefreshAll(uiMsg)

        Await EwentualnieBlokujNieaktywne()

        UpdateDisplayName()   ' wraz z licznikiem nowości
        uiFiltr.IsChecked = False   ' tylko wedle licznika
        ShowListaKanalow()

        SetSettingsDate("lastRefresh")
        SetSettingsInt("addedChannels", 0)

        CreateScheduledToast()


        ProgRingShow(False)
    End Sub

#End Region

#Region "lista kanałów"
    Private Async Sub uiRefreshThis_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As LocalChannel = oMFI.DataContext

        ProgRingShow(True)
        Dim iRet As Integer = Await InstaNugetCheckNewsFromUserAsync(oItem, uiMsg)
        ' Dim iRet As Integer = Await GetInstagramFeed(oItem, True)
        If iRet < 0 Then
            oItem.iNewCnt = -1
            If oItem.sFirstError = "" Then oItem.sFirstError = DateTime.Now.ToString("dd MM yyyy")
        ElseIf iRet > 0 Then
            oItem.sFirstError = ""  ' kanał już nie daje błędów
            oItem.iNewCnt = iRet
        End If
#Disable Warning BC42358 ' Because this call is not awaited, execution of the current method continues before the call is completed
        SaveChannelsAsync()   ' tak, bez await, niech sobie to robi w tle
#Enable Warning BC42358
        ProgRingShow(False)

        If iRet > 0 Then
            ' 2021.07.09: jeśli coś nowego, to odśwież listę obrazków
            Await SelectChannel(oItem)
        End If
        ''UpdateDisplayName()   ' wraz z licznikiem nowości
        ''uiFiltr.IsChecked = False   ' pokaz tylko nowe

        'ShowListaKanalow()
    End Sub

    Private Sub uiGoWebThis_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As LocalChannel = oMFI.DataContext

        Dim sUrl As String = "https://www.instagram.com/" & oItem.sChannel
        OpenBrowser(sUrl)

    End Sub
    Private Async Sub uiDisableThis_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As LocalChannel = oMFI.DataContext
        If Not Await DialogBoxYNAsync("Zablokować kanał '" & oItem.sChannel & "' ?") Then Return

        oItem.bEnabled = False
        Await SaveChannelsAsync()   ' tak, bez await, niech sobie to robi w tle
        ' ale jednak z AWAIT, jakby wylecial pozniej zanim zdąży zapisać
        ' ShowListaKanalow() - a po co? przeskakuje na początek, co jest bez sensu, a i tak na liście jest ten usunięty :)
    End Sub
    Private Async Sub uiReadDetailsThis_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As LocalChannel = oMFI.DataContext
        If oItem.sBiografia <> "" OrElse oItem.sFullName <> "" Then
            If Not Await DialogBoxYNAsync("Ale już mamy dane, na pewno odczytać ponownie?") Then Return
        End If

        If Not Await InstaNugetLoginAsync(uiMsg) Then Return

        Dim user As JSONinstaUser = Await InstaNugetGetUserDataAsync(oItem.sChannel)
        If user Is Nothing Then
            DialogBox("Nie mogę sprawdzić danych kanału, może go już nie ma?")
            Return
        End If

        Dim sZmieniono As String = ""
        If user.biography <> oItem.sBiografia Then
            oItem.sBiografia = user.biography
            sZmieniono += "Biografia"
        End If

        If user.full_name <> oItem.sFullName Then
            oItem.sFullName = user.full_name
            sZmieniono += " FullName"
        End If

        If sZmieniono = "" Then
            DialogBox("Nic się nie zmieniło...")
            Return
        End If

        DialogBox("Zmiana: " & sZmieniono)
        Await SaveChannelsAsync()

    End Sub

    Private Sub uiShowDetailsThis_Click(sender As Object, e As RoutedEventArgs)
        Dim oMFI As MenuFlyoutItem = sender
        Dim oItem As LocalChannel = oMFI.DataContext
        Dim sMsg As String = oItem.sChannel & vbCrLf & vbCrLf &
            oItem.sFullName & vbCrLf &
            "Bio:" & vbCrLf & oItem.sBiografia
        DialogBox(sMsg)
    End Sub

    Private Async Sub uiChannel_Click(sender As Object, e As TappedRoutedEventArgs)
        Dim oMFI As Grid = sender
        Dim oItem As LocalChannel = oMFI.DataContext

        Await SelectChannel(oItem)
    End Sub


#End Region

#Region "lista obrazków"
    Private Sub uiPicture_Click(sender As Object, e As TappedRoutedEventArgs)
        Dim oMFI As Grid = sender
        Dim oItem As LocalNewPicture = oMFI.DataContext
        uiFullPicture.Visibility = Visibility.Visible
        uiFullPicture.Source = oItem.oImageSrc
        uiPicOpis.Text = oItem.oPicture.sCaption & " (" & oItem.oPicture.sPlace & ")"
        uiPicDel.IsEnabled = True
        uiPicDel.DataContext = oItem
        _lastPicShowed = oItem
    End Sub

#End Region

#Region "obrazek"
    Private Sub uiPicCopyOpis_Tapped(sender As Object, e As RoutedEventArgs)
        ClipPut(_lastPicShowed.oPicture.sCaption & " (" & _lastPicShowed.oPicture.sPlace & ")")
    End Sub
    Private Sub uiPic_Tapped(sender As Object, e As RoutedEventArgs)
        Dim oResize As Stretch = uiFullPicture.Stretch
        Select Case oResize
            Case Stretch.Uniform
                uiFullPicture.Stretch = Stretch.None
            Case Stretch.None
                uiFullPicture.Stretch = Stretch.Uniform
        End Select

    End Sub
    Private Sub uiPicDelFromMenu_Click(sender As Object, e As RoutedEventArgs)

    End Sub

    Private Async Sub uiCopyPath_Click(sender As Object, e As RoutedEventArgs)
        If uiPicDel.DataContext Is Nothing Then Return

        Dim oItem As LocalNewPicture = uiPicDel.DataContext
        ClipPut((Await GetPicRootDirAsync()).Path & "\" & oItem.oChannel.sDirName & "\" & oItem.oPicture.sFileName)
    End Sub
    Private Sub uiRotateAntiClock_Click(sender As Object, e As RoutedEventArgs)
        'If uiPicDel.DataContext Is Nothing Then Return

        'Dim oBmp As BitmapImage = uiPicDel.DataContext.oImageSrc

        DialogBox("jeszcze nie umiem obracać dziewczynek")

    End Sub
    Private Sub uiRotateClock_Click(sender As Object, e As RoutedEventArgs)
        'If uiPicDel.DataContext Is Nothing Then Return

        'Dim oBmp As BitmapImage = uiPicDel.DataContext.oImageSrc

        DialogBox("jeszcze nie umiem obracać dziewczynek")

    End Sub


#End Region
End Class
