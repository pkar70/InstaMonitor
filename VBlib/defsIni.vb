
' ponieważ NIE DZIAŁA pod Uno.Android wczytywanie pliku (apk nie jest rozpakowany?),
' to w ten sposób przekazywanie zawartości pliku INI
' wychodzi na to samo, edycja pliku defaults.ini albo defsIni.lib.vb

Public Class IniLikeDefaults

    Public Const sIniContent As String = "
[main]
TimerInterval=120

# remark
' remark
; remark
// remark

[debug]
key=value # remark

[app]
; lista z app (bez ustawiania)
lastRun=    # SetSettingsString(x, DateTime.Now.ToString('yyyy.MM.dd HH:mm'))
addedChannels=0 # SetSettingsInt(X, iNewCnt)
uiUserName= #        mUserLogin.UserName = GetSettingsString(X, App.gmDefaultLoginName)
uiPassword= #        mUserLogin.Password = GetSettingsString(X)
instagramUserId=    # GetSettingsLong()
autoRead=false      # uiClockRead.GetSettingsBool(X)
lastRefresh=    #        SetSettingsDate()
addedChannels=0 #  vb14.SetSettingsInt(, 0)


[libs]
; lista z pkarmodule
remoteSystemDisabled=false
appFailData=
offline=false
lastPolnocnyTry=
lastPolnocnyOk=

"

End Class

'H:\Home\PIOTR\VStudio\_Vs2017\InstaMonitor\InstaMonitor\App.xaml.vb 9 KB Visual Basic Source file 18/10/2021 08:33:25 16/5/2020 09:22:39 21/4/2022 20:52:35 4
'96             Dim iNewCnt As Integer = GetSettingsInt("addedChannels")
'98             SetSettingsInt("addedChannels", iNewCnt)
'147     '                        Dim iNewCnt As Integer = GetSettingsInt("addedChannels")
'149     '                        SetSettingsInt("addedChannels", iNewCnt)

'H:\Home\PIOTR\VStudio\_Vs2017\InstaMonitor\InstaMonitor\InstaFeeds.xaml.vb 13 KB Visual Basic Source file 18/10/2021 08:33:59 16/5/2020 09:49:25 21/4/2022 20:52:35 4
'46         GetSettingsString(uiUserName, "uiUserName", App.gmDefaultLoginName)
'47         GetSettingsString(uiPassword, "uiPassword")
'340         SetSettingsString(uiUserName, "uiUserName")
'341         SetSettingsString(uiPassword, "uiPassword")

'H:\Home\PIOTR\VStudio\_Vs2017\InstaMonitor\InstaMonitor\InstaModule.vb 42 KB Visual Basic Source file 13/1/2022 18:43:14 16/5/2020 09:35:15 21/4/2022 20:43:05 8
'504         SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
'511         'SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
'565         mUserLogin.UserName = GetSettingsString("uiUserName", App.gmDefaultLoginName)
'566         mUserLogin.Password = GetSettingsString("uiPassword")
'725         Dim iUserId As Long = GetSettingsLong("instagramUserId")
'732             SetSettingsLong("instagramUserId", iUserId)
'835         SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
'842         'SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))

'H:\Home\PIOTR\VStudio\_Vs2017\InstaMonitor\InstaMonitor\MainPage.xaml.vb 30 KB Visual Basic Source file 1/12/2021 12:17:34 16/5/2020 09:22:39 21/4/2022 20:52:35 6
'139         uiClockRead.IsChecked = GetSettingsBool("autoRead")
'140         uiLastRun.Text = GetSettingsString("lastRun")
'535             RegisterTimerTrigger("InstaMonitorTimer", GetSettingsInt("TimerInterval", 120))
'537         SetSettingsBool("autoRead", uiClockRead.IsChecked)
'563         SetSettingsDate("lastRefresh")
'564         SetSettingsInt("addedChannels", 0)


'H:\Home\PIOTR\VStudio\_Vs2017\InstaMonitor\InstaMonitor\RefreshWebView.xaml.vb 7 KB Visual Basic Source file 18/10/2021 08:35:42 3/12/2020 14:57:48 21/4/2022 20:52:35 2
'81                 SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))
'131                 SetSettingsString("lastRun", DateTime.Now.ToString("yyyy.MM.dd HH:mm"))

