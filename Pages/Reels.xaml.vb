Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Reels
    Inherits Page

    Public Class ReelItem
        Public Property title As String
        Public Property views As Integer
        Public Property time As String
    End Class

    Public Sub New()
        InitializeComponent()
        LoadLocalReels()
    End Sub

    Private Sub LoadLocalReels()
        Try
            Dim reels = New List(Of ReelItem)() From {
                New ReelItem() With {.title = "Fresh Organic Apples - Farm to Table", .views = 1250, .time = "1 day ago"},
                New ReelItem() With {.title = "Baking Fresh Bread Every Morning", .views = 890, .time = "2 days ago"},
                New ReelItem() With {.title = "Weekend Flash Sale Preview", .views = 2100, .time = "3 days ago"},
                New ReelItem() With {.title = "Meet Our Local Farmers", .views = 650, .time = "1 week ago"},
                New ReelItem() With {.title = "Store Tour & Product Highlights", .views = 1420, .time = "2 weeks ago"}
            }

            ReelsGrid.ItemsSource = reels
            TxtStatus.Text = $"Loaded {reels.Count} reels (local)"
        Catch ex As Exception
            TxtStatus.Text = "Reels load error: " & ex.Message
        End Try
    End Sub

    Private Sub BtnUploadReel_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Upload a short video reel for your store.", "Upload Reel", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class
