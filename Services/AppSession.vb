Imports System

Public Enum StoreMode
    Demo
    Live
End Enum

Public NotInheritable Class AppSession
    Public Shared Property Mode As StoreMode = StoreMode.Demo
    Public Shared Property UserId As String = String.Empty
    Public Shared Property SellerId As String = String.Empty
    Public Shared Property StoreName As String = "tagitShop"
    Public Shared Property AccessToken As String = String.Empty
    Public Shared Property RefreshToken As String = String.Empty

    Private Sub New()
    End Sub

    Public Shared Sub StartDemo()
        ClearLiveSession()
        Mode = StoreMode.Demo
        StoreName = LocalData.Load().Store.name
    End Sub

    Public Shared Sub ClearLiveSession()
        Mode = StoreMode.Demo
        UserId = String.Empty
        SellerId = String.Empty
        AccessToken = String.Empty
        RefreshToken = String.Empty
        StoreName = "tagitShop"
    End Sub
End Class