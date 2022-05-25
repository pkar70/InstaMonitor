
#Region "instagram"

' odtwarzane z DUMP danych
' przy pomocy ladnego rozpisania: https://jsonformatter.curiousconcept.com/

' entry_data / ProfilePage / graphql / user / full_name
'"full_name":" .... "
' entry_data / ProfilePage / graphql / user / profile_pic_url
' "profile_pic_url":"..."
' entry_data / ProfilePage / graphql / user / edge_owner_to_timeline_media / edges [ / node

Public Class JSONinstagram
    Public Property entry_data As JSONinstaEntryData
End Class

Public Class JSONinstaEntryData
    Public Property ProfilePage As List(Of JSONinstaProfileItem)
End Class
Public Class JSONinstaProfileItem
    Public Property graphql As JSONinstaGraph
End Class
Public Class JSONinstaGraph
    ' full implementation
    Public Property user As JSONinstaUser
End Class

Public Class JSONinstaUser
    ' część danych tylko
    Public Property biography As String
    Public Property full_name As String
    Public Property profile_pic_url As String
    Public Property profile_pic_url_hd As String
    Public Property edge_owner_to_timeline_media As JSONinstaTimeline
    Public Property id As Long
End Class
Public Class JSONinstaTimeline
    ' część danych tylko
    Public Property count As Integer
    Public Property edges As List(Of JSONinstaPicEdge)
End Class
Public Class JSONinstaPicEdge
    ' kompletna
    Public Property node As JSONinstaPicNode
End Class
Public Class JSONinstaPicNode
    ' część danych tylko
    Public Property id As String
    Public Property dimensions As JSONinstaDimensions
    Public Property display_url As String
    Public Property accessibility_caption As String
    Public Property edge_media_to_caption As JSONinstaEdgeCaption
    Public Property location As JSONinstaLocation
    Public Property owner As JSONinstaOwner
    Public Property thumbnail_src As String
    Public Property taken_at_timestamp As Integer
    ' display_url
    ' accessibility_caption
    ' edge_media_to_caption / edges [ node / text
    ' location / name   - czyli gdzie zrobiona fotka
    ' thumbnail_src
End Class

Public Class JSONinstaEdgeCaption
    Public Property edges As List(Of JSONinstaNodeCaption)
End Class

Public Class JSONinstaNodeCaption
    Public Property node As JSONinstaNodeCaptionText
End Class
Public Class JSONinstaNodeCaptionText
    Public Property text As String
End Class

Public Class JSONinstaDimensions
    Public Property height As Integer
    Public Property width As Integer
End Class

Public Class JSONinstaOwner
    Public Property id As String
    Public Property username As String
End Class
Public Class JSONinstaLocation
    ' full implementation
    Public Property id As String
    Public Property name As String
    Public Property slug As String
    Public Property has_public_page As Boolean
End Class

#End Region

#Region "local files"
Public Class LocalChannel
    Public Property sChannel As String
    Public Property sFullName As String
    Public Property sDirName As String
    Public Property sBiografia As String
    Public Property sLastId As String
    Public Property bEnabled As Boolean
    Public Property sFirstError As String = ""
    Public Property iPicCnt As Integer = 0
    Public Property sAdded As String = ""
    Public Property iUserId As Long = 0
    Public Property iNewCnt As Integer = 0
    <Newtonsoft.Json.JsonIgnore>
    Public Property sDisplayName As String
    <Newtonsoft.Json.JsonIgnore>
    Public Property sDymek As String

End Class

Public Class LocalPictureData
    Public Property sFileName As String
    Public Property iTimestamp As Integer
    Public Property sPlace As String = ""
    Public Property sCaptionAccessib As String
    Public Property sCaption As String
    Public Property sData As String = ""

End Class

'Public Class LocalNewPicture
'    Public Property oChannel As LocalChannel
'    Public Property oPicture As LocalPictureData
'    Public Property oImageSrc As BitmapImage = Nothing
'    Public Property sDymek As String = ""
'    Public Property sData As String = ""

'End Class
#End Region