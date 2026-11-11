Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Partial Public Class StorefrontEditor
    Inherits Page

    Private selectedAccent As String = "#FF5A79"
    Private isInitializing As Boolean
    Private isUpdatingTemplate As Boolean
    Private isUpdatingSwitch As Boolean
    Private isStackedLayout As Boolean
    Private previewReady As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        isInitializing = True
        Try
            Dim settings As LocalData.StoreSettings
            If AppSession.Mode = StoreMode.Live Then
                settings = Await DataCache.RefreshStoreSettingsAsync()
            Else
                Await DataCache.EnsureLoadedAsync()
                settings = DataCache.StoreSettings
            End If
            previewReady = True
            TxtStoreName.Text = If(settings.name, String.Empty)
            TxtStoreEmail.Text = If(settings.email, String.Empty)
            TxtDescription.Text = If(settings.description, String.Empty)
            TxtStoreDescription.Text = If(settings.store_description, String.Empty)
            TxtThemeColor.Text = If(String.IsNullOrWhiteSpace(settings.theme_color), selectedAccent, settings.theme_color.Trim())
            selectedAccent = TxtThemeColor.Text
            UpdateAccentSwatchSelection(selectedAccent)

            CmbTheme.SelectedValue = If(settings.storefront_theme, "chicwave")
            If CmbTheme.SelectedItem Is Nothing Then CmbTheme.SelectedValue = "chicwave"
            CmbLayout.SelectedValue = If(settings.storefront_layout, "classic")
            If CmbLayout.SelectedItem Is Nothing Then CmbLayout.SelectedValue = "classic"

            TxtAvatar.Text = If(settings.avatar, String.Empty)
            TxtCoverImage.Text = If(settings.cover_image, String.Empty)
            TxtCoverImage2.Text = If(settings.cover_image_2, String.Empty)
            TxtSocialWebsite.Text = If(settings.social_website, String.Empty)
            TxtSocialFacebook.Text = If(settings.social_facebook, String.Empty)
            TxtSocialInstagram.Text = If(settings.social_instagram, String.Empty)
            TxtSocialTwitter.Text = If(settings.social_twitter, String.Empty)
            TxtSocialWhatsapp.Text = If(settings.social_whatsapp, String.Empty)
            ChkStorefrontEnabled.IsChecked = settings.storefront_enabled.GetValueOrDefault(False)

            UpdateTemplateCardSelection(CmbTheme.SelectedValue.ToString())
            UpdateStorefrontStatus()
            TxtFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Changes update your ExpressMart storefront.", "Changes stay in this computer's demo store.")

            RefreshPreview()
            WirePreviewUpdates()
            UpdatePreviewViewport()
        Catch ex As Exception
            TxtFeedback.Text = ex.Message
        Finally
            isInitializing = False
        End Try
    End Sub

    Private Sub WirePreviewUpdates()
        AddHandler TxtThemeColor.TextChanged, AddressOf PreviewTextChanged
        AddHandler CmbTheme.SelectionChanged, AddressOf PreviewThemeChanged
    End Sub

    Private Sub BtnBack_Click(sender As Object, e As RoutedEventArgs)
        If NavigationService IsNot Nothing AndAlso NavigationService.CanGoBack Then NavigationService.GoBack()
    End Sub

    Private Sub Page_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        UpdatePreviewViewport()
    End Sub

    Private Sub EditorColumns_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If EditorColumns.ActualWidth <= 0 Then Return

        Dim shouldStack = EditorColumns.ActualWidth < 860
        If shouldStack = isStackedLayout Then Return
        isStackedLayout = shouldStack

        If shouldStack Then
            PreviewColumn.Width = New GridLength(1, GridUnitType.Star)
            SettingsColumn.Width = New GridLength(0)
            Grid.SetRow(SettingsColumnPanel, 1)
            Grid.SetColumn(SettingsColumnPanel, 0)
            PreviewColumnPanel.Margin = New Thickness(0, 0, 0, 26)
        Else
            PreviewColumn.Width = New GridLength(1.08, GridUnitType.Star)
            SettingsColumn.Width = New GridLength(0.92, GridUnitType.Star)
            Grid.SetRow(SettingsColumnPanel, 0)
            Grid.SetColumn(SettingsColumnPanel, 1)
            PreviewColumnPanel.Margin = New Thickness(0, 0, 20, 0)
        End If

        UpdatePreviewViewport()
    End Sub

    Private Sub PreviewViewport_Checked(sender As Object, e As RoutedEventArgs)
        UpdatePreviewViewport()
    End Sub

    Private Sub UpdatePreviewViewport()
        If PreviewBrowser Is Nothing Then Return

        If ViewportMobile IsNot Nothing AndAlso ViewportMobile.IsChecked.GetValueOrDefault() Then
            PreviewBrowser.Width = Math.Min(280, Math.Max(240, PreviewColumnPanel.ActualWidth - 8))
            PreviewBrowser.HorizontalAlignment = HorizontalAlignment.Center
            PreviewNavPanel.Visibility = Visibility.Collapsed
        ElseIf ViewportTablet IsNot Nothing AndAlso ViewportTablet.IsChecked.GetValueOrDefault() Then
            PreviewBrowser.Width = Math.Min(420, Math.Max(280, PreviewColumnPanel.ActualWidth - 8))
            PreviewBrowser.HorizontalAlignment = HorizontalAlignment.Center
            PreviewNavPanel.Visibility = Visibility.Visible
        Else
            PreviewBrowser.Width = Double.NaN
            PreviewBrowser.HorizontalAlignment = HorizontalAlignment.Stretch
            PreviewNavPanel.Visibility = Visibility.Visible
        End If

        If previewReady AndAlso Not isInitializing AndAlso PreviewHeroHost IsNot Nothing Then RefreshPreview()
    End Sub

    Private Sub TemplateCard_Checked(sender As Object, e As RoutedEventArgs)
        If isInitializing OrElse isUpdatingTemplate Then Return
        Dim card = TryCast(sender, RadioButton)
        If card Is Nothing Then Return

        isUpdatingTemplate = True
        CmbTheme.SelectedValue = TryCast(card.Tag, String)
        isUpdatingTemplate = False
        RefreshPreview()
    End Sub

    Private Sub UpdateTemplateCardSelection(theme As String)
        isUpdatingTemplate = True
        For Each card In {TemplateChicWave, TemplateFashionia, TemplateMinimal, TemplateBotanical, TemplateLuxe, TemplateClarity, TemplateStudioAir}
            card.IsChecked = String.Equals(TryCast(card.Tag, String), theme, StringComparison.OrdinalIgnoreCase)
        Next
        isUpdatingTemplate = False
    End Sub

    Private Sub PreviewThemeChanged(sender As Object, e As SelectionChangedEventArgs)
        If isInitializing OrElse Not previewReady Then Return
        If Not isUpdatingTemplate Then
            Dim selectedTheme = TryCast(CmbTheme.SelectedValue, String)
            If Not String.IsNullOrWhiteSpace(selectedTheme) Then UpdateTemplateCardSelection(selectedTheme)
        End If
        RefreshPreview()
    End Sub

    Private Sub AccentSwatch_Click(sender As Object, e As RoutedEventArgs)
        Dim swatch = TryCast(sender, Button)
        If swatch Is Nothing Then Return
        Dim colorHex = TryCast(swatch.Tag, String)
        If Not IsValidHexColor(colorHex) Then Return

        selectedAccent = colorHex.ToUpperInvariant()
        TxtThemeColor.Text = selectedAccent
        UpdateAccentSwatchSelection(selectedAccent)
        RefreshPreview()
    End Sub

    Private Sub UpdateAccentSwatchSelection(hex As String)
        For Each swatch In {AccentSwatch1, AccentSwatch2, AccentSwatch3, AccentSwatch4, AccentSwatch5, AccentSwatch6, AccentSwatch7, AccentSwatch8}
            Dim swatchHex = TryCast(swatch.Tag, String)
            Dim isSelected = String.Equals(swatchHex, hex, StringComparison.OrdinalIgnoreCase)
            If isSelected Then
                swatch.BorderBrush = Brushes.White
            Else
                swatch.SetResourceReference(Border.BorderBrushProperty, "TagitBorder")
            End If
            swatch.BorderThickness = If(isSelected, New Thickness(2), New Thickness(1))
        Next
    End Sub

    Private Sub PreviewTextChanged(sender As Object, e As TextChangedEventArgs)
        If IsValidHexColor(TxtThemeColor.Text.Trim()) Then
            selectedAccent = TxtThemeColor.Text.Trim().ToUpperInvariant()
            UpdateAccentSwatchSelection(selectedAccent)
        Else
            UpdateAccentSwatchSelection(String.Empty)
        End If
        RefreshPreview()
    End Sub

    Private Sub PreviewImageChanged(sender As Object, e As TextChangedEventArgs)
        RefreshPreview()
    End Sub

    Private Sub RefreshPreview()
        If PreviewHeroHost Is Nothing Then Return

        Dim storeName = If(String.IsNullOrWhiteSpace(TxtStoreName.Text), "Your store", TxtStoreName.Text.Trim())
        Dim description = If(String.IsNullOrWhiteSpace(TxtDescription.Text), "A neighborhood store serving everyday essentials.", TxtDescription.Text.Trim())
        Dim theme = SelectedValue(CmbTheme, "chicwave")
        Dim layout = SelectedValue(CmbLayout, "classic")
        Dim accentColor As Color
        If Not TryGetAccentColor(TxtThemeColor.Text.Trim(), accentColor) AndAlso Not TryGetAccentColor(selectedAccent, accentColor) Then
            accentColor = Color.FromRgb(&HFF, &H5A, &H79)
        End If

        Dim foreground = GetThemeForeground(theme)
        Dim accentBrush As New SolidColorBrush(accentColor)
        Dim fontFamily = GetThemeFont(theme)
        Dim initials = If(storeName.Length > 0, storeName.Substring(0, 1).ToUpperInvariant(), "T")
        PreviewStorePage.Background = GetThemeBackground(theme)
        PreviewStorePage.Padding = If(layout = "quiet", New Thickness(16, 20, 16, 18), New Thickness(12, 10, 12, 14))
        PreviewStoreName.Text = If(theme = "studio-air", storeName.ToUpperInvariant(), storeName)
        PreviewStoreName.Foreground = foreground
        PreviewStoreName.FontFamily = fontFamily
        PreviewStoreName.FontSize = If(theme = "clarity", 14D, 12D)
        PreviewAvatarFallback.Background = accentBrush
        PreviewAvatarFallback.CornerRadius = If(theme = "studio-air" OrElse theme = "fashionia", New CornerRadius(0), New CornerRadius(13))
        PreviewAvatarInitial.Text = initials
        PreviewSearchButton.Background = accentBrush
        PreviewSearchShell.CornerRadius = If(theme = "studio-air", New CornerRadius(0), New CornerRadius(14))
        PreviewSearchButton.CornerRadius = If(theme = "studio-air", New CornerRadius(0), New CornerRadius(10))
        PreviewSearchShell.Background = GetThemeSurface(theme)
        PreviewSearchShell.BorderBrush = New SolidColorBrush(Color.FromArgb(&H50, foreground.Color.R, foreground.Color.G, foreground.Color.B))
        PreviewHeaderDivider.Background = New SolidColorBrush(Color.FromArgb(&H50, foreground.Color.R, foreground.Color.G, foreground.Color.B))
        PreviewHeaderDivider.Margin = If(layout = "asymmetric", New Thickness(18, 0, 0, 8), New Thickness(0, 0, 0, 8))
        PreviewHeaderDivider.Height = If(layout = "editorial" OrElse layout = "product-first", 2D, If(theme = "clarity" OrElse layout = "gallery" OrElse layout = "quiet", 0D, 1D))
        PreviewHeaderDivider.Visibility = If(PreviewHeaderDivider.Height = 0, Visibility.Collapsed, Visibility.Visible)
        PreviewStoreName.TextTrimming = TextTrimming.CharacterEllipsis
        PreviewNavPanel.Visibility = If(IsMobilePreview(), Visibility.Collapsed, Visibility.Visible)

        If theme = "fashionia" OrElse theme = "luxe" OrElse theme = "clarity" Then
            PreviewStoreName.FontWeight = FontWeights.Medium
        Else
            PreviewStoreName.FontWeight = FontWeights.SemiBold
        End If

        PreviewHeroHost.Content = CreateHero(theme, layout, storeName, description, accentColor)
        PreviewBelowHeroHost.Content = CreateStoreSections(theme, layout, storeName, accentColor)
        PreviewSocialLinksHost.Content = CreateSocialLinks(theme, foreground, accentColor)
        SetPreviewImage(PreviewAvatarImage, TxtAvatar.Text.Trim(), PreviewAvatarFallback)
    End Sub

    Private Function CreateSocialLinks(theme As String, foreground As Brush, accent As Color) As UIElement
        Dim links As New List(Of Tuple(Of String, String)) From {
            Tuple.Create("Facebook", NormalizeSocialLink(TxtSocialFacebook.Text, False)),
            Tuple.Create("Instagram", NormalizeSocialLink(TxtSocialInstagram.Text, False)),
            Tuple.Create("X", NormalizeSocialLink(TxtSocialTwitter.Text, False)),
            Tuple.Create("WhatsApp", NormalizeSocialLink(TxtSocialWhatsapp.Text, True)),
            Tuple.Create("Website", NormalizeSocialLink(TxtSocialWebsite.Text, False))
        }
        Dim visibleLinks = links.Where(Function(item) Not String.IsNullOrWhiteSpace(item.Item2)).ToList()
        If visibleLinks.Count = 0 Then Return New Border() With {.Visibility = Visibility.Collapsed}

        Dim panel As New WrapPanel() With {.HorizontalAlignment = HorizontalAlignment.Left}
        For Each link In visibleLinks
            Dim label = CreateText(link.Item1, New SolidColorBrush(accent), 7, FontWeights.SemiBold, New Thickness(0, 0, 16, 5))
            label.ToolTip = link.Item2
            panel.Children.Add(label)
        Next
        Dim foregroundColor = If(TryCast(foreground, SolidColorBrush), New SolidColorBrush(Colors.Black)).Color
        Return New Border() With {
            .BorderBrush = New SolidColorBrush(Color.FromArgb(&H35, foregroundColor.R, foregroundColor.G, foregroundColor.B)),
            .BorderThickness = New Thickness(0, 1, 0, 0),
            .Padding = New Thickness(0, 10, 0, 0),
            .Child = panel
        }
    End Function

    Private Shared Function NormalizeSocialLink(value As String, isWhatsapp As Boolean) As String
        Dim trimmed = If(value, String.Empty).Trim()
        If String.IsNullOrWhiteSpace(trimmed) Then Return String.Empty
        If isWhatsapp AndAlso Regex.IsMatch(trimmed, "^[0-9+()\-\s]{6,}$") Then
            Dim digits = New String(trimmed.Where(Function(character) Char.IsDigit(character)).ToArray())
            If Not String.IsNullOrWhiteSpace(digits) Then Return "https://wa.me/" & digits
        End If
        If Regex.IsMatch(trimmed, "^[a-z][a-z0-9+\-.]*://", RegexOptions.IgnoreCase) Then Return trimmed
        Return "https://" & trimmed
    End Function

    Private Function GetThemeBackground(theme As String) As SolidColorBrush
        Select Case theme.ToLowerInvariant()
            Case "chicwave" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#FFF8F4"))
            Case "fashionia" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#F4EEE7"))
            Case "minimal" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#F8FAFC"))
            Case "botanical" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#F3F7EF"))
            Case "luxe" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#F7F2E8"))
            Case "clarity" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#F5F4F0"))
            Case "studio-air" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#E8E8E8"))
            Case Else : Return New SolidColorBrush(ColorConverter.ConvertFromString("#FFF4EC"))
        End Select
    End Function

    Private Function GetThemeForeground(theme As String) As SolidColorBrush
        Select Case theme.ToLowerInvariant()
            Case "chicwave" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#292524"))
            Case "fashionia" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#312820"))
            Case "minimal" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#0F172A"))
            Case "botanical" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#243327"))
            Case "luxe" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#342B20"))
            Case "clarity", "studio-air" : Return New SolidColorBrush(ColorConverter.ConvertFromString("#202020"))
            Case Else : Return New SolidColorBrush(ColorConverter.ConvertFromString("#292524"))
        End Select
    End Function

    Private Function GetThemeSurface(theme As String) As Brush
        If theme = "studio-air" Then Return Brushes.Transparent
        Return New SolidColorBrush(Color.FromArgb(&HBF, &HFF, &HFF, &HFF))
    End Function

    Private Function GetThemeFont(theme As String) As FontFamily
        If theme = "fashionia" OrElse theme = "luxe" OrElse theme = "clarity" Then Return New FontFamily("Georgia")
        Return New FontFamily("Segoe UI")
    End Function

    Private Function SelectedValue(combo As ComboBox, fallback As String) As String
        Dim selected = TryCast(combo.SelectedItem, ComboBoxItem)
        If selected Is Nothing OrElse selected.Tag Is Nothing Then Return fallback
        Return selected.Tag.ToString().ToLowerInvariant()
    End Function

    Private Function IsMobilePreview() As Boolean
        Return ViewportMobile IsNot Nothing AndAlso ViewportMobile.IsChecked.GetValueOrDefault()
    End Function

    Private Function CreateHero(theme As String, layout As String, storeName As String, description As String, accent As Color) As UIElement
        If layout = "gallery" Then Return CreateGalleryHero(theme, storeName, description, accent)
        If layout = "immersive" Then Return CreateImmersiveHero(theme, storeName, description, accent)
        If theme = "clarity" Then Return CreateClarityHero(theme, storeName, description, accent)
        If theme = "studio-air" Then Return CreateStudioAirHero(theme, storeName, description, accent)
        If layout = "product-first" Then Return CreateProductFirstHero(theme, storeName, description, accent)
        If layout = "quiet" Then Return CreateQuietHero(theme, storeName, description, accent)
        If layout = "asymmetric" Then Return CreateAsymmetricHero(theme, storeName, description, accent)
        If layout = "split" Then Return CreateSplitHero(theme, storeName, description, accent)
        If layout = "editorial" Then Return CreateEditorialHero(theme, storeName, description, accent)
        If layout = "catalog" Then Return CreateCatalogHero(theme, storeName, description, accent)
        Return CreateClassicHero(theme, layout, storeName, description, accent)
    End Function

    Private Function CreateGalleryHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim gallery As New Grid() With {.MinHeight = If(IsMobilePreview(), 260, 210), .Background = New SolidColorBrush(Color.FromArgb(&H0A, 0, 0, 0))}
        If IsMobilePreview() Then
            gallery.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(92)})
            gallery.RowDefinitions.Add(New RowDefinition() With {.Height = GridLength.Auto})
            gallery.Children.Add(CreateImageSurface(TxtCoverImage.Text.Trim(), Color.FromRgb(&HD9, &HD9, &HD9), 92))
            Dim mobileCopy = CreateHeroCopy(theme, "The collection", storeName, description, "Browse collection", accent,
                                            TextAlignment.Center, 21, False, 8)
            Grid.SetRow(mobileCopy, 1)
            gallery.Children.Add(mobileCopy)
            Return gallery
        End If

        gallery.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        gallery.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1.16, GridUnitType.Star)})
        gallery.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        gallery.Children.Add(CreateImageSurface(TxtCoverImage.Text.Trim(), Color.FromRgb(&HD9, &HD9, &HD9), 210))
        Dim copy = CreateHeroCopy(theme, "The collection", storeName, description, "Browse collection", accent,
                                  TextAlignment.Center, 20, False, 12)
        Grid.SetColumn(copy, 1)
        gallery.Children.Add(copy)
        Dim secondImage = CreateImageSurface(TxtCoverImage2.Text.Trim(), Color.FromRgb(&HC9, &HD5, &HDF), 210)
        Grid.SetColumn(secondImage, 2)
        gallery.Children.Add(secondImage)
        Return gallery
    End Function

    Private Function CreateImmersiveHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim hero As New Grid() With {.MinHeight = If(IsMobilePreview(), 190, 245), .ClipToBounds = True}
        hero.Children.Add(CreateImageSurface(TxtCoverImage.Text.Trim(), accent, hero.MinHeight))
        hero.Children.Add(New Border() With {
            .Background = New LinearGradientBrush(Color.FromArgb(&HE8, 0, 0, 0), Color.FromArgb(&H19, 0, 0, 0), New Point(0.5, 1), New Point(0.5, 0))
        })
        Dim copy = CreateHeroCopy(theme, "Welcome to " & storeName, storeName, description, "Shop the collection", accent,
                                  TextAlignment.Left, If(IsMobilePreview(), 24, 31), False, 15, Brushes.White, True)
        copy.VerticalAlignment = VerticalAlignment.Bottom
        copy.HorizontalAlignment = HorizontalAlignment.Left
        copy.Margin = New Thickness(16, 30, 16, 17)
        hero.Children.Add(copy)
        Return hero
    End Function

    Private Function CreateClarityHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim content As New StackPanel() With {.HorizontalAlignment = HorizontalAlignment.Center}
        content.Children.Add(CreateText("A considered collection from " & storeName, GetThemeForeground(theme), 7, FontWeights.SemiBold, New Thickness(0, 16, 0, 0), TextAlignment.Center))
        content.Children.Add(CreateText("Make room for what matters.", GetThemeForeground(theme), If(IsMobilePreview(), 25, 34), FontWeights.Medium, New Thickness(8, 0, 8, 0), TextAlignment.Center, GetThemeFont(theme)))
        content.Children.Add(CreateText(description, GetThemeForeground(theme), 9, FontWeights.Normal, New Thickness(12, 5, 12, 0), TextAlignment.Center, maxWidth:=320))
        content.Children.Add(CreateCta("Explore the collection", Colors.Black, Colors.White, False, New CornerRadius(16), New Thickness(0, 9, 0, 0)))

        Dim hero As New StackPanel()
        hero.Children.Add(content)
        Dim image = CreateImageSurface(TxtCoverImage.Text.Trim(), Color.FromRgb(&HDF, &HE5, &HEB), If(IsMobilePreview(), 92, 130))
        image.Margin = New Thickness(20, 12, 20, 0)
        hero.Children.Add(image)
        Return hero
    End Function

    Private Function CreateStudioAirHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim copy = CreateHeroCopy(theme, "Designed for everyday listening", storeName, description, "Shop now", accent,
                                  TextAlignment.Left, If(IsMobilePreview(), 23, 30), True, 12, GetThemeForeground(theme), False, True)
        Dim image = CreateImageSurface(TxtCoverImage.Text.Trim(), Color.FromRgb(&HE8, &HE8, &HE8), If(IsMobilePreview(), 100, 175), Stretch.Uniform)

        If IsMobilePreview() Then
            Dim stacked As New StackPanel()
            stacked.Children.Add(copy)
            stacked.Children.Add(image)
            Return stacked
        End If

        Dim hero As New Grid() With {.MinHeight = 195, .Background = GetThemeBackground(theme)}
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(0.8, GridUnitType.Star)})
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1.2, GridUnitType.Star)})
        Grid.SetColumn(copy, 0)
        Grid.SetColumn(image, 1)
        hero.Children.Add(copy)
        hero.Children.Add(image)
        Return hero
    End Function

    Private Function CreateProductFirstHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim product = FirstProduct()
        Dim title = If(product Is Nothing OrElse String.IsNullOrWhiteSpace(product.title), storeName, product.title)
        Dim detail = If(product Is Nothing OrElse String.IsNullOrWhiteSpace(product.description), description, product.description)
        Dim copy = CreateHeroCopy(theme, "Featured now", title, detail, "Shop featured product", accent,
                                  TextAlignment.Left, If(IsMobilePreview(), 24, 32), False, 12)
        Dim imagePath = If(product Is Nothing OrElse String.IsNullOrWhiteSpace(product.thumbnail), TxtCoverImage.Text.Trim(), product.thumbnail)
        Dim image = CreateImageSurface(imagePath, accent, If(IsMobilePreview(), 120, 205))
        image.Margin = New Thickness(8)

        If IsMobilePreview() Then
            Dim stacked As New StackPanel()
            stacked.Children.Add(image)
            stacked.Children.Add(copy)
            Return stacked
        End If

        Dim hero As New Grid() With {.MinHeight = 205, .Background = GetThemeSurface(theme)}
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(0.7, GridUnitType.Star)})
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1.3, GridUnitType.Star)})
        Grid.SetColumn(copy, 0)
        Grid.SetColumn(image, 1)
        hero.Children.Add(copy)
        hero.Children.Add(image)
        Return hero
    End Function

    Private Function CreateQuietHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim content As New Grid() With {.Margin = New Thickness(0, 12, 0, 12)}
        content.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        content.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(0.7, GridUnitType.Star)})
        Dim titleStack As New StackPanel()
        titleStack.Children.Add(CreateText("A quiet collection", GetThemeForeground(theme), 7, FontWeights.Medium))
        titleStack.Children.Add(CreateText(storeName, GetThemeForeground(theme), If(IsMobilePreview(), 25, 32), FontWeights.Normal, New Thickness(0, 5, 5, 0), TextAlignment.Left, GetThemeFont(theme)))
        Dim descriptionStack As New StackPanel() With {.VerticalAlignment = VerticalAlignment.Bottom, .Margin = New Thickness(8, 0, 0, 0)}
        descriptionStack.Children.Add(CreateText(description, GetThemeForeground(theme), 8, FontWeights.Normal))
        descriptionStack.Children.Add(CreateText("View products  ─────", GetThemeForeground(theme), 8, FontWeights.SemiBold, New Thickness(0, 8, 0, 0)))
        If IsMobilePreview() Then
            content.ColumnDefinitions.Clear()
            content.Children.Add(titleStack)
            descriptionStack.Margin = New Thickness(0, 10, 0, 0)
            Grid.SetRow(descriptionStack, 1)
            content.RowDefinitions.Add(New RowDefinition() With {.Height = GridLength.Auto})
            content.RowDefinitions.Add(New RowDefinition() With {.Height = GridLength.Auto})
            content.Children.Add(descriptionStack)
        Else
            Grid.SetColumn(titleStack, 0)
            Grid.SetColumn(descriptionStack, 1)
            content.Children.Add(titleStack)
            content.Children.Add(descriptionStack)
        End If

        Return New Border() With {
            .BorderBrush = New SolidColorBrush(Color.FromArgb(&H50, GetThemeForeground(theme).Color.R, GetThemeForeground(theme).Color.G, GetThemeForeground(theme).Color.B)),
            .BorderThickness = New Thickness(0, 1, 0, 1),
            .Padding = New Thickness(0, 10, 0, 10),
            .Child = content
        }
    End Function

    Private Function CreateAsymmetricHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim image = CreateImageSurface(TxtCoverImage.Text.Trim(), accent, If(IsMobilePreview(), 105, 205))
        Dim copy = CreateHeroCopy(theme, "New perspective", storeName, description, "Explore", accent,
                                  TextAlignment.Left, If(IsMobilePreview(), 25, 34), False, 12, linkOnly:=True)
        If IsMobilePreview() Then
            Dim stacked As New StackPanel()
            stacked.Children.Add(image)
            stacked.Children.Add(copy)
            Return stacked
        End If

        Dim hero As New Grid() With {.MinHeight = 220, .Background = New SolidColorBrush(Color.FromArgb(&H08, 0, 0, 0))}
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1.4, GridUnitType.Star)})
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(0.8, GridUnitType.Star)})
        image.RenderTransform = New TranslateTransform(0, 7)
        Grid.SetColumn(image, 0)
        Grid.SetColumn(copy, 1)
        hero.Children.Add(image)
        hero.Children.Add(copy)
        Return hero
    End Function

    Private Function CreateSplitHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim copy = CreateHeroCopy(theme, If(theme = "fashionia", "The new collection", If(theme = "minimal", "Thoughtfully selected", "Discover " & storeName)),
                                  storeName, description, "Browse collection", accent, TextAlignment.Left,
                                  If(IsMobilePreview(), 25, 32), theme = "fashionia", 12)
        Dim image = CreateImageSurface(TxtCoverImage.Text.Trim(), accent, If(IsMobilePreview(), 110, 205))
        If IsMobilePreview() Then
            Dim stacked As New StackPanel()
            stacked.Children.Add(copy)
            stacked.Children.Add(image)
            Return stacked
        End If

        Dim hero As New Grid() With {.MinHeight = 205}
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.SetColumn(copy, 0)
        Grid.SetColumn(image, 1)
        hero.Children.Add(copy)
        hero.Children.Add(image)
        Return hero
    End Function

    Private Function CreateEditorialHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim image = CreateImageSurface(TxtCoverImage.Text.Trim(), accent, If(IsMobilePreview(), 105, 210))
        Dim copy = CreateHeroCopy(theme, "The new collection", If(theme = "fashionia", storeName.ToUpperInvariant(), storeName),
                                  description, "Browse collection", accent, TextAlignment.Left,
                                  If(IsMobilePreview(), 23, 28), theme = "fashionia", 12)
        If IsMobilePreview() Then
            Dim stacked As New StackPanel()
            stacked.Children.Add(image)
            stacked.Children.Add(copy)
            Return stacked
        End If

        Dim hero As New Grid() With {.MinHeight = 215}
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1.25, GridUnitType.Star)})
        hero.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.SetColumn(image, 0)
        Grid.SetColumn(copy, 1)
        hero.Children.Add(image)
        hero.Children.Add(copy)
        Return hero
    End Function

    Private Function CreateCatalogHero(theme As String, storeName As String, description As String, accent As Color) As UIElement
        Dim hero As New Grid() With {.MinHeight = If(IsMobilePreview(), 130, 120), .ClipToBounds = True}
        Dim background = CreateImageSurface(TxtCoverImage.Text.Trim(), GetThemeBackground(theme).Color, hero.MinHeight)
        background.Opacity = If(String.IsNullOrWhiteSpace(TxtCoverImage.Text), 1D, 0.16D)
        hero.Children.Add(background)
        hero.Children.Add(CreateHeroCopy(theme, "Browse the store", storeName, description, "Browse all products", accent,
                                         TextAlignment.Center, If(IsMobilePreview(), 20, 24), False, 4))
        Return hero
    End Function

    Private Function CreateClassicHero(theme As String, layout As String, storeName As String, description As String, accent As Color) As UIElement
        Dim hero As New Grid() With {.MinHeight = If(IsMobilePreview(), 170, 205), .ClipToBounds = True}
        hero.Children.Add(CreateImageSurface(TxtCoverImage.Text.Trim(), accent, hero.MinHeight))
        hero.Children.Add(New Border() With {
            .Background = New LinearGradientBrush(Color.FromArgb(&HCC, 0, 0, 0), Color.FromArgb(&H1A, 0, 0, 0), New Point(0.5, 1), New Point(0.5, 0))
        })
        Dim callToAction = If(layout = "spotlight", "Discover the featured piece", "Shop the collection")
        Dim copy = CreateHeroCopy(theme, "Independent store", storeName, description, callToAction, accent,
                                  TextAlignment.Left, If(IsMobilePreview(), 25, 31), False, 14, Brushes.White, True)
        copy.VerticalAlignment = VerticalAlignment.Bottom
        copy.HorizontalAlignment = HorizontalAlignment.Left
        hero.Children.Add(copy)
        Return hero
    End Function

    Private Function CreateHeroCopy(theme As String, eyebrow As String, title As String, description As String, buttonText As String,
                                    accent As Color, alignment As TextAlignment, titleSize As Double, outlineButton As Boolean,
                                    padding As Double, Optional foreground As Brush = Nothing, Optional whiteButton As Boolean = False,
                                    Optional squareButton As Boolean = False, Optional linkOnly As Boolean = False) As StackPanel
        Dim mainForeground = If(foreground, GetThemeForeground(theme))
        Dim panel As New StackPanel() With {
            .VerticalAlignment = VerticalAlignment.Center,
            .HorizontalAlignment = If(alignment = TextAlignment.Center, HorizontalAlignment.Center, HorizontalAlignment.Left),
            .Margin = New Thickness(padding)
        }
        panel.Children.Add(CreateText(eyebrow, mainForeground, 7, FontWeights.SemiBold, New Thickness(0, 0, 0, 5), alignment))
        Dim heading = If(theme = "fashionia" AndAlso (CmbLayout.SelectedValue?.ToString() = "editorial" OrElse CmbLayout.SelectedValue?.ToString() = "split"), title.ToUpperInvariant(), title)
        panel.Children.Add(CreateText(heading, mainForeground, titleSize, FontWeights.SemiBold, New Thickness(0), alignment, GetThemeFont(theme), 0.94))
        panel.Children.Add(CreateText(description, mainForeground, 8, FontWeights.Normal, New Thickness(0, 6, 0, 0), alignment, maxWidth:=280))
        If linkOnly Then
            panel.Children.Add(CreateText(buttonText & "  ───", mainForeground, 8, FontWeights.SemiBold, New Thickness(0, 9, 0, 0)))
        Else
            Dim buttonBackground = If(whiteButton, Colors.White, accent)
            Dim buttonForeground = If(whiteButton, Colors.Black, Colors.White)
            Dim button = CreateCta(buttonText, buttonBackground, buttonForeground, outlineButton, If(squareButton, New CornerRadius(0), If(theme = "minimal", New CornerRadius(3), New CornerRadius(12))), New Thickness(0, 9, 0, 0))
            If alignment = TextAlignment.Center Then button.HorizontalAlignment = HorizontalAlignment.Center
            panel.Children.Add(button)
        End If
        Return panel
    End Function

    Private Function CreateText(text As String, foreground As Brush, fontSize As Double, fontWeight As FontWeight,
                                Optional margin As Thickness = Nothing, Optional alignment As TextAlignment = TextAlignment.Left,
                                Optional fontFamily As FontFamily = Nothing, Optional lineHeight As Double = 0,
                                Optional maxWidth As Double = Double.PositiveInfinity) As TextBlock
        Dim block As New TextBlock() With {
            .Text = text,
            .Foreground = foreground,
            .FontSize = fontSize,
            .FontWeight = fontWeight,
            .Margin = margin,
            .TextAlignment = alignment,
            .TextWrapping = TextWrapping.Wrap,
            .MaxWidth = maxWidth
        }
        If fontFamily IsNot Nothing Then block.FontFamily = fontFamily
        If lineHeight > 0 Then block.LineHeight = fontSize * lineHeight
        If alignment = TextAlignment.Center Then block.HorizontalAlignment = HorizontalAlignment.Center
        Return block
    End Function

    Private Function CreateCta(text As String, backgroundColor As Color, foregroundColor As Color, isOutline As Boolean,
                               cornerRadius As CornerRadius, margin As Thickness) As Border
        Dim button As New Border() With {
            .BorderBrush = New SolidColorBrush(If(isOutline, foregroundColor, backgroundColor)),
            .BorderThickness = New Thickness(If(isOutline, 1, 0)),
            .Background = If(isOutline, Brushes.Transparent, New SolidColorBrush(backgroundColor)),
            .CornerRadius = cornerRadius,
            .Padding = New Thickness(9, 5, 9, 5),
            .Margin = margin,
            .HorizontalAlignment = HorizontalAlignment.Left
        }
        button.Child = CreateText(text, New SolidColorBrush(foregroundColor), 7, FontWeights.SemiBold)
        Return button
    End Function

    Private Function CreateImageSurface(path As String, fallbackColor As Color, height As Double,
                                        Optional stretch As Stretch = Stretch.UniformToFill) As Grid
        Dim surface As New Grid() With {.Height = height, .ClipToBounds = True, .Background = New SolidColorBrush(fallbackColor)}
        Dim accentBrush As New SolidColorBrush(fallbackColor)
        surface.Background = New LinearGradientBrush(
            New GradientStopCollection From {
                New GradientStop(Color.FromArgb(&H50, accentBrush.Color.R, accentBrush.Color.G, accentBrush.Color.B), 0),
                New GradientStop(Color.FromArgb(&H20, &HFF, &HFF, &HFF), 1)
            },
            New Point(0, 0),
            New Point(1, 1))

        If String.IsNullOrWhiteSpace(path) Then Return surface
        Dim imageUri = ResolveImageUri(path)
        If imageUri Is Nothing Then
            TxtFeedback.Text = "Couldn't preview an image: enter a valid image URL or file path."
            Return surface
        End If

        Try
            Dim bitmap As New BitmapImage()
            bitmap.BeginInit()
            bitmap.UriSource = imageUri
            bitmap.CacheOption = If(imageUri.IsFile, BitmapCacheOption.OnLoad, BitmapCacheOption.OnDemand)
            bitmap.EndInit()
            Dim image As New Image() With {.Source = bitmap, .Stretch = stretch}
            AddHandler image.ImageFailed, AddressOf PreviewImage_Failed
            surface.Children.Add(image)
        Catch ex As Exception
            TxtFeedback.Text = "Couldn't preview an image: " & ex.Message
        End Try
        Return surface
    End Function

    Private Function CreateStoreSections(theme As String, layout As String, storeName As String, accent As Color) As UIElement
        Dim section As New StackPanel() With {.Margin = New Thickness(0, 4, 0, 0)}
        Dim foreground = GetThemeForeground(theme)
        Dim accentBrush As New SolidColorBrush(accent)
        Dim isEditorial = layout = "editorial"
        Dim heading = If(layout = "catalog", "Browse all products", "Shop " & storeName)
        Dim eyebrow = If(theme = "fashionia", "Selected pieces", If(theme = "clarity", "Small rituals, considered objects", "The collection"))
        section.Children.Add(CreateText(eyebrow, accentBrush, 7, FontWeights.SemiBold))
        section.Children.Add(CreateText(heading, foreground, If(isEditorial, 19, 16), FontWeights.SemiBold, New Thickness(0, 3, 0, 10), fontFamily:=GetThemeFont(theme)))

        Dim products = DataCache.Products.Where(Function(product) product IsNot Nothing).
            Take(If(layout = "spotlight" OrElse layout = "product-first", 4, 3)).ToList()
        Dim hasFeatured = (layout = "spotlight" OrElse layout = "product-first") AndAlso products.Count > 0
        If hasFeatured Then
            section.Children.Add(CreateFeaturedProduct(theme, products(0), accent))
            products = products.Skip(1).ToList()
        End If

        If products.Count = 0 Then
            section.Children.Add(CreateText("The collection is on its way. Add products to bring this storefront to life.",
                                            foreground, 8, FontWeights.Normal, New Thickness(0, 8, 0, 12), TextAlignment.Center))
            Return section
        End If

        Dim columns = If(IsMobilePreview(), 2, If(layout = "catalog", 4, If(layout = "editorial", 2, 3)))
        Dim productGrid As New UniformGrid() With {.Columns = Math.Min(columns, products.Count), .Margin = New Thickness(-3, 0, -3, 0)}
        For Each product In products
            productGrid.Children.Add(CreateProductCard(theme, layout, product, accent))
        Next
        section.Children.Add(productGrid)
        Return section
    End Function

    Private Function CreateFeaturedProduct(theme As String, product As LocalData.ProductItem, accent As Color) As Border
        Dim feature As New Grid() With {
            .Margin = New Thickness(0, 0, 0, 10),
            .Background = GetThemeSurface(theme)
        }
        feature.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        feature.ColumnDefinitions.Add(New ColumnDefinition() With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim image = CreateImageSurface(product.thumbnail, accent, If(IsMobilePreview(), 90, 110))
        Dim copy As New StackPanel() With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(8)}
        copy.Children.Add(CreateText("Featured piece", New SolidColorBrush(accent), 7, FontWeights.SemiBold))
        If Not String.IsNullOrWhiteSpace(product.category) Then copy.Children.Add(CreateText(product.category, GetThemeForeground(theme), 6, FontWeights.Normal, New Thickness(0, 5, 0, 0)))
        copy.Children.Add(CreateText(product.title, GetThemeForeground(theme), 10, FontWeights.SemiBold, New Thickness(0, 3, 0, 0), fontFamily:=GetThemeFont(theme)))
        copy.Children.Add(CreateText("GHC " & product.price.ToString("N2"), New SolidColorBrush(accent), 8, FontWeights.SemiBold, New Thickness(0, 5, 0, 0)))

        If IsMobilePreview() Then
            Dim stacked As New StackPanel()
            stacked.Children.Add(image)
            stacked.Children.Add(copy)
            Return New Border() With {
                .BorderBrush = New SolidColorBrush(Color.FromArgb(&H30, GetThemeForeground(theme).Color.R, GetThemeForeground(theme).Color.G, GetThemeForeground(theme).Color.B)),
                .BorderThickness = New Thickness(1),
                .Child = stacked
            }
        End If

        Grid.SetColumn(copy, 1)
        feature.Children.Add(image)
        feature.Children.Add(copy)
        Return New Border() With {
            .BorderBrush = New SolidColorBrush(Color.FromArgb(&H30, GetThemeForeground(theme).Color.R, GetThemeForeground(theme).Color.G, GetThemeForeground(theme).Color.B)),
            .BorderThickness = New Thickness(1),
            .Child = feature
        }
    End Function

    Private Function CreateProductCard(theme As String, layout As String, product As LocalData.ProductItem, accent As Color) As Border
        Dim card As New StackPanel() With {.Margin = New Thickness(3, 0, 3, 5)}
        Dim imageHeight = If(IsMobilePreview(), 68D, If(layout = "catalog", 88D, 78D))
        Dim image = CreateImageSurface(product.thumbnail, Color.FromArgb(&H18, accent.R, accent.G, accent.B), imageHeight)
        card.Children.Add(New Border() With {
            .Child = image,
            .CornerRadius = If(theme = "fashionia" OrElse theme = "studio-air", New CornerRadius(0), If(layout = "catalog", New CornerRadius(5), New CornerRadius(9))),
            .ClipToBounds = True
        })
        If Not String.IsNullOrWhiteSpace(product.category) Then
            card.Children.Add(CreateText(product.category.ToUpperInvariant(), GetThemeForeground(theme), 5, FontWeights.Normal, New Thickness(0, 4, 0, 0)))
        End If
        card.Children.Add(CreateText(product.title, GetThemeForeground(theme), 7, FontWeights.SemiBold, New Thickness(0, 2, 0, 0), fontFamily:=GetThemeFont(theme)))
        card.Children.Add(CreateText("GHC " & product.price.ToString("N2"), GetThemeForeground(theme), 7, FontWeights.SemiBold, New Thickness(0, 2, 0, 0)))
        Return New Border() With {.Child = card}
    End Function

    Private Function FirstProduct() As LocalData.ProductItem
        Return DataCache.Products.FirstOrDefault(Function(product) product IsNot Nothing)
    End Function

    Private Sub SetPreviewImage(image As Image, path As String, fallback As FrameworkElement)
        image.Source = Nothing
        image.Visibility = Visibility.Collapsed
        fallback.Visibility = Visibility.Visible
        If String.IsNullOrWhiteSpace(path) Then Return

        Try
            Dim imageUri = ResolveImageUri(path)
            If imageUri Is Nothing Then
                Throw New InvalidOperationException("Enter a valid image URL or file path.")
            End If

            Dim bitmap As New BitmapImage()
            bitmap.BeginInit()
            bitmap.UriSource = imageUri
            bitmap.CacheOption = If(imageUri.IsFile, BitmapCacheOption.OnLoad, BitmapCacheOption.OnDemand)
            bitmap.EndInit()
            image.Source = bitmap
            image.Visibility = Visibility.Visible
            fallback.Visibility = Visibility.Collapsed
        Catch ex As Exception
            TxtFeedback.Text = "Couldn't preview an image: " & ex.Message
        End Try
    End Sub

    Private Shared Function ResolveImageUri(path As String) As Uri
        Dim trimmed = If(path, String.Empty).Trim()
        If String.IsNullOrWhiteSpace(trimmed) Then Return Nothing

        If File.Exists(trimmed) Then Return New Uri(System.IO.Path.GetFullPath(trimmed), UriKind.Absolute)

        Dim imageUri As Uri = Nothing
        If Uri.TryCreate(trimmed, UriKind.Absolute, imageUri) AndAlso
           (imageUri.Scheme = Uri.UriSchemeHttp OrElse imageUri.Scheme = Uri.UriSchemeHttps OrElse imageUri.Scheme = Uri.UriSchemeFile) Then
            Return imageUri
        End If
        Return Nothing
    End Function

    Private Sub PreviewImage_Failed(sender As Object, e As ExceptionRoutedEventArgs)
        Dim image = TryCast(sender, Image)
        If image IsNot Nothing Then image.Visibility = Visibility.Collapsed

        If image Is PreviewAvatarImage Then
            PreviewAvatarFallback.Visibility = Visibility.Visible
        ElseIf image Is PreviewCoverImage1 Then
            PreviewCoverGraphic1.Visibility = Visibility.Visible
        ElseIf image Is PreviewCoverImage2 Then
            PreviewCoverGraphic2.Visibility = Visibility.Visible
        End If
        TxtFeedback.Text = "Couldn't load a storefront image. Check that the selected file or image URL is available."
    End Sub

    Private Function IsValidHexColor(hex As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(hex) AndAlso Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$")
    End Function

    Private Function TryGetAccentColor(hex As String, ByRef color As Color) As Boolean
        color = Colors.HotPink
        If Not IsValidHexColor(hex) Then Return False
        color = CType(ColorConverter.ConvertFromString(hex), Color)
        Return True
    End Function

    Private Async Sub StorefrontEnabled_Changed(sender As Object, e As RoutedEventArgs)
        If isInitializing OrElse isUpdatingSwitch Then Return
        Dim enabled = ChkStorefrontEnabled.IsChecked.GetValueOrDefault(False)
        ChkStorefrontEnabled.IsEnabled = False
        UpdateStorefrontStatus()

        Try
            Await SupabaseService.SetStorefrontEnabledAsync(enabled)
            DataCache.StoreSettings.storefront_enabled = enabled
            TxtFeedback.Text = If(enabled, "Storefront activated.", "Storefront hidden from shoppers.")
        Catch ex As Exception
            isUpdatingSwitch = True
            ChkStorefrontEnabled.IsChecked = Not enabled
            isUpdatingSwitch = False
            UpdateStorefrontStatus()
            TxtFeedback.Text = ex.Message
        Finally
            ChkStorefrontEnabled.IsEnabled = True
        End Try
    End Sub

    Private Sub UpdateStorefrontStatus()
        Dim enabled = ChkStorefrontEnabled.IsChecked.GetValueOrDefault(False)
        TxtStorefrontStatus.Text = If(enabled, "Storefront is active", "Storefront is inactive")
        TxtStorefrontStatusDescription.Text = If(enabled, "Shoppers can visit your public store page.", "Your public store page is hidden from shoppers.")
    End Sub

    Private Sub BtnBrowseAvatar_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New Microsoft.Win32.OpenFileDialog() With {
            .Title = "Select avatar image",
            .Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        }
        If dialog.ShowDialog() = True Then TxtAvatar.Text = dialog.FileName
    End Sub

    Private Sub BtnBrowseCover_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New Microsoft.Win32.OpenFileDialog() With {
            .Title = "Select cover image",
            .Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        }
        If dialog.ShowDialog() = True Then TxtCoverImage.Text = dialog.FileName
    End Sub

    Private Sub BtnBrowseCover2_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New Microsoft.Win32.OpenFileDialog() With {
            .Title = "Select second cover image",
            .Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        }
        If dialog.ShowDialog() = True Then TxtCoverImage2.Text = dialog.FileName
    End Sub

    Private Async Sub BtnSaveTheme_Click(sender As Object, e As RoutedEventArgs)
        Dim storeName = TxtStoreName.Text.Trim()
        Dim email = TxtStoreEmail.Text.Trim()
        If String.IsNullOrWhiteSpace(storeName) OrElse String.IsNullOrWhiteSpace(email) Then
            MessageBox.Show("Enter a store name and contact email.", "Storefront", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If
        If Not email.Contains("@") Then
            MessageBox.Show("Enter a valid contact email.", "Storefront", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If
        If Not IsValidHexColor(TxtThemeColor.Text.Trim()) Then
            MessageBox.Show("Enter a valid six-digit hex color, such as #FF5A79.", "Storefront", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        BtnSaveTheme.IsEnabled = False
        BtnSaveTheme.Content = "Saving..."
        TxtFeedback.Text = "Saving storefront changes..."
        Try
            Dim theme = If(TryCast(CmbTheme.SelectedValue, String), "chicwave")
            Dim layout = If(TryCast(CmbLayout.SelectedValue, String), "classic")
            Dim avatar = Await PrepareStorefrontImageAsync(TxtAvatar.Text.Trim(), "avatar")
            Dim coverImage = Await PrepareStorefrontImageAsync(TxtCoverImage.Text.Trim(), "cover")
            Dim coverImage2 = Await PrepareStorefrontImageAsync(TxtCoverImage2.Text.Trim(), "cover-2")
            Dim settings = New LocalData.StoreSettings() With {
                .name = storeName,
                .email = email,
                .description = TxtDescription.Text.Trim(),
                .store_description = TxtStoreDescription.Text.Trim(),
                .theme_color = TxtThemeColor.Text.Trim().ToUpperInvariant(),
                .storefront_theme = theme,
                .storefront_layout = layout,
                .storefront_enabled = ChkStorefrontEnabled.IsChecked.GetValueOrDefault(False),
                .avatar = avatar,
                .cover_image = coverImage,
                .cover_image_2 = coverImage2,
                .social_website = If(String.IsNullOrWhiteSpace(TxtSocialWebsite.Text.Trim()), Nothing, NormalizeSocialLink(TxtSocialWebsite.Text, False)),
                .social_facebook = If(String.IsNullOrWhiteSpace(TxtSocialFacebook.Text.Trim()), Nothing, NormalizeSocialLink(TxtSocialFacebook.Text, False)),
                .social_instagram = If(String.IsNullOrWhiteSpace(TxtSocialInstagram.Text.Trim()), Nothing, NormalizeSocialLink(TxtSocialInstagram.Text, False)),
                .social_twitter = If(String.IsNullOrWhiteSpace(TxtSocialTwitter.Text.Trim()), Nothing, NormalizeSocialLink(TxtSocialTwitter.Text, False)),
                .social_whatsapp = If(String.IsNullOrWhiteSpace(TxtSocialWhatsapp.Text.Trim()), Nothing, NormalizeSocialLink(TxtSocialWhatsapp.Text, True))
            }

            Await SupabaseService.SaveStoreSettingsAsync(settings)
            DataCache.StoreSettings = settings
            CType(Application.Current.MainWindow, MainWindow).RefreshStoreIdentity()
            selectedAccent = settings.theme_color
            TxtFeedback.Text = "Storefront saved."
        Catch ex As Exception
            TxtFeedback.Text = ex.Message
        Finally
            BtnSaveTheme.IsEnabled = True
            BtnSaveTheme.Content = "Save"
        End Try
    End Sub

    Private Async Function PrepareStorefrontImageAsync(value As String, label As String) As Task(Of String)
        If String.IsNullOrWhiteSpace(value) Then Return Nothing
        If AppSession.Mode <> StoreMode.Live Then Return value
        If File.Exists(value) Then Return Await SupabaseService.UploadStorefrontImageAsync(value, label)

        Dim imageUri As Uri = Nothing
        If Uri.TryCreate(value, UriKind.Absolute, imageUri) AndAlso
           (imageUri.Scheme = Uri.UriSchemeHttp OrElse imageUri.Scheme = Uri.UriSchemeHttps) Then
            Return value
        End If
        Throw New InvalidOperationException("The " & label & " image must be a public URL or an existing local image file.")
    End Function
End Class
