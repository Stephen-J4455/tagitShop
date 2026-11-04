Imports System
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Class MainWindow
    Private activeNavigation As Button

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        LoginPanel.Visibility = Visibility.Visible
        DashboardPanel.Visibility = Visibility.Collapsed
    End Sub

    Private Sub BtnMinimize_Click(sender As Object, e As RoutedEventArgs)
        WindowState = WindowState.Minimized
    End Sub

    Private Sub BtnMaximize_Click(sender As Object, e As RoutedEventArgs)
        WindowState = If(WindowState = WindowState.Maximized, WindowState.Normal, WindowState.Maximized)
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Application.Current.Shutdown()
    End Sub

    Private Sub TitleBar_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ButtonState = MouseButtonState.Pressed AndAlso WindowState <> WindowState.Maximized Then DragMove()
    End Sub

    Private Sub Window_StateChanged(sender As Object, e As EventArgs)
        BtnMaximize.Content = If(WindowState = WindowState.Maximized, "❐", "□")
    End Sub

    Private Sub BtnStartDemo_Click(sender As Object, e As RoutedEventArgs)
        AppSession.StartDemo()
        EnterDashboard()
    End Sub

    Private Sub BtnShowLiveLogin_Click(sender As Object, e As RoutedEventArgs)
        ModePickerPanel.Visibility = Visibility.Collapsed
        LiveLoginPanel.Visibility = Visibility.Visible
        TxtLoginEmail.Focus()
    End Sub

    Private Sub BtnBackToModes_Click(sender As Object, e As RoutedEventArgs)
        LiveLoginPanel.Visibility = Visibility.Collapsed
        ModePickerPanel.Visibility = Visibility.Visible
        TxtLoginFeedback.Text = String.Empty
    End Sub

    Private Async Sub BtnLogin_Click(sender As Object, e As RoutedEventArgs)
        Dim email = TxtLoginEmail.Text.Trim()
        If String.IsNullOrWhiteSpace(email) OrElse String.IsNullOrWhiteSpace(PwdLoginPassword.Password) Then
            TxtLoginFeedback.Text = "Enter your email and password to continue."
            Return
        End If

        BtnLogin.IsEnabled = False
        BtnLogin.Content = "Connecting..."
        TxtLoginFeedback.Text = String.Empty
        Try
            Await SupabaseService.SignInAsync(email, PwdLoginPassword.Password)
            EnterDashboard()
        Catch ex As Exception
            AppSession.ClearLiveSession()
            TxtLoginFeedback.Text = ex.Message
        Finally
            BtnLogin.IsEnabled = True
            BtnLogin.Content = "Sign in securely"
        End Try
    End Sub

    Private Async Sub BtnLogOut_Click(sender As Object, e As RoutedEventArgs)
        If AppSession.Mode = StoreMode.Live Then Await SupabaseService.SignOutAsync()
        AppSession.ClearLiveSession()
        MainFrame.Content = Nothing
        DashboardPanel.Visibility = Visibility.Collapsed
        LoginPanel.Visibility = Visibility.Visible
        LiveLoginPanel.Visibility = Visibility.Collapsed
        ModePickerPanel.Visibility = Visibility.Visible
        TxtLoginEmail.Text = String.Empty
        PwdLoginPassword.Password = String.Empty
    End Sub

    Private Sub EnterDashboard()
        LoginPanel.Visibility = Visibility.Collapsed
        DashboardPanel.Visibility = Visibility.Visible
        TxtStoreName.Text = AppSession.StoreName
        TxtSidebarStoreName.Text = AppSession.StoreName
        TxtModeBadge.Text = If(AppSession.Mode = StoreMode.Live, "LIVE / CONNECTED", "DEMO / LOCAL")
        NavigateToPage(New Overview(), BtnOverview)
    End Sub

    Private Sub BtnOverview_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Overview(), BtnOverview)
    End Sub

    Private Sub BtnStocktake_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Stocktake(), BtnStocktake)
    End Sub

    Private Sub BtnCatalog_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Catalog(), BtnCatalog)
    End Sub

    Private Sub BtnOrders_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Orders(), BtnOrders)
    End Sub

    Private Sub BtnDebt_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Debt(), BtnDebt)
    End Sub

    Private Sub BtnReviews_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Reviews(), BtnReviews)
    End Sub

    Private Sub BtnInsights_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Insights(), BtnInsights)
    End Sub

    Private Sub BtnStoreProfile_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New StoreProfile(), BtnStoreProfile)
    End Sub

    Public Sub NavigateToPage(page As Page, Optional navButton As Button = Nothing)
        MainFrame.Navigate(page)
        TxtWindowTitle.Text = GetPageTitle(page)
        If activeNavigation IsNot Nothing Then activeNavigation.Style = DirectCast(FindResource("SidebarButton"), Style)
        activeNavigation = navButton
        If activeNavigation IsNot Nothing Then activeNavigation.Style = DirectCast(FindResource("SidebarButtonActive"), Style)
    End Sub

    Public Sub RefreshStoreIdentity()
        TxtStoreName.Text = AppSession.StoreName
        TxtSidebarStoreName.Text = AppSession.StoreName
    End Sub

    Private Function GetPageTitle(page As Page) As String
        Select Case page.GetType().Name
            Case "Catalog"
                Return "Products & pricing"
            Case "Stocktake"
                Return "Stocktake"
            Case "Orders"
                Return "Sales"
            Case "Debt"
                Return "Customer debts"
            Case "Reviews"
                Return "Product reviews"
            Case "StoreProfile"
                Return "Store settings"
            Case Else
                Return page.GetType().Name
        End Select
    End Function
End Class