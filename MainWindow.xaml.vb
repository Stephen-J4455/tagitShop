Imports System
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Class MainWindow
    Private Const DemoEmail As String = "demo@tagit.local"
    Private Const DemoPassword As String = "demo"
    Private activeNavigation As Button
    Private sidebarCollapsed As Boolean = False
    Private passwordVisible As Boolean
    Private syncingPassword As Boolean

    Private Async Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        ThemeManager.Initialize()
        UpdateThemeToggle()
        If AppSession.TryRestoreSession() Then
            EnterDashboard()
        Else
            LoginPanel.Visibility = Visibility.Visible
            DashboardPanel.Visibility = Visibility.Collapsed
        End If
    End Sub

    Private Sub BtnThemeToggle_Click(sender As Object, e As RoutedEventArgs)
        Try
            ThemeManager.Toggle()
            UpdateThemeToggle()
        Catch ex As Exception
            MessageBox.Show("The theme could not be saved. " & ex.Message, "Appearance", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub UpdateThemeToggle()
        BtnThemeToggle.Content = If(ThemeManager.IsDark, "Light mode", "Dark mode")
        BtnThemeToggle.ToolTip = If(ThemeManager.IsDark, "Switch to light mode", "Switch to dark mode")
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

    Private Async Sub BtnLogin_Click(sender As Object, e As RoutedEventArgs)
        Dim email = TxtLoginEmail.Text.Trim()
        Dim password = PwdLoginPassword.Password
        If String.IsNullOrWhiteSpace(email) OrElse String.IsNullOrWhiteSpace(password) Then
            TxtLoginFeedback.Text = "Enter your email and password to continue."
            Return
        End If

        BtnLogin.IsEnabled = False
        BtnLogin.Content = "Connecting..."
        TxtLoginFeedback.Text = String.Empty
        Try
            If String.Equals(email, DemoEmail, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(password, DemoPassword, StringComparison.Ordinal) Then
                AppSession.StartDemo()
                EnterDashboard()
                Return
            End If

            Await SupabaseService.SignInAsync(email, password)
            EnterDashboard()
        Catch ex As Exception
            AppSession.ClearLiveSession()
            TxtLoginFeedback.Text = ex.Message
        Finally
            BtnLogin.IsEnabled = True
            BtnLogin.Content = "Sign in securely"
        End Try
    End Sub

    Private Sub PwdLoginPassword_PasswordChanged(sender As Object, e As RoutedEventArgs)
        UpdatePasswordPlaceholder(PwdLoginPassword)
        If syncingPassword Then Return

        syncingPassword = True
        TxtLoginPasswordReveal.Text = PwdLoginPassword.Password
        syncingPassword = False
    End Sub

    Private Sub TxtLoginPasswordReveal_TextChanged(sender As Object, e As TextChangedEventArgs)
        If syncingPassword Then Return

        syncingPassword = True
        PwdLoginPassword.Password = TxtLoginPasswordReveal.Text
        syncingPassword = False
    End Sub

    Private Sub BtnTogglePassword_Click(sender As Object, e As RoutedEventArgs)
        passwordVisible = Not passwordVisible
        If passwordVisible Then
            TxtLoginPasswordReveal.Text = PwdLoginPassword.Password
            PwdLoginPassword.Visibility = Visibility.Collapsed
            TxtLoginPasswordReveal.Visibility = Visibility.Visible
            TxtLoginPasswordReveal.Focus()
            TxtLoginPasswordReveal.CaretIndex = TxtLoginPasswordReveal.Text.Length
            PasswordEyeOpen.Visibility = Visibility.Collapsed
            PasswordEyeClosed.Visibility = Visibility.Visible
            BtnTogglePassword.ToolTip = "Hide password"
        Else
            PwdLoginPassword.Password = TxtLoginPasswordReveal.Text
            TxtLoginPasswordReveal.Visibility = Visibility.Collapsed
            PwdLoginPassword.Visibility = Visibility.Visible
            PwdLoginPassword.Focus()
            PasswordEyeOpen.Visibility = Visibility.Visible
            PasswordEyeClosed.Visibility = Visibility.Collapsed
            BtnTogglePassword.ToolTip = "Show password"
        End If
    End Sub

    Private Sub UpdatePasswordPlaceholder(passwordBox As PasswordBox)
        If passwordBox.Template Is Nothing Then Return
        Dim placeholder = TryCast(passwordBox.Template.FindName("InputPlaceholder", passwordBox), TextBlock)
        If placeholder IsNot Nothing Then
            placeholder.Visibility = If(String.IsNullOrEmpty(passwordBox.Password), Visibility.Visible, Visibility.Collapsed)
        End If
    End Sub

    Private Sub BtnCollapseSidebar_Click(sender As Object, e As RoutedEventArgs)
        sidebarCollapsed = Not sidebarCollapsed
        UpdateSidebarState()
    End Sub

    Private Sub UpdateSidebarState()
        Dim width As Double = CDbl(FindResource(If(sidebarCollapsed, "SidebarWidthCollapsed", "SidebarWidthExpanded")))
        SidebarPanel.Width = width
        SidebarColumn.Width = New GridLength(width)

        Dim showText As Boolean = Not sidebarCollapsed
        For Each btn As Button In {BtnOverview, BtnCatalog, BtnOrders, BtnDebt, BtnReviews, BtnInsights,
                                   BtnFlash, BtnCoupons, BtnReels, BtnStoreProfile, BtnStorefront, BtnLogOut}
            btn.Content = If(showText, GetButtonText(btn), String.Empty)
            btn.HorizontalContentAlignment = If(showText, HorizontalAlignment.Left, HorizontalAlignment.Center)
        Next

        SidebarIdentity.Visibility = If(showText, Visibility.Visible, Visibility.Collapsed)
        SidebarLogo.Width = If(showText, 32, 26)
        SidebarLogo.Height = If(showText, 32, 26)
        SidebarHeader.Margin = If(showText, New Thickness(14, 18, 14, 16), New Thickness(10, 18, 10, 16))
        SidebarSearchBorder.Visibility = If(showText, Visibility.Visible, Visibility.Collapsed)
        SidebarFooterDetails.Visibility = If(showText, Visibility.Visible, Visibility.Collapsed)
        Divider3.Visibility = If(showText, Visibility.Visible, Visibility.Collapsed)
        BtnCollapseSidebar.Content = If(showText, "‹", "›")
        BtnCollapseSidebar.ToolTip = If(showText, "Collapse navigation", "Expand navigation")
        ApplySidebarFilter()
    End Sub

    Private Function GetButtonText(btn As Button) As String
        Select Case btn.Name
            Case "BtnOverview" : Return "Overview"
            Case "BtnCatalog" : Return "Products & pricing"
            Case "BtnOrders" : Return "Sales"
            Case "BtnDebt" : Return "Customer debts"
            Case "BtnReviews" : Return "Product reviews"
            Case "BtnInsights" : Return "Insights"
            Case "BtnFlash" : Return "Flash Sales"
            Case "BtnCoupons" : Return "Coupons"
            Case "BtnReels" : Return "Reels / Status"
            Case "BtnStoreProfile" : Return "Store settings"
            Case "BtnStorefront" : Return "Storefront Editor"
            Case "BtnLogOut" : Return "Exit store"
            Case Else : Return String.Empty
        End Select
    End Function

    Private Sub TxtSidebarSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        ApplySidebarFilter()
    End Sub

    Private Sub ApplySidebarFilter()
        Dim query As String = TxtSidebarSearch.Text.Trim()
        TxtSidebarSearchPlaceholder.Visibility = If(String.IsNullOrEmpty(query), Visibility.Visible, Visibility.Collapsed)

        FilterNavigationGroup(NavGroupWorkspace, TxtSectionWorkspace, query,
                              {BtnOverview, BtnCatalog, BtnOrders, BtnDebt, BtnReviews, BtnInsights})
        FilterNavigationGroup(NavGroupPromotions, TxtSectionPromotions, query,
                              {BtnFlash, BtnCoupons, BtnReels})
        FilterNavigationGroup(NavGroupStore, TxtSectionStore, query,
                              {BtnStoreProfile, BtnStorefront})

        Divider1.Visibility = If(Not sidebarCollapsed AndAlso NavGroupWorkspace.Visibility = Visibility.Visible AndAlso NavGroupPromotions.Visibility = Visibility.Visible,
                                 Visibility.Visible, Visibility.Collapsed)
        Divider2.Visibility = If(Not sidebarCollapsed AndAlso NavGroupPromotions.Visibility = Visibility.Visible AndAlso NavGroupStore.Visibility = Visibility.Visible,
                                 Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub FilterNavigationGroup(group As StackPanel, heading As TextBlock, query As String, buttons As Button())
        Dim hasVisibleItems As Boolean = False
        For Each btn As Button In buttons
            Dim matches As Boolean = String.IsNullOrEmpty(query) OrElse
                GetButtonText(btn).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            btn.Visibility = If(matches, Visibility.Visible, Visibility.Collapsed)
            hasVisibleItems = hasVisibleItems OrElse matches
        Next

        group.Visibility = If(hasVisibleItems, Visibility.Visible, Visibility.Collapsed)
        heading.Visibility = If(hasVisibleItems AndAlso Not sidebarCollapsed, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Async Sub BtnLogOut_Click(sender As Object, e As RoutedEventArgs)
        If AppSession.Mode = StoreMode.Live Then Await SupabaseService.SignOutAsync()
        AppSession.ClearLiveSession()
        MainFrame.Content = Nothing
        DashboardPanel.Visibility = Visibility.Collapsed
        LoginPanel.Visibility = Visibility.Visible
        LoginCard.Visibility = Visibility.Visible
        TxtLoginEmail.Text = String.Empty
        PwdLoginPassword.Password = String.Empty
        TxtLoginPasswordReveal.Text = String.Empty
        passwordVisible = False
        PwdLoginPassword.Visibility = Visibility.Visible
        TxtLoginPasswordReveal.Visibility = Visibility.Collapsed
        PasswordEyeOpen.Visibility = Visibility.Visible
        PasswordEyeClosed.Visibility = Visibility.Collapsed
        BtnTogglePassword.ToolTip = "Show password"
    End Sub

    Private Sub EnterDashboard()
        LoginPanel.Visibility = Visibility.Collapsed
        DashboardPanel.Visibility = Visibility.Visible
        TxtSidebarStoreName.Text = AppSession.StoreName
        TxtModeBadge.Text = If(AppSession.Mode = StoreMode.Live, "LIVE / CONNECTED", "DEMO / LOCAL")
        DataCache.EnsureLoadedAsync()
        NavigateToPage(New Overview(), BtnOverview)
    End Sub

    Private Sub BtnOverview_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Overview(), BtnOverview)
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

    Private Sub BtnFlash_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Flash(), BtnFlash)
    End Sub

    Private Sub BtnCoupons_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Coupons(), BtnCoupons)
    End Sub

    Private Sub BtnReels_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New Reels(), BtnReels)
    End Sub

    Private Sub BtnStorefront_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New StorefrontEditor(), BtnStorefront)
    End Sub

    Private Sub BtnStoreProfile_Click(sender As Object, e As RoutedEventArgs)
        NavigateToPage(New StoreProfile(), BtnStoreProfile)
    End Sub

    Public Sub NavigateToPage(page As Page, Optional navButton As Button = Nothing)
        MainFrame.Navigate(page)
        If activeNavigation IsNot Nothing Then activeNavigation.Style = DirectCast(FindResource("SidebarButton"), Style)
        activeNavigation = navButton
        If activeNavigation IsNot Nothing Then activeNavigation.Style = DirectCast(FindResource("SidebarButtonActive"), Style)
    End Sub

    Public Sub RefreshStoreIdentity()
        TxtSidebarStoreName.Text = AppSession.StoreName
    End Sub
End Class