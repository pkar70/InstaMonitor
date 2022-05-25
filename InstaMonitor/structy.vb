
' większośc w vblib

#Region "local files"

Public Class LocalNewPicture
    Public Property oChannel As Vblib.LocalChannel
    Public Property oPicture As Vblib.LocalPictureData
    Public Property oImageSrc As BitmapImage = Nothing  ' <-- to blokuje przeniesienie do VBlib
    Public Property sDymek As String = ""
    Public Property sData As String = ""

End Class
#End Region