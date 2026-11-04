Imports System
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class StoreProfile
    Inherits Page

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Try
            Dim settings = Await SupabaseService.LoadStoreSettingsAsync()
            TxtStoreName.Text = settings.name
            TxtStoreEmail.Text = settings.email
            TxtDescription.Text = settings.description
            TxtFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Changes update your ExpressMart seller profile.", "Changes stay in this computer's demo store.")
        Catch ex As Exception
            TxtFeedback.Text = ex.Message
        End Try
    End Sub

    Private Async Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        Dim storeName = TxtStoreName.Text.Trim()
        Dim email = TxtStoreEmail.Text.Trim()
        If String.IsNullOrWhiteSpace(storeName) OrElse String.IsNullOrWhiteSpace(email) Then
            MessageBox.Show("Enter a store name and contact email.", "Store settings", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If
        If Not email.Contains("@") Then
            MessageBox.Show("Enter a valid contact email.", "Store settings", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Try
            Await SupabaseService.SaveStoreSettingsAsync(New LocalData.StoreSettings() With {
                .name = storeName,
                .email = email,
                .description = TxtDescription.Text.Trim()
            })
            CType(Application.Current.MainWindow, MainWindow).RefreshStoreIdentity()
            TxtFeedback.Text = "Store profile saved."
        Catch ex As Exception
            TxtFeedback.Text = ex.Message
        End Try
    End Sub
End Class