Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports System.Windows.Input
Imports System.Windows.Media

Partial Public Class Reels
    Inherits Page

    Private Shared ReadOnly VideoClient As New HttpClient() With {.Timeout = TimeSpan.FromMinutes(5)}
    Private activeMedia As MediaElement

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
            Dim productReels = DataCache.Products.
                Where(Function(product) String.Equals(product.status, "active", StringComparison.OrdinalIgnoreCase)).
                Where(Function(product) Not String.IsNullOrWhiteSpace(SupabaseService.ResolveVideoUrl(product.video_url, product.r2_video_key))).
                Select(Function(product) New SupabaseService.ReelItem With {
                    .id = "product-" & product.id,
                    .source_id = product.id,
                    .source_type = "product",
                .title = product.title,
                .video_url = SupabaseService.ResolveVideoUrl(product.video_url, product.r2_video_key),
                .r2_key = product.r2_video_key,
                .thumbnail_url = If(Not String.IsNullOrWhiteSpace(product.thumbnail), product.thumbnail, If(product.thumbnails IsNot Nothing AndAlso product.thumbnails.Count > 0, product.thumbnails(0), Nothing)),
                .views = product.sold_count,
                .likes = 0,
                .time = product.updated_at.ToString("O"),
                .created_at = product.updated_at.ToString("O")
            })

            Dim uploadedReels = DataCache.Reels.Select(Function(r) New SupabaseService.ReelItem With {
                .id = r.id,
                .source_id = r.id,
                .source_type = "reel",
                .title = r.title,
                .video_url = SupabaseService.ResolveVideoUrl(r.video_url, r.r2_key),
                .r2_key = r.r2_key,
                .thumbnail_url = r.thumbnail_url,
                .views = r.views,
                .likes = r.likes,
                .time = r.created_at,
                .created_at = r.created_at
            })

            Dim reels = productReels.Concat(uploadedReels).
                GroupBy(Function(r) r.id).
                Select(Function(group) group.First()).
                OrderByDescending(Function(r) r.created_at).
                ToList()
            ReelsGrid.ItemsSource = reels
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

    Private Sub BtnPlayReel_Click(sender As Object, e As RoutedEventArgs)
        PlayReelAsync(sender)
    End Sub

    Private Async Sub PlayReelAsync(sender As Object)
        Dim btn = TryCast(sender, Button)
        If btn Is Nothing Then Return
        Dim reel = TryCast(btn.DataContext, SupabaseService.ReelItem)
        If reel Is Nothing Then Return

        Dim sourceUrl = SupabaseService.ResolveVideoUrl(reel.video_url, reel.r2_key)
        If String.IsNullOrWhiteSpace(sourceUrl) Then
            TxtStatus.Text = "This reel does not have a playable video source."
            Return
        End If

        ' Find the MediaElement in the visual tree
        Dim mediaHost = FindVisualParent(Of Grid)(btn)
        Dim media = If(mediaHost Is Nothing, Nothing, FindVisualChild(Of MediaElement)(mediaHost))
        If media IsNot Nothing Then
            Dim uri As Uri = Nothing
            If Not Uri.TryCreate(sourceUrl, UriKind.Absolute, uri) Then
                TxtStatus.Text = "The reel video URL is invalid."
                Return
            End If

            If activeMedia IsNot Nothing AndAlso activeMedia IsNot media Then
                activeMedia.Stop()
                activeMedia.Source = Nothing
                activeMedia.Visibility = Visibility.Collapsed
            End If
            activeMedia = media

            Dim thumbnail = If(mediaHost Is Nothing, Nothing, FindVisualChild(Of Image)(mediaHost))
            media.Visibility = Visibility.Visible
            If thumbnail IsNot Nothing Then thumbnail.Visibility = Visibility.Collapsed
            btn.Visibility = Visibility.Collapsed
            TxtStatus.Text = "Preparing video..."
            media.Stop()
            Try
                media.Source = Nothing
                media.Source = Await GetPlaybackSourceAsync(uri, reel.source_id)
                TxtStatus.Text = "Opening " & If(String.IsNullOrWhiteSpace(reel.title), "reel", reel.title) & "..."
            Catch ex As Exception
                media.Visibility = Visibility.Collapsed
                If thumbnail IsNot Nothing Then thumbnail.Visibility = Visibility.Visible
                btn.Visibility = Visibility.Visible
                TxtStatus.Text = "The reel video could not be downloaded: " & ex.Message
            End Try
        End If
    End Sub

    Private Async Function GetPlaybackSourceAsync(sourceUri As Uri, sourceId As String) As Threading.Tasks.Task(Of Uri)
        If Not sourceUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) AndAlso
           Not sourceUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) Then
            Return sourceUri
        End If

        Dim extension = Path.GetExtension(sourceUri.AbsolutePath).ToLowerInvariant()
        If extension <> ".mp4" AndAlso extension <> ".m4v" AndAlso extension <> ".mov" Then extension = ".mp4"

        Dim hash As String
        Using sha = SHA256.Create()
            hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sourceUri.AbsoluteUri))).ToLowerInvariant()
        End Using

        Dim cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tagitShop",
            "reel-cache")
        Directory.CreateDirectory(cacheDirectory)
        Dim cachePath = Path.Combine(cacheDirectory, If(String.IsNullOrWhiteSpace(sourceId), hash, sourceId) & "-" & hash.Substring(0, 12) & extension)

        If Not File.Exists(cachePath) OrElse New FileInfo(cachePath).Length = 0 Then
            Using response = Await VideoClient.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead)
                response.EnsureSuccessStatusCode()
                Using sourceStream = Await response.Content.ReadAsStreamAsync()
                    Using targetStream As New FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None)
                        Await sourceStream.CopyToAsync(targetStream)
                    End Using
                End Using
            End Using
        End If

        Return New Uri(cachePath, UriKind.Absolute)
    End Function

    Private Async Sub BtnDeleteReel_Click(sender As Object, e As RoutedEventArgs)
        Dim reel = TryCast(DirectCast(sender, FrameworkElement).Tag, SupabaseService.ReelItem)
        If reel Is Nothing Then Return

        Dim description = If(String.IsNullOrWhiteSpace(reel.title), "this video", """" & reel.title & """")
        If MessageBox.Show("Delete " & description & "?" & Environment.NewLine & Environment.NewLine &
                           "This cannot be undone.", "Delete reel", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return

        Try
            If String.Equals(reel.source_type, "product", StringComparison.OrdinalIgnoreCase) Then
                Await SupabaseService.DeleteProductVideoAsync(reel.source_id, reel.video_url, reel.r2_key)
            Else
                Await SupabaseService.DeleteReelAsync(reel.source_id, reel.r2_key)
            End If

            If String.Equals(reel.source_type, "product", StringComparison.OrdinalIgnoreCase) Then
                Await DataCache.RefreshProductsAsync()
            Else
                Await DataCache.RefreshReelsAsync()
            End If
            Await LoadReelsAsync()
            TxtStatus.Text = description & " was deleted."
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Reel could not be deleted", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub ReelMedia_MediaOpened(sender As Object, e As RoutedEventArgs)
        Dim media = TryCast(sender, MediaElement)
        If media Is Nothing Then Return
        media.Visibility = Visibility.Visible
        media.Tag = "playing"
        media.Play()
    End Sub

    Private Sub ReelMedia_MediaFailed(sender As Object, e As ExceptionRoutedEventArgs)
        Dim media = TryCast(sender, MediaElement)
        If media Is Nothing Then Return

        media.Visibility = Visibility.Collapsed
        Dim card = FindVisualParent(Of Border)(media)
        Dim thumbnail = If(card Is Nothing, Nothing, FindVisualChild(Of Image)(card))
        Dim playButton = If(card Is Nothing, Nothing, FindVisualChild(Of Button)(card))
        If thumbnail IsNot Nothing Then thumbnail.Visibility = Visibility.Visible
        If playButton IsNot Nothing Then playButton.Visibility = Visibility.Visible
        Dim detail = If(e.ErrorException Is Nothing, String.Empty, " " & e.ErrorException.Message)
        TxtStatus.Text = "The reel video could not be played." & detail
    End Sub

    Private Sub ReelMedia_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ClickCount <> 2 Then Return
        Dim media = TryCast(sender, MediaElement)
        If media Is Nothing Then Return
        If String.Equals(TryCast(media.Tag, String), "playing", StringComparison.OrdinalIgnoreCase) Then
            media.Pause()
            media.Tag = "paused"
            TxtStatus.Text = "Video paused."
        Else
            media.Play()
            media.Tag = "playing"
            TxtStatus.Text = "Video playing."
        End If
        e.Handled = True
    End Sub

    Private Shared Function FindVisualChild(Of T As DependencyObject)(obj As DependencyObject) As T
        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(obj) - 1
            Dim child = VisualTreeHelper.GetChild(obj, i)
            If TypeOf child Is T Then
                Return DirectCast(child, T)
            End If
            Dim result = FindVisualChild(Of T)(child)
            If result IsNot Nothing Then Return result
        Next
        Return Nothing
    End Function

    Private Shared Function FindVisualParent(Of T As DependencyObject)(obj As DependencyObject) As T
        Dim current = VisualTreeHelper.GetParent(obj)
        While current IsNot Nothing
            If TypeOf current Is T Then Return DirectCast(current, T)
            current = VisualTreeHelper.GetParent(current)
        End While
        Return Nothing
    End Function

End Class
