Imports System
Imports System.IO
Imports System.Text.Json

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
    Private Shared ReadOnly SessionPath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "tagitShop",
        "session.json")

    Public Shared Sub SaveSession()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath))
            Dim data = New With {
                .Mode = Mode.ToString(),
                .UserId = UserId,
                .SellerId = SellerId,
                .StoreName = StoreName,
                .AccessToken = AccessToken,
                .RefreshToken = RefreshToken
            }
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(data))
        Catch
            ' Session persistence is best-effort; never block login on disk errors
        End Try
    End Sub

    Public Shared Function TryRestoreSession() As Boolean
        Try
            If Not File.Exists(SessionPath) Then Return False
            Dim json = File.ReadAllText(SessionPath)
            Using doc = JsonDocument.Parse(json)
                Dim root = doc.RootElement
                If root.TryGetProperty("Mode", Nothing) Then
                    Mode = If(root.GetProperty("Mode").GetString() = "Live", StoreMode.Live, StoreMode.Demo)
                End If
                UserId = GetString(root, "UserId")
                SellerId = GetString(root, "SellerId")
                StoreName = GetString(root, "StoreName")
                AccessToken = GetString(root, "AccessToken")
                RefreshToken = GetString(root, "RefreshToken")
            End Using
            Return Not String.IsNullOrWhiteSpace(AccessToken) AndAlso Not String.IsNullOrWhiteSpace(SellerId)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function GetString(element As JsonElement, name As String) As String
        If element.TryGetProperty(name, Nothing) Then
            Return element.GetProperty(name).GetString()
        End If
        Return String.Empty
    End Function

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
        Try
            If File.Exists(SessionPath) Then File.Delete(SessionPath)
        Catch
        End Try
    End Sub
End Class