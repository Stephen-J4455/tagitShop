Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Reels
    Inherits Page

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await LoadReelsAsync()
    End Sub

    Private Async Function LoadReelsAsync() As Threading.Tasks.Task
        Try
            LoadingIndicator.Visibility = Visibility.Visible
            EmptyReelsState.Visibility = Visibility.Collapsed
            ReelsGrid.Visibility = Visibility.Collapsed

            Await DataCache.EnsureLoadedAsync()
            Dim reels = DataCache.Reels
            ReelsGrid.ItemsSource = reels.OrderByDescending(Function(r) r.created_at).ToList()
            ReelsGrid.Visibility = If(reels.Count = 0, Visibility.Collapsed, Visibility.Visible)
            EmptyReelsState.Visibility = If(reels.Count = 0, Visibility.Visible, Visibility.Collapsed)
            TxtStatus.Text = If(AppSession.Mode = StoreMode.Live, "Live reels from your store.", "Demo reels stored locally.")
        Catch ex As Exception
            TxtStatus.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Sub BtnUploadReel_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Upload a short video reel for your store.", "Upload Reel", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class
