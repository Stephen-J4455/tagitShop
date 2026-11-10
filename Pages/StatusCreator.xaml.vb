Imports System
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class StatusCreator
    Inherits Page

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        TxtFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Status posts to your ExpressMart store followers.", "Status stays in this computer's demo store.")
    End Sub

    Private Async Sub BtnPostStatus_Click(sender As Object, e As RoutedEventArgs)
        Dim statusText = TxtStatusText.Text.Trim()
        If String.IsNullOrWhiteSpace(statusText) Then
            MessageBox.Show("Enter a status message before posting.", "Create Status", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Try
            Await SupabaseService.PostSellerStatusAsync(statusText, "text")
            TxtStatusText.Text = String.Empty
            TxtFeedback.Text = "Status posted."
        Catch ex As Exception
            TxtFeedback.Text = ex.Message
        End Try
    End Sub
End Class
