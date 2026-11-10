Imports System
Imports System.Windows
Imports System.Windows.Controls
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports Microsoft.Win32

Partial Public Class AddProductDialog
    Inherits Window

    Private ReadOnly sourceProduct As LocalData.ProductItem
    Private ReadOnly imagePaths As New List(Of String)()
    Private ReadOnly existingImageUrls As New List(Of String)()
    Private ReadOnly specificationEditors As New List(Of SpecificationEditor)()
    Private currentStep As Integer = 1
    Private selectedVideoPath As String
    Private existingVideoUrl As String
    Private existingVideoKey As String
    Public Property NewProduct As LocalData.ProductItem

    Private Class SpecificationEditor
        Public Property Container As StackPanel
        Public Property KeyInput As TextBox
        Public Property ValueInput As TextBox
    End Class

    Public Sub New(Optional product As LocalData.ProductItem = Nothing)
        InitializeComponent()
        sourceProduct = product
        InitializeOptionSelectors()
        If sourceProduct IsNot Nothing Then PopulateProduct()
        AddHandler Loaded, AddressOf Dialog_Loaded
        ShowStep(1)
    End Sub

    Private Async Sub Dialog_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Dialog_Loaded
        CategoryLoadingIndicator.Visibility = Visibility.Visible
        Try
            Dim categories = Await SupabaseService.LoadProductCategoriesAsync()
            If sourceProduct IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(sourceProduct.category) AndAlso
                Not categories.Any(Function(category) String.Equals(category.Name, sourceProduct.category, StringComparison.OrdinalIgnoreCase)) Then
                categories.Add(New SupabaseService.ProductCategory() With {.Id = sourceProduct.category_id, .Name = sourceProduct.category})
            End If
            CmbCategory.ItemsSource = categories
            If sourceProduct IsNot Nothing Then
                CmbCategory.SelectedItem = categories.FirstOrDefault(Function(category) String.Equals(category.Name, sourceProduct.category, StringComparison.OrdinalIgnoreCase))
            Else
                CmbCategory.SelectedIndex = -1
            End If
        Catch ex As Exception
            TxtSaveProgress.Text = "Categories could not be loaded: " & ex.Message
        Finally
            CategoryLoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Sub InitializeOptionSelectors()
        For Each size In {"XS", "S", "M", "L", "XL", "XXL"}
            SizesPanel.Children.Add(New CheckBox() With {
                .Content = size, .Tag = size, .Margin = New Thickness(0, 0, 14, 8),
                .Foreground = TryCast(FindResource("TextDark"), Brush), .VerticalAlignment = VerticalAlignment.Center
            })
        Next

        Dim colorChoices As (Name As String, Hex As String)() = {
            ("Black", "#000000"), ("White", "#FFFFFF"), ("Red", "#EF4444"), ("Blue", "#3B82F6"),
            ("Green", "#10B981"), ("Yellow", "#F59E0B"), ("Purple", "#8B5CF6"), ("Pink", "#EC4899"),
            ("Orange", "#F97316"), ("Brown", "#92400E"), ("Gray", "#6B7280"), ("Navy", "#1E3A8A")
        }
        For Each colorChoice In colorChoices
            Dim colorContent As New StackPanel() With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
            colorContent.Children.Add(New Ellipse() With {
                .Width = 18, .Height = 18,
                .Fill = New SolidColorBrush(CType(ColorConverter.ConvertFromString(colorChoice.Hex), Color)),
                .Stroke = Brushes.Gray, .StrokeThickness = If(colorChoice.Name = "White", 1.5, 0.5),
                .Margin = New Thickness(0, 0, 6, 0), .VerticalAlignment = VerticalAlignment.Center
            })
            colorContent.Children.Add(New TextBlock() With {.Text = colorChoice.Name, .VerticalAlignment = VerticalAlignment.Center})
            ColorsPanel.Children.Add(New CheckBox() With {
                .Content = colorContent, .Tag = colorChoice.Name, .ToolTip = colorChoice.Name,
                .Margin = New Thickness(0, 0, 12, 8), .VerticalAlignment = VerticalAlignment.Center
            })
        Next
    End Sub

    Private Sub PopulateProduct()
        TxtDialogTitle.Text = "Edit Product"
        TxtProductName.Text = sourceProduct.title
        TxtPrice.Text = sourceProduct.price.ToString("0.00")
        TxtShippingFee.Text = sourceProduct.shipping_fee.ToString("0.00")
        TxtDescription.Text = sourceProduct.description
        TxtQuantity.Text = sourceProduct.quantity.ToString()
        TxtDiscount.Text = sourceProduct.discount.ToString()
        TxtCompareAtPrice.Text = If(sourceProduct.compare_at_price.HasValue, sourceProduct.compare_at_price.Value.ToString("0.00"), String.Empty)
        TxtCostPrice.Text = sourceProduct.cost_price.ToString("0.00")
        TxtSku.Text = sourceProduct.sku
        TxtBarcode.Text = sourceProduct.barcode
        TxtVendor.Text = sourceProduct.vendor
        TxtWeight.Text = If(sourceProduct.weight.HasValue, sourceProduct.weight.Value.ToString("0.##"), String.Empty)
        TxtTags.Text = String.Join(", ", If(sourceProduct.tags, New List(Of String)()))
        ChkPreorder.IsChecked = sourceProduct.is_preorder
        ChkTrackInventory.IsChecked = sourceProduct.track_inventory
        ChkAllowBackorder.IsChecked = sourceProduct.allow_backorder

        For Each optionItem As ComboBoxItem In CmbWeightUnit.Items
            If String.Equals(CStr(optionItem.Content), sourceProduct.weight_unit, StringComparison.OrdinalIgnoreCase) Then
                CmbWeightUnit.SelectedItem = optionItem
                Exit For
            End If
        Next

        SetOptionSelections(SizesPanel, sourceProduct.sizes)
        SetOptionSelections(ColorsPanel, sourceProduct.colors)
        If sourceProduct.specifications IsNot Nothing Then
            For Each item In sourceProduct.specifications
                AddSpecification(item.Key, item.Value)
            Next
        End If
        existingVideoUrl = sourceProduct.video_url
        existingVideoKey = sourceProduct.r2_video_key
        UpdateVideoStatus()

        If sourceProduct.thumbnails IsNot Nothing Then existingImageUrls.AddRange(sourceProduct.thumbnails.Where(Function(url) Not String.IsNullOrWhiteSpace(url)))
        If existingImageUrls.Count = 0 AndAlso Not String.IsNullOrWhiteSpace(sourceProduct.thumbnail) AndAlso Not sourceProduct.thumbnail.Contains("placeholder.png", StringComparison.OrdinalIgnoreCase) Then
            existingImageUrls.Add(sourceProduct.thumbnail)
        End If
        RenderImagePreviews()
    End Sub

    Private Sub SetOptionSelections(panel As WrapPanel, values As List(Of String))
        If values Is Nothing Then Return
        For Each optionCheck In panel.Children.OfType(Of CheckBox)()
            optionCheck.IsChecked = values.Contains(CStr(optionCheck.Tag), StringComparer.OrdinalIgnoreCase)
        Next
    End Sub

    Private Sub ShowStep(stepNumber As Integer)
        currentStep = Math.Max(1, Math.Min(4, stepNumber))
        MediaStepPanel.Visibility = If(currentStep = 1, Visibility.Visible, Visibility.Collapsed)
        BasicsStepPanel.Visibility = If(currentStep = 2, Visibility.Visible, Visibility.Collapsed)
        InventoryStepPanel.Visibility = If(currentStep = 3, Visibility.Visible, Visibility.Collapsed)
        DetailsStepPanel.Visibility = If(currentStep = 4, Visibility.Visible, Visibility.Collapsed)
        Dim labels = {"Media", "Basics", "Inventory", "Details"}
        TxtStepCaption.Text = labels(currentStep - 1) & " - Step " & currentStep.ToString() & " of 4"
        FormProgress.Value = currentStep
        Dim accent = TryCast(FindResource("TagitPrimary"), Brush)
        Dim muted = TryCast(FindResource("TextMuted"), Brush)
        LblMediaStep.Foreground = If(currentStep >= 1, accent, muted)
        LblBasicsStep.Foreground = If(currentStep >= 2, accent, muted)
        LblInventoryStep.Foreground = If(currentStep >= 3, accent, muted)
        LblDetailsStep.Foreground = If(currentStep >= 4, accent, muted)
        BtnBack.Visibility = If(currentStep = 1, Visibility.Collapsed, Visibility.Visible)
        BtnContinue.Visibility = If(currentStep < 4, Visibility.Visible, Visibility.Collapsed)
        BtnPublish.Visibility = If(currentStep = 4, Visibility.Visible, Visibility.Collapsed)
        If currentStep = 4 Then UpdateReview()
    End Sub

    Private Sub BtnContinue_Click(sender As Object, e As RoutedEventArgs)
        If Not ValidateCurrentStep() Then Return
        ShowStep(currentStep + 1)
    End Sub

    Private Sub BtnBack_Click(sender As Object, e As RoutedEventArgs)
        ShowStep(currentStep - 1)
    End Sub

    Private Function ValidateCurrentStep() As Boolean
        If currentStep = 1 AndAlso imagePaths.Count + existingImageUrls.Count = 0 Then
            MessageBox.Show("Choose at least one product photo before continuing.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return False
        End If

        If currentStep = 2 Then
            Dim price As Decimal
            If String.IsNullOrWhiteSpace(TxtProductName.Text) Then
                MessageBox.Show("Enter a product title.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return False
            End If
            If Not Decimal.TryParse(TxtPrice.Text, price) OrElse price <= 0D Then
                MessageBox.Show("Enter a selling price greater than zero.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return False
            End If
            If CmbCategory.SelectedItem Is Nothing Then
                MessageBox.Show("Choose a product category.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return False
            End If
        End If

        If currentStep = 3 Then
            Dim quantity As Integer
            If Not CBool(ChkPreorder.IsChecked) AndAlso (Not Integer.TryParse(TxtQuantity.Text, quantity) OrElse quantity < 0) Then
                MessageBox.Show("Enter a non-negative quantity or mark the product as preorder.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return False
            End If
            Dim discount As Decimal
            If Not Decimal.TryParse(If(String.IsNullOrWhiteSpace(TxtDiscount.Text), "0", TxtDiscount.Text), discount) OrElse discount < 0D OrElse discount > 100D Then
                MessageBox.Show("Discount must be between 0 and 100 percent.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return False
            End If
        End If
        Return True
    End Function

    Private Sub BtnAddPhotos_Click(sender As Object, e As RoutedEventArgs)
        Dim remaining = 5 - imagePaths.Count - existingImageUrls.Count
        If remaining <= 0 Then
            MessageBox.Show("You can upload up to five photos.", "Product photos", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim picker As New OpenFileDialog() With {
            .Title = "Choose product photos",
            .Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.gif|All files|*.*",
            .Multiselect = True
        }
        If picker.ShowDialog(Me) <> True Then Return
        For Each filePath In picker.FileNames.Take(remaining)
            imagePaths.Add(filePath)
        Next
        RenderImagePreviews()
    End Sub

    Private Sub RenderImagePreviews()
        ImagePreviewPanel.Children.Clear()
        For Each path In existingImageUrls.Concat(imagePaths)
            Dim card As New Border() With {
                .Width = 128, .Margin = New Thickness(6), .Padding = New Thickness(6),
                .BorderThickness = New Thickness(1)
            }
            card.SetResourceReference(Border.BackgroundProperty, "CardSurface")
            card.SetResourceReference(Border.BorderBrushProperty, "TagitBorder")
            Dim content As New StackPanel()
            Dim image As New Image() With {.Width = 112, .Height = 100, .Stretch = Stretch.UniformToFill, .Margin = New Thickness(0, 0, 0, 6)}
            Try
                Dim bitmap As New BitmapImage()
                bitmap.BeginInit()
                bitmap.UriSource = New Uri(path, UriKind.Absolute)
                bitmap.CacheOption = BitmapCacheOption.OnLoad
                bitmap.EndInit()
                image.Source = bitmap
            Catch
                image.Source = New BitmapImage(New Uri("pack://application:,,,/assets/placeholder.png", UriKind.Absolute))
            End Try
            content.Children.Add(image)
            Dim removeButton = CreateRemoveButton(path, "Remove photo")
            removeButton.Height = 30
            AddHandler removeButton.Click, AddressOf BtnRemoveImage_Click
            content.Children.Add(removeButton)
            card.Child = content
            ImagePreviewPanel.Children.Add(card)
        Next
        TxtPhotoCount.Text = (existingImageUrls.Count + imagePaths.Count).ToString() & " / 5 photos"
        UpdateReview()
    End Sub

    Private Sub BtnRemoveImage_Click(sender As Object, e As RoutedEventArgs)
        Dim path = CStr(DirectCast(sender, FrameworkElement).Tag)
        If existingImageUrls.Contains(path) Then
            existingImageUrls.Remove(path)
        Else
            imagePaths.Remove(path)
        End If
        RenderImagePreviews()
    End Sub

    Private Sub BtnPickVideo_Click(sender As Object, e As RoutedEventArgs)
        Dim picker As New OpenFileDialog() With {
            .Title = "Choose product video",
            .Filter = "Video files|*.mp4;*.mov;*.m4v"
        }
        If picker.ShowDialog(Me) <> True Then Return
        Dim fileInfo As New FileInfo(picker.FileName)
        If fileInfo.Length > 30 * 1024 * 1024 Then
            MessageBox.Show("The video exceeds the 30 MB limit.", "Product video", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If
        selectedVideoPath = picker.FileName
        UpdateVideoStatus()
        UpdateReview()
    End Sub

    Private Sub BtnRemoveVideo_Click(sender As Object, e As RoutedEventArgs)
        selectedVideoPath = Nothing
        existingVideoUrl = Nothing
        existingVideoKey = Nothing
        UpdateVideoStatus()
        UpdateReview()
    End Sub

    Private Sub UpdateVideoStatus()
        If Not String.IsNullOrWhiteSpace(selectedVideoPath) Then
            TxtVideoStatus.Text = Path.GetFileName(selectedVideoPath)
            BtnRemoveVideo.Visibility = Visibility.Visible
        ElseIf Not String.IsNullOrWhiteSpace(existingVideoUrl) Then
            TxtVideoStatus.Text = "Existing product video"
            BtnRemoveVideo.Visibility = Visibility.Visible
        Else
            TxtVideoStatus.Text = "No video selected"
            BtnRemoveVideo.Visibility = Visibility.Collapsed
        End If
    End Sub

    Private Async Sub BtnAutoCreate_Click(sender As Object, e As RoutedEventArgs)
        If imagePaths.Count + existingImageUrls.Count = 0 Then
            MessageBox.Show("Choose a product photo first.", "Auto-create", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim priceHint As Decimal?
        Dim parsedPrice As Decimal
        If Not String.IsNullOrWhiteSpace(TxtAgentPriceHint.Text) Then
            If Not Decimal.TryParse(TxtAgentPriceHint.Text, parsedPrice) OrElse parsedPrice <= 0D Then
                MessageBox.Show("Enter a valid positive price hint.", "Auto-create", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            priceHint = parsedPrice
        End If
        Dim quantityHint As Integer?
        Dim parsedQuantity As Integer
        If Not String.IsNullOrWhiteSpace(TxtAgentQuantityHint.Text) Then
            If Not Integer.TryParse(TxtAgentQuantityHint.Text, parsedQuantity) OrElse parsedQuantity < 0 Then
                MessageBox.Show("Enter a valid non-negative quantity hint.", "Auto-create", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            quantityHint = parsedQuantity
        End If

        BtnAutoCreate.IsEnabled = False
        AutoCreateProgress.Visibility = Visibility.Visible
        TxtAutoCreateStatus.Text = "Analyzing photo and preparing a product draft..."
        Try
            Dim image As String
            If imagePaths.Count > 0 Then
                image = PrepareImageDataUri(imagePaths(0))
            ElseIf existingImageUrls(0).StartsWith("http://", StringComparison.OrdinalIgnoreCase) OrElse existingImageUrls(0).StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
                image = existingImageUrls(0)
            ElseIf File.Exists(existingImageUrls(0)) Then
                image = PrepareImageDataUri(existingImageUrls(0))
            Else
                Throw New InvalidOperationException("The selected photo could not be opened for analysis.")
            End If

            Dim categoryNames = If(TryCast(CmbCategory.ItemsSource, List(Of SupabaseService.ProductCategory)), New List(Of SupabaseService.ProductCategory)()).
                Select(Function(category) category.Name).
                Where(Function(name) Not String.IsNullOrWhiteSpace(name)).
                ToList()
            Dim currentCategory = TryCast(CmbCategory.SelectedItem, SupabaseService.ProductCategory)
            Dim draft = Await SupabaseService.GenerateProductDraftAsync(image, If(currentCategory Is Nothing, Nothing, currentCategory.Name), categoryNames, priceHint, quantityHint)

            TxtProductName.Text = If(draft.title, String.Empty)
            TxtDescription.Text = If(draft.description, String.Empty)
            If draft.price.HasValue Then TxtPrice.Text = draft.price.Value.ToString("0.00")
            If draft.quantity.HasValue Then TxtQuantity.Text = Math.Max(0, draft.quantity.Value).ToString()
            If Not String.IsNullOrWhiteSpace(draft.category) Then
                Dim loadedCategories = TryCast(CmbCategory.ItemsSource, List(Of SupabaseService.ProductCategory))
                If loadedCategories IsNot Nothing Then
                    CmbCategory.SelectedItem = loadedCategories.FirstOrDefault(Function(category) String.Equals(category.Name, draft.category, StringComparison.OrdinalIgnoreCase))
                End If
            End If

            TxtTags.Text = String.Join(", ", If(draft.tags, New List(Of String)()).Take(10))
            specificationEditors.Clear()
            SpecificationsPanel.Children.Clear()
            If draft.specifications IsNot Nothing Then
                For Each spec In draft.specifications.Where(Function(item) item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(item.key) AndAlso Not String.IsNullOrWhiteSpace(item.value))
                    AddSpecification(spec.key, spec.value)
                Next
            End If
            TxtAutoCreateStatus.Text = "Draft ready. Review the suggested details before publishing."
            ShowStep(4)
        Catch ex As Exception
            TxtAutoCreateStatus.Text = ex.Message
            MessageBox.Show(ex.Message, "Auto-create failed", MessageBoxButton.OK, MessageBoxImage.Warning)
        Finally
            BtnAutoCreate.IsEnabled = True
            AutoCreateProgress.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Function PrepareImageDataUri(filePath As String) As String
        Using sourceStream = File.OpenRead(filePath)
            Dim decoder = BitmapDecoder.Create(sourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
            Dim frame = decoder.Frames(0)
            Dim scale = Math.Min(1.0, 1600.0 / Math.Max(frame.PixelWidth, frame.PixelHeight))
            Dim imageSource As BitmapSource = frame
            If scale < 1.0 Then imageSource = New TransformedBitmap(frame, New ScaleTransform(scale, scale))
            Dim encoder As New JpegBitmapEncoder() With {.QualityLevel = 85}
            encoder.Frames.Add(BitmapFrame.Create(imageSource))
            Using compressedStream As New MemoryStream()
                encoder.Save(compressedStream)
                Return "data:image/jpeg;base64," & Convert.ToBase64String(compressedStream.ToArray())
            End Using
        End Using
    End Function

    Private Sub BtnAddSpecification_Click(sender As Object, e As RoutedEventArgs)
        AddSpecification(String.Empty, String.Empty)
    End Sub

    Private Sub AddSpecification(key As String, value As String)
        Dim editor As New SpecificationEditor()
        editor.Container = New StackPanel() With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 8)}
        editor.KeyInput = New TextBox() With {.Style = TryCast(FindResource("LoginInput"), Style), .Width = 235, .Margin = New Thickness(0, 0, 10, 0), .Tag = "Name"}
        editor.ValueInput = New TextBox() With {.Style = TryCast(FindResource("LoginInput"), Style), .Width = 235, .Margin = New Thickness(0, 0, 10, 0), .Tag = "Value"}
        editor.KeyInput.Text = key
        editor.ValueInput.Text = value
        Dim removeButton = CreateRemoveButton(editor, "Remove specification")
        AddHandler removeButton.Click, AddressOf BtnRemoveSpecification_Click
        editor.Container.Children.Add(editor.KeyInput)
        editor.Container.Children.Add(editor.ValueInput)
        editor.Container.Children.Add(removeButton)
        specificationEditors.Add(editor)
        SpecificationsPanel.Children.Add(editor.Container)
    End Sub

    Private Sub BtnRemoveSpecification_Click(sender As Object, e As RoutedEventArgs)
        Dim editor = TryCast(DirectCast(sender, FrameworkElement).Tag, SpecificationEditor)
        If editor Is Nothing Then Return
        specificationEditors.Remove(editor)
        SpecificationsPanel.Children.Remove(editor.Container)
    End Sub

    Private Function CreateRemoveButton(tag As Object, tooltip As String) As Button
        Return New Button() With {
            .Content = New TextBlock() With {.Text = ChrW(&HE74D), .FontFamily = New FontFamily("Segoe MDL2 Assets"), .FontSize = 14, .VerticalAlignment = VerticalAlignment.Center, .HorizontalAlignment = HorizontalAlignment.Center},
            .Width = 38, .Height = 38, .Padding = New Thickness(0), .Tag = tag, .ToolTip = tooltip,
            .Style = TryCast(FindResource("OutlineButton"), Style)
        }
    End Function

    Private Sub UpdateReview()
        If TxtReviewTitle Is Nothing Then Return
        TxtReviewTitle.Text = If(String.IsNullOrWhiteSpace(TxtProductName.Text), "Product title", TxtProductName.Text.Trim())
        Dim price As Decimal
        TxtReviewPrice.Text = "GHC " & If(Decimal.TryParse(TxtPrice.Text, price), price.ToString("N2"), "0.00")
        TxtReviewStock.Text = If(CBool(ChkPreorder.IsChecked), "Stock: Preorder", "Stock: " & If(String.IsNullOrWhiteSpace(TxtQuantity.Text), "0", TxtQuantity.Text.Trim()))
        TxtReviewMedia.Text = (imagePaths.Count + existingImageUrls.Count).ToString() & " photos" & If(String.IsNullOrWhiteSpace(selectedVideoPath) AndAlso String.IsNullOrWhiteSpace(existingVideoUrl), String.Empty, " + video")
    End Sub

    Private Async Sub BtnPublish_Click(sender As Object, e As RoutedEventArgs)
        If Not ValidatePublishFields() Then Return

        BtnPublish.IsEnabled = False
        BtnBack.IsEnabled = False
        BtnContinue.IsEnabled = False
        OperationProgress.Visibility = Visibility.Visible
        Try
            Dim imageUrls As New List(Of String)(existingImageUrls)
            If imagePaths.Count > 0 Then
                TxtSaveProgress.Text = "Uploading " & imagePaths.Count.ToString() & " photo(s)..."
                If AppSession.Mode = StoreMode.Live Then
                    imageUrls.AddRange(Await SupabaseService.UploadProductImagesAsync(imagePaths, TxtProductName.Text.Trim()))
                Else
                    imageUrls.AddRange(CopyDemoImages())
                End If
            End If

            Dim videoUrl = existingVideoUrl
            Dim videoKey = existingVideoKey
            If Not String.IsNullOrWhiteSpace(selectedVideoPath) Then
                If AppSession.Mode = StoreMode.Live Then
                    TxtSaveProgress.Text = "Uploading product video..."
                    Dim videoUpload = Await SupabaseService.UploadProductVideoAsync(selectedVideoPath, TxtProductName.Text.Trim())
                    videoUrl = videoUpload.PublicUrl
                    videoKey = videoUpload.Key
                Else
                    videoUrl = CopyDemoVideo()
                    videoKey = Nothing
                End If
            End If

            BuildProduct(imageUrls, videoUrl, videoKey)
            TxtSaveProgress.Text = "Saving product..."
            Await SupabaseService.SaveProductAsync(NewProduct)
            If AppSession.Mode = StoreMode.Live AndAlso Not String.IsNullOrWhiteSpace(NewProduct.r2_video_key) Then
                Try
                    Await SupabaseService.EnqueueProductVideoTranscodeAsync(NewProduct.id, NewProduct.r2_video_key)
                Catch
                End Try
            End If
            DialogResult = True
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Product could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            BtnPublish.IsEnabled = True
            BtnBack.IsEnabled = True
            BtnContinue.IsEnabled = True
            TxtSaveProgress.Text = String.Empty
            OperationProgress.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Function ValidatePublishFields() As Boolean
        If String.IsNullOrWhiteSpace(TxtProductName.Text) Then
            MessageBox.Show("Enter a product title.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            ShowStep(2)
            Return False
        End If
        Dim price As Decimal
        If Not Decimal.TryParse(TxtPrice.Text, price) OrElse price <= 0D Then
            MessageBox.Show("Enter a selling price greater than zero.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            ShowStep(2)
            Return False
        End If
        If CmbCategory.SelectedItem Is Nothing Then
            MessageBox.Show("Choose a product category.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            ShowStep(2)
            Return False
        End If
        Dim quantity As Integer
        If Not CBool(ChkPreorder.IsChecked) AndAlso (Not Integer.TryParse(TxtQuantity.Text, quantity) OrElse quantity < 0) Then
            MessageBox.Show("Enter a non-negative quantity or mark the product as preorder.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            ShowStep(3)
            Return False
        End If
        Dim discount As Decimal
        If Not Decimal.TryParse(If(String.IsNullOrWhiteSpace(TxtDiscount.Text), "0", TxtDiscount.Text), discount) OrElse discount < 0D OrElse discount > 100D Then
            MessageBox.Show("Discount must be between 0 and 100 percent.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            ShowStep(3)
            Return False
        End If
        If imagePaths.Count + existingImageUrls.Count = 0 Then
            MessageBox.Show("Choose at least one product photo.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            ShowStep(1)
            Return False
        End If
        Return ValidateAllNumericFields()
    End Function

    Private Function ValidateAllNumericFields() As Boolean
        Dim numericValue As Decimal
        For Each fieldInput In {TxtShippingFee, TxtCostPrice, TxtCompareAtPrice, TxtWeight}
            If Not String.IsNullOrWhiteSpace(fieldInput.Text) AndAlso (Not Decimal.TryParse(fieldInput.Text, numericValue) OrElse numericValue < 0D) Then
                MessageBox.Show("Enter a valid non-negative value for " & CStr(fieldInput.Tag) & ".", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
                fieldInput.Focus()
                Return False
            End If
        Next
        Return True
    End Function

    Private Function CopyDemoImages() As List(Of String)
        Dim savedPaths As New List(Of String)()
        Dim imageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tagitShop", "images")
        Directory.CreateDirectory(imageFolder)
        For Each sourcePath In imagePaths
            Dim extension = Path.GetExtension(sourcePath)
            Dim targetPath = Path.Combine(imageFolder, Guid.NewGuid().ToString("N") & extension)
            File.Copy(sourcePath, targetPath, False)
            savedPaths.Add(targetPath)
        Next
        Return savedPaths
    End Function

    Private Function CopyDemoVideo() As String
        Dim mediaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tagitShop", "videos")
        Directory.CreateDirectory(mediaFolder)
        Dim targetPath = Path.Combine(mediaFolder, Guid.NewGuid().ToString("N") & Path.GetExtension(selectedVideoPath))
        File.Copy(selectedVideoPath, targetPath, False)
        Return targetPath
    End Function

    Private Sub BuildProduct(imageUrls As List(Of String), videoUrl As String, videoKey As String)
        Dim price As Decimal
        Decimal.TryParse(TxtPrice.Text, price)
        Dim shippingFee As Decimal = 0D
        Decimal.TryParse(TxtShippingFee.Text, shippingFee)
        Dim costPrice As Decimal = 0D
        Decimal.TryParse(TxtCostPrice.Text, costPrice)
        Dim discount As Decimal = 0D
        Decimal.TryParse(TxtDiscount.Text, discount)
        Dim compareAtPrice As Decimal?
        Dim compareAtValue As Decimal
        If Decimal.TryParse(TxtCompareAtPrice.Text, compareAtValue) Then compareAtPrice = compareAtValue
        Dim weight As Decimal?
        Dim weightValue As Decimal
        If Decimal.TryParse(TxtWeight.Text, weightValue) Then weight = weightValue
        Dim quantity As Integer = 0
        Integer.TryParse(TxtQuantity.Text, quantity)
        Dim category = TryCast(CmbCategory.SelectedItem, SupabaseService.ProductCategory)
        Dim badges As New List(Of String)()
        If shippingFee = 0D Then badges.Add("free_shipping")
        If Not CBool(ChkPreorder.IsChecked) AndAlso quantity > 0 Then badges.Add("limited_stock")

        Dim specifications As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each editor In specificationEditors
            Dim key = editor.KeyInput.Text.Trim()
            Dim value = editor.ValueInput.Text.Trim()
            If Not String.IsNullOrWhiteSpace(key) AndAlso Not String.IsNullOrWhiteSpace(value) Then specifications(key) = value
        Next
        Dim tags = TxtTags.Text.Split(","c).
            Select(Function(tag) tag.Trim()).
            Where(Function(tag) Not String.IsNullOrWhiteSpace(tag)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            Take(10).
            ToList()
        Dim sizes = SelectedOptionValues(SizesPanel)
        Dim colors = SelectedOptionValues(ColorsPanel)
        Dim weightUnit = If(TryCast(CmbWeightUnit.SelectedItem, ComboBoxItem)?.Content?.ToString(), "kg")

        NewProduct = New LocalData.ProductItem() With {
            .id = If(sourceProduct Is Nothing, If(AppSession.Mode = StoreMode.Demo, Guid.NewGuid().ToString("N"), String.Empty), sourceProduct.id),
            .title = TxtProductName.Text.Trim(), .price = price, .shipping_fee = shippingFee,
            .category = category.Name, .category_id = category.Id, .description = TxtDescription.Text.Trim(),
            .cost_price = costPrice, .compare_at_price = compareAtPrice, .discount = CInt(discount),
            .quantity = quantity, .reorder_level = If(sourceProduct Is Nothing, 0, sourceProduct.reorder_level),
            .thumbnail = If(imageUrls.Count > 0, imageUrls(0), Nothing), .thumbnails = imageUrls,
            .video_url = videoUrl, .r2_video_key = videoKey,
            .sizes = sizes, .colors = colors, .badges = badges, .tags = tags, .specifications = specifications,
            .track_inventory = CBool(ChkTrackInventory.IsChecked), .allow_backorder = CBool(ChkAllowBackorder.IsChecked),
            .is_preorder = CBool(ChkPreorder.IsChecked), .sku = TxtSku.Text.Trim(), .barcode = TxtBarcode.Text.Trim(),
            .vendor = TxtVendor.Text.Trim(), .weight = weight, .weight_unit = weightUnit, .slug = If(sourceProduct Is Nothing, Nothing, sourceProduct.slug),
            .seller_id = If(AppSession.Mode = StoreMode.Live, AppSession.SellerId, "local"),
            .status = "active", .updated_at = DateTime.Now
        }
    End Sub

    Private Function SelectedOptionValues(panel As WrapPanel) As List(Of String)
        Return panel.Children.OfType(Of CheckBox)().
            Where(Function(optionCheck) optionCheck.IsChecked = True).
            Select(Function(optionCheck) CStr(optionCheck.Tag)).
            ToList()
    End Function

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class