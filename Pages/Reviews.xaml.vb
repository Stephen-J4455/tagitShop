Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Reviews
    Inherits Page

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        LoadingIndicator.Visibility = Visibility.Visible
        EmptyReviewsState.Visibility = Visibility.Collapsed
        ReviewList.Visibility = Visibility.Collapsed
        Try
            Await DataCache.EnsureLoadedAsync()
            Dim reviews = DataCache.Reviews
            ReviewList.ItemsSource = reviews
            ReviewList.Visibility = If(reviews.Count = 0, Visibility.Collapsed, Visibility.Visible)
            EmptyReviewsState.Visibility = If(reviews.Count = 0, Visibility.Visible, Visibility.Collapsed)
            TxtReviewCount.Text = reviews.Count.ToString() & If(reviews.Count = 1, " review", " reviews")
            TxtReviewFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Showing approved reviews from your store products.", "Demo reviews are local sample data.")
        Catch ex As Exception
            ReviewList.ItemsSource = New List(Of LocalData.ReviewItem)()
            TxtReviewCount.Text = "0 reviews"
            TxtReviewFeedback.Text = ex.Message
            TxtEmptyReviewsTitle.Text = "Reviews could not be loaded"
            TxtEmptyReviewsMessage.Text = ex.Message
            EmptyReviewsState.Visibility = Visibility.Visible
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Sub
End Class