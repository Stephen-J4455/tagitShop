Imports System
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class StoreProfile
    Inherits Page

    Private storeSettings As LocalData.StoreSettings

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Try
            storeSettings = Await SupabaseService.LoadStoreSettingsAsync()
            TxtStoreName.Text = If(storeSettings.name, String.Empty)
            TxtStoreEmail.Text = If(storeSettings.email, String.Empty)
            TxtDescription.Text = If(storeSettings.description, String.Empty)
            UpdateStorePreview()
            TxtFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Changes update your live store profile.", "Changes stay in this computer's demo store.")
            BtnSave.IsEnabled = True
        Catch ex As Exception
            TxtFeedback.Text = "Store details could not be loaded. " & ex.Message
        End Try
    End Sub

    Private Async Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        If storeSettings Is Nothing Then Return

        Dim storeName = TxtStoreName.Text.Trim()
        Dim email = TxtStoreEmail.Text.Trim()
        If String.IsNullOrWhiteSpace(storeName) OrElse String.IsNullOrWhiteSpace(email) Then
            MessageBox.Show("Enter a store name and contact email.", "Store profile", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If
        If Not email.Contains("@") Then
            MessageBox.Show("Enter a valid contact email.", "Store profile", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        storeSettings.name = storeName
        storeSettings.email = email
        storeSettings.description = TxtDescription.Text.Trim()
        storeSettings.store_description = storeSettings.description
        BtnSave.IsEnabled = False
        Try
            Await SupabaseService.SaveStoreSettingsAsync(storeSettings)
            CType(Application.Current.MainWindow, MainWindow).RefreshStoreIdentity()
            TxtFeedback.Text = "Store profile saved."
        Catch ex As Exception
            TxtFeedback.Text = "Store profile could not be saved. " & ex.Message
        Finally
            BtnSave.IsEnabled = True
        End Try
    End Sub

    Private Sub StoreIdentity_TextChanged(sender As Object, e As TextChangedEventArgs)
        UpdateStorePreview()
    End Sub

    Private Sub UpdateStorePreview()
        If PreviewStoreName Is Nothing Then Return

        Dim storeName = TxtStoreName.Text.Trim()
        Dim email = TxtStoreEmail.Text.Trim()
        Dim description = TxtDescription.Text.Trim()
        PreviewStoreName.Text = If(String.IsNullOrWhiteSpace(storeName), "Your store", storeName)
        PreviewHeroName.Text = PreviewStoreName.Text
        PreviewStoreInitial.Text = If(String.IsNullOrWhiteSpace(storeName), "T", storeName.Substring(0, 1).ToUpperInvariant())
        PreviewDescription.Text = If(String.IsNullOrWhiteSpace(description), "Tell customers what makes your store special.", description)
        PreviewEmail.Text = If(String.IsNullOrWhiteSpace(email), "Contact email", email)
    End Sub
End Class