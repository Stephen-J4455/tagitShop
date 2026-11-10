Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading.Tasks
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions

Public NotInheritable Class SupabaseService
    Private Const SupabaseUrl As String = "https://meiljgoztnhnyvtfkzuh.supabase.co"
    Private Const SupabaseAnonKey As String = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im1laWxqZ296dG5obnl2dGZrenVoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjUxMTI0OTksImV4cCI6MjA4MDY4ODQ5OX0.X7zve3MSvaoplAHl45BpC57h9G4IY5suhBBteIoEU3I"
    Private Shared ReadOnly Client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(60)}
    Private Const MaxRetries As Integer = 3
    Private Shared ReadOnly JsonOptions As New JsonSerializerOptions() With {.PropertyNameCaseInsensitive = True}

    Private Sub New()
    End Sub

    Public Shared Async Function SignInAsync(email As String, password As String) As Task
        Dim authPayload = JsonSerializer.Serialize(New With {.email = email, .password = password})
        Dim authResponse = Await SendAsync(HttpMethod.Post, "/auth/v1/token?grant_type=password", authPayload, False)

        Using authJson = JsonDocument.Parse(authResponse)
            Dim root = authJson.RootElement
            AppSession.UserId = root.GetProperty("user").GetProperty("id").GetString()
            AppSession.AccessToken = root.GetProperty("access_token").GetString()
            AppSession.RefreshToken = root.GetProperty("refresh_token").GetString()
        End Using

        Dim sellerPath = "/rest/v1/express_sellers?user_id=eq." & Uri.EscapeDataString(AppSession.UserId) & "&select=id,name&limit=1"
        Dim sellerResponse = Await SendAsync(HttpMethod.Get, sellerPath, Nothing, True)
        Dim sellers = JsonSerializer.Deserialize(Of List(Of SellerResponse))(sellerResponse, JsonOptions)

        If sellers Is Nothing OrElse sellers.Count = 0 Then
            AppSession.ClearLiveSession()
            Throw New InvalidOperationException("This account does not have an ExpressMart store linked to it.")
        End If

        AppSession.Mode = StoreMode.Live
        AppSession.SellerId = sellers(0).Id
        AppSession.StoreName = sellers(0).Name
        AppSession.SaveSession()
    End Function

    Public Shared Async Function SignOutAsync() As Task
        If Not String.IsNullOrWhiteSpace(AppSession.AccessToken) Then
            Try
                Await SendAsync(HttpMethod.Post, "/auth/v1/logout", "{}", True)
            Catch
            End Try
        End If
        AppSession.ClearLiveSession()
    End Function

    Private Shared Async Function RefreshSessionAsync() As Task
        If String.IsNullOrWhiteSpace(AppSession.RefreshToken) Then Throw New InvalidOperationException("Your session has expired. Sign in again to continue.")
        Dim payload = JsonSerializer.Serialize(New With {.refresh_token = AppSession.RefreshToken})
        Dim refreshed = Await SendAsync(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token", payload, False, Nothing, False)
        Using json = JsonDocument.Parse(refreshed)
            AppSession.AccessToken = json.RootElement.GetProperty("access_token").GetString()
            AppSession.RefreshToken = json.RootElement.GetProperty("refresh_token").GetString()
        End Using
    End Function

    Public Shared Async Function LoadProductsAsync() As Task(Of List(Of LocalData.ProductItem))
        If AppSession.Mode = StoreMode.Demo Then Return LocalData.Load().Products

        Dim responses = Await LoadRemoteProductsAsync()
        If responses.Count = 0 AndAlso Not String.IsNullOrWhiteSpace(AppSession.SellerId) Then
            responses = Await LoadAllProductsAsync()
        End If

        Dim products As New List(Of LocalData.ProductItem)()
        For Each remote In responses
            If Not String.IsNullOrWhiteSpace(AppSession.SellerId) AndAlso Not String.IsNullOrWhiteSpace(remote.seller_id) AndAlso remote.seller_id <> AppSession.SellerId Then
                Continue For
            End If

            products.Add(New LocalData.ProductItem() With {
                .id = remote.id, .title = remote.title, .price = remote.price, .cost_price = If(remote.cost_price, 0D),
                .quantity = If(remote.quantity, 0), .reorder_level = If(remote.reorder_level, 0), .sku = remote.sku, .barcode = remote.barcode,
                .sold_count = If(remote.sold_count, 0),
                .category = remote.category, .status = remote.status,
                .thumbnail = If(Not String.IsNullOrWhiteSpace(remote.thumbnail), remote.thumbnail, If(remote.thumbnails IsNot Nothing AndAlso remote.thumbnails.Count > 0, remote.thumbnails(0), Nothing)),
                .thumbnails = If(remote.thumbnails, New List(Of String)()), .discount = CInt(If(remote.discount, 0D)),
                .video_url = remote.video_url, .r2_video_key = remote.r2_video_key,
                .seller_id = If(String.IsNullOrWhiteSpace(remote.seller_id), AppSession.SellerId, remote.seller_id),
                .category_id = remote.category_id, .description = remote.description,
                .shipping_fee = If(remote.shipping_fee, 0D), .compare_at_price = remote.compare_at_price,
                .sizes = If(remote.sizes, New List(Of String)()), .colors = If(remote.colors, New List(Of String)()),
                .badges = If(remote.badges, New List(Of String)()), .tags = If(remote.tags, New List(Of String)()),
                .specifications = If(remote.specifications, New Dictionary(Of String, String)()),
                .track_inventory = If(remote.track_inventory, True), .allow_backorder = If(remote.allow_backorder, False),
                .is_preorder = If(remote.is_preorder, False), .vendor = remote.vendor, .weight = remote.weight,
                .weight_unit = If(String.IsNullOrWhiteSpace(remote.weight_unit), "kg", remote.weight_unit), .slug = remote.slug
            })
        Next

        Return products
    End Function

    Private Shared Async Function LoadRemoteProductsAsync() As Task(Of List(Of RemoteProduct))
        If String.IsNullOrWhiteSpace(AppSession.SellerId) Then
            Return Await LoadAllProductsAsync()
        End If

        Dim sellerScopedPath = "/rest/v1/express_products?seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
                "&select=id,title,price,cost_price,quantity,sold_count,sku,barcode,category,category_id,description,shipping_fee,compare_at_price,status,thumbnail,thumbnails,video_url,r2_video_key,discount,sizes,colors,badges,tags,specifications,track_inventory,allow_backorder,is_preorder,vendor,weight,weight_unit,slug,seller_id&order=title.asc"

        Try
            Dim response = Await SendAsync(HttpMethod.Get, sellerScopedPath, Nothing, True)
            Dim remoteProducts = JsonSerializer.Deserialize(Of List(Of RemoteProduct))(response, JsonOptions)
            Return If(remoteProducts, New List(Of RemoteProduct)())
        Catch
            Return New List(Of RemoteProduct)()
        End Try
    End Function

    Private Shared Async Function LoadAllProductsAsync() As Task(Of List(Of RemoteProduct))
        Dim allPath = "/rest/v1/express_products?select=id,title,price,cost_price,quantity,sold_count,sku,barcode,category,category_id,description,shipping_fee,compare_at_price,status,thumbnail,thumbnails,video_url,r2_video_key,discount,sizes,colors,badges,tags,specifications,track_inventory,allow_backorder,is_preorder,vendor,weight,weight_unit,slug,seller_id&order=title.asc&limit=1000"
        Dim response = Await SendAsync(HttpMethod.Get, allPath, Nothing, True)
        Dim remoteProducts = JsonSerializer.Deserialize(Of List(Of RemoteProduct))(response, JsonOptions)
        Return If(remoteProducts, New List(Of RemoteProduct)())
    End Function

    Public Shared Async Function LoadProductCategoriesAsync() As Task(Of List(Of ProductCategory))
        Dim categories As New List(Of ProductCategory)()
        If AppSession.Mode = StoreMode.Live Then
            Try
                Dim response = Await SendAsync(HttpMethod.Get, "/rest/v1/express_categories?select=id,name&order=name.asc", Nothing, True)
                Using json = JsonDocument.Parse(response)
                    For Each row In json.RootElement.EnumerateArray()
                        Dim idValue As JsonElement
                        Dim nameValue As JsonElement
                        categories.Add(New ProductCategory() With {
                            .Id = If(row.TryGetProperty("id", idValue), idValue.GetString(), Nothing),
                            .Name = If(row.TryGetProperty("name", nameValue), nameValue.GetString(), Nothing)
                        })
                    Next
                End Using
            Catch
                categories.Clear()
            End Try
        End If

        If categories.Count = 0 Then
            Dim fallbackNames = If(AppSession.Mode = StoreMode.Demo,
                {"Fashion", "Grocery", "Beauty", "Electronics", "Home", "Produce", "Bakery", "Dairy", "Beverages", "Snacks", "Household", "Other"},
                {"Fashion", "Grocery", "Beauty", "Electronics", "Home"})
            For Each name In fallbackNames
                categories.Add(New ProductCategory() With {.Id = Nothing, .Name = name})
            Next
        End If
        Return categories
    End Function

    Public Shared Async Function UploadProductImagesAsync(filePaths As IEnumerable(Of String), productTitle As String) As Task(Of List(Of String))
        Dim uploadedUrls As New List(Of String)()
        Dim titleSegment = SafeStorageSegment(productTitle, "product")
        Dim storeSegment = SafeStorageSegment(AppSession.StoreName, "store") & "-" & If(String.IsNullOrWhiteSpace(AppSession.SellerId), "unknown", AppSession.SellerId)
        Dim folder = "express-products/products/" & storeSegment

        Using uploadClient As New HttpClient() With {.Timeout = TimeSpan.FromMinutes(2)}
            For Each filePath In filePaths
                Dim extension = Path.GetExtension(filePath).TrimStart("."c).ToLowerInvariant()
                Dim contentType = ImageContentType(extension)
                Dim fileName = titleSegment & "-" & Guid.NewGuid().ToString("N") & "." & extension
                Dim presignPayload = JsonSerializer.Serialize(New With {
                    .fileName = fileName,
                    .fileType = contentType,
                    .folder = folder
                })
                Dim presignResponse = Await SendAsync(HttpMethod.Post, "/functions/v1/get-r2-upload-url", presignPayload, True)
                Dim uploadUrl As String
                Dim publicUrl As String
                Using presignJson = JsonDocument.Parse(presignResponse)
                    uploadUrl = presignJson.RootElement.GetProperty("uploadUrl").GetString()
                    publicUrl = presignJson.RootElement.GetProperty("publicUrl").GetString()
                End Using

                Dim bytes = Await File.ReadAllBytesAsync(filePath)
                Using request As New HttpRequestMessage(HttpMethod.Put, uploadUrl)
                    request.Content = New ByteArrayContent(bytes)
                    request.Content.Headers.ContentType = New MediaTypeHeaderValue(contentType)
                    Using response = Await uploadClient.SendAsync(request)
                        If Not response.IsSuccessStatusCode Then
                            Throw New InvalidOperationException("Image upload failed (" & CInt(response.StatusCode).ToString() & ").")
                        End If
                    End Using
                End Using
                uploadedUrls.Add(publicUrl)
            Next
        End Using

        Return uploadedUrls
    End Function

    Public Shared Async Function UploadProductVideoAsync(filePath As String, productTitle As String) As Task(Of ProductMediaUpload)
        Dim extension = Path.GetExtension(filePath).TrimStart("."c).ToLowerInvariant()
        Dim contentType = If(extension = "mov", "video/quicktime", "video/mp4")
        Dim titleSegment = SafeStorageSegment(productTitle, "product")
        Dim storeSegment = SafeStorageSegment(AppSession.StoreName, "store") & "-" & If(String.IsNullOrWhiteSpace(AppSession.SellerId), "unknown", AppSession.SellerId)
        Dim fileName = titleSegment & "-" & Guid.NewGuid().ToString("N") & "." & extension
        Dim presignPayload = JsonSerializer.Serialize(New With {
            .fileName = fileName,
            .fileType = contentType,
            .folder = "products/" & storeSegment
        })
        Dim presignResponse = Await SendAsync(HttpMethod.Post, "/functions/v1/get-r2-upload-url", presignPayload, True)
        Dim uploadUrl As String
        Dim publicUrl As String
        Dim key As String
        Using presignJson = JsonDocument.Parse(presignResponse)
            uploadUrl = presignJson.RootElement.GetProperty("uploadUrl").GetString()
            publicUrl = presignJson.RootElement.GetProperty("publicUrl").GetString()
            key = presignJson.RootElement.GetProperty("key").GetString()
        End Using

        Dim bytes = Await File.ReadAllBytesAsync(filePath)
        Using uploadClient As New HttpClient() With {.Timeout = TimeSpan.FromMinutes(2)}
            Using request As New HttpRequestMessage(HttpMethod.Put, uploadUrl)
                request.Content = New ByteArrayContent(bytes)
                request.Content.Headers.ContentType = New MediaTypeHeaderValue(contentType)
                Using response = Await uploadClient.SendAsync(request)
                    If Not response.IsSuccessStatusCode Then
                        Throw New InvalidOperationException("Video upload failed (" & CInt(response.StatusCode).ToString() & ").")
                    End If
                End Using
            End Using
        End Using
        Return New ProductMediaUpload() With {.PublicUrl = publicUrl, .Key = key}
    End Function

    Public Shared Async Function GenerateProductDraftAsync(image As String, category As String, categories As IEnumerable(Of String), price As Decimal?, quantity As Integer?) As Task(Of ProductDraft)
        Dim payload = JsonSerializer.Serialize(New With {
            .image = image,
            .category = If(String.IsNullOrWhiteSpace(category), Nothing, category),
            .categories = categories.Where(Function(name) Not String.IsNullOrWhiteSpace(name)).ToList(),
            .price = price,
            .quantity = quantity
        })
        Dim lastError As Exception = Nothing
        For attempt = 1 To 3
            Dim shouldRetry = False
            Try
                Dim response = Await SendAsync(HttpMethod.Post, "/functions/v1/product-vision", payload, True)
                Dim result = JsonSerializer.Deserialize(Of ProductVisionResponse)(response, JsonOptions)
                If result Is Nothing OrElse Not result.success OrElse result.product Is Nothing Then
                    Throw New InvalidOperationException(If(result?.ErrorMessage, "Product details could not be generated."))
                End If
                If String.IsNullOrWhiteSpace(result.product.title) OrElse
                    String.IsNullOrWhiteSpace(result.product.description) OrElse
                    String.IsNullOrWhiteSpace(result.product.category) OrElse
                    result.product.tags Is Nothing OrElse
                    result.product.specifications Is Nothing Then
                    Throw New InvalidOperationException("AI returned an incomplete or invalid draft.")
                End If
                Return result.product
            Catch ex As Exception
                lastError = ex
                Dim isInvalidDraft = ex.Message.Contains("invalid product data", StringComparison.OrdinalIgnoreCase) OrElse
                    ex.Message.Contains("incomplete or invalid draft", StringComparison.OrdinalIgnoreCase)
                If Not isInvalidDraft OrElse attempt = 3 Then Throw
                shouldRetry = True
            End Try
            If shouldRetry Then Await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)))
        Next
        Throw lastError
    End Function

    Public Shared Async Function AskDesktopAssistantAsync(message As String,
                                                           currentPage As String,
                                                           desktopGuide As String,
                                                           recordContext As String,
                                                           history As IEnumerable(Of String)) As Task(Of String)
        If AppSession.Mode <> StoreMode.Live Then
            Throw New InvalidOperationException("Online TagAI is available after signing in to a live store.")
        End If

        Dim payload = JsonSerializer.Serialize(New With {
            .mode = "desktop_assistant",
            .message = If(message, String.Empty),
            .current_page = If(currentPage, String.Empty),
            .desktop_guide = If(desktopGuide, String.Empty),
            .record_context = If(recordContext, String.Empty),
            .history = If(history, Enumerable.Empty(Of String)()).
                TakeLast(8).
                Select(Function(text) New With {.role = "user", .text = text}).
                ToList()
        })
        Dim response = Await SendAsync(HttpMethod.Post, "/functions/v1/ai-assistant", payload, True)
        Using json = JsonDocument.Parse(response)
            Dim root = json.RootElement
            Dim success As JsonElement
            If Not root.TryGetProperty("success", success) OrElse success.ValueKind <> JsonValueKind.True Then
                Dim errorMessage As JsonElement
                If root.TryGetProperty("error", errorMessage) AndAlso errorMessage.ValueKind = JsonValueKind.String Then
                    Throw New InvalidOperationException(errorMessage.GetString())
                End If
                Throw New InvalidOperationException("TagAI could not answer this question.")
            End If

            Dim reply As JsonElement
            If Not root.TryGetProperty("reply", reply) OrElse reply.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(reply.GetString()) Then
                Throw New InvalidOperationException("TagAI returned an empty answer.")
            End If
            Return reply.GetString()
        End Using
    End Function

    Public Shared Async Function EnqueueProductVideoTranscodeAsync(productId As String, sourceKey As String) As Task
        Dim payload = JsonSerializer.Serialize(New With {
            .sourceKey = sourceKey,
            .ownerTable = "express_products",
            .ownerId = productId,
            .hlsUrlColumn = "video_hls_url"
        })
        Await SendAsync(HttpMethod.Post, "/functions/v1/transcode-reel", payload, True)
    End Function

    Private Shared Function SafeStorageSegment(value As String, fallback As String) As String
        Dim safeValue = Regex.Replace(If(value, String.Empty).Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim("-"c)
        Return If(String.IsNullOrWhiteSpace(safeValue), fallback, safeValue)
    End Function

    Private Shared Function ImageContentType(extension As String) As String
        Select Case extension
            Case "png"
                Return "image/png"
            Case "webp"
                Return "image/webp"
            Case "gif"
                Return "image/gif"
            Case Else
                Return "image/jpeg"
        End Select
    End Function

    Public Shared Async Function LoadStoreSettingsAsync() As Task(Of LocalData.StoreSettings)
        If AppSession.Mode = StoreMode.Demo Then Return LocalData.Load().Store
        Dim path = "/rest/v1/express_sellers?id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=name,email,description,store_description,theme_color,storefront_theme,storefront_layout,storefront_enabled,avatar,cover_image,cover_image_2,social_website,social_facebook,social_instagram,social_twitter,social_whatsapp&limit=1"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Dim settings = JsonSerializer.Deserialize(Of List(Of RemoteStoreSettings))(response, JsonOptions)
        If settings Is Nothing OrElse settings.Count = 0 Then Throw New InvalidOperationException("The store profile could not be loaded.")
        Return New LocalData.StoreSettings() With {
            .name = settings(0).name,
            .email = settings(0).email,
            .description = If(String.IsNullOrWhiteSpace(settings(0).store_description), settings(0).description, settings(0).store_description),
            .store_description = settings(0).store_description,
            .theme_color = settings(0).theme_color,
            .storefront_theme = settings(0).storefront_theme,
            .storefront_layout = settings(0).storefront_layout,
            .storefront_enabled = settings(0).storefront_enabled,
            .avatar = settings(0).avatar,
            .cover_image = settings(0).cover_image,
            .cover_image_2 = settings(0).cover_image_2,
            .social_website = settings(0).social_website,
            .social_facebook = settings(0).social_facebook,
            .social_instagram = settings(0).social_instagram,
            .social_twitter = settings(0).social_twitter,
            .social_whatsapp = settings(0).social_whatsapp
        }
    End Function

    Public Shared Async Function SaveStoreSettingsAsync(settings As LocalData.StoreSettings) As Task
        If AppSession.Mode = StoreMode.Demo Then
            Dim data = LocalData.Load()
            Dim existingEnabled = data.Store.storefront_enabled
            data.Store = settings
            If Not settings.storefront_enabled.HasValue Then data.Store.storefront_enabled = existingEnabled
            LocalData.Save()
            AppSession.StoreName = settings.name
            Return
        End If

        Dim path = "/rest/v1/express_sellers?id=eq." & Uri.EscapeDataString(AppSession.SellerId)
        Dim payloadFields As New Dictionary(Of String, Object) From {
            {"name", settings.name},
            {"email", settings.email},
            {"description", settings.description},
            {"store_description", settings.store_description},
            {"theme_color", settings.theme_color},
            {"storefront_theme", settings.storefront_theme},
            {"storefront_layout", settings.storefront_layout},
            {"avatar", settings.avatar},
            {"cover_image", settings.cover_image},
            {"cover_image_2", settings.cover_image_2},
            {"social_website", settings.social_website},
            {"social_facebook", settings.social_facebook},
            {"social_instagram", settings.social_instagram},
            {"social_twitter", settings.social_twitter},
            {"social_whatsapp", settings.social_whatsapp}
        }
        If settings.storefront_enabled.HasValue Then payloadFields("storefront_enabled") = settings.storefront_enabled.Value
        Dim payload = JsonSerializer.Serialize(payloadFields)
        Dim saved = Await SendAsync(New HttpMethod("PATCH"), path & "&select=id", payload, True, "return=representation")
        EnsureReturnedRow(saved, "Store profile update")
        AppSession.StoreName = settings.name
    End Function

    Public Shared Async Function SetStorefrontEnabledAsync(enabled As Boolean) As Task
        If AppSession.Mode = StoreMode.Demo Then
            Dim data = LocalData.Load()
            data.Store.storefront_enabled = enabled
            LocalData.Save()
            Return
        End If

        If String.IsNullOrWhiteSpace(AppSession.SellerId) Then
            Throw New InvalidOperationException("Your store session has expired. Sign in again to change storefront visibility.")
        End If

        Dim path = "/rest/v1/express_sellers?id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=id"
        Dim payload = JsonSerializer.Serialize(New With {.storefront_enabled = enabled})
        Dim saved = Await SendAsync(New HttpMethod("PATCH"), path, payload, True, "return=representation")
        EnsureReturnedRow(saved, "Storefront visibility update")
    End Function

    Public Shared Async Function PostSellerStatusAsync(statusText As String, statusType As String) As Task
        If AppSession.Mode = StoreMode.Demo Then
            ' Demo mode: status stays local
            Return
        End If

        Dim payload = JsonSerializer.Serialize(New With {
            .status_text = statusText,
            .seller_id = AppSession.SellerId,
            .status_type = statusType,
            .is_active = True
        })
        Dim saved = Await SendAsync(HttpMethod.Post, "/rest/v1/express_seller_statuses", payload, True, "return=representation")
        EnsureReturnedRow(saved, "Status post")
    End Function

    Public Shared Async Function SaveProductAsync(product As LocalData.ProductItem) As Task
        If AppSession.Mode = StoreMode.Demo Then
            Dim data = LocalData.Load()
            Dim existing = data.Products.Find(Function(item) item.id = product.id)
            product.updated_at = DateTime.Now
            If existing Is Nothing Then
                data.Products.Add(product)
            Else
                Dim index = data.Products.IndexOf(existing)
                data.Products(index) = product
            End If
            LocalData.Save()
            Return
        End If

        Dim payload = JsonSerializer.Serialize(New With {
            .title = product.title,
            .price = product.price,
            .shipping_fee = product.shipping_fee,
            .category = product.category,
            .category_id = If(String.IsNullOrWhiteSpace(product.category_id), Nothing, product.category_id),
            .description = product.description,
            .cost_price = product.cost_price,
            .compare_at_price = product.compare_at_price,
            .discount = product.discount,
            .quantity = product.quantity,
            .thumbnail = product.thumbnail,
            .thumbnails = If(product.thumbnails IsNot Nothing AndAlso product.thumbnails.Count > 0, product.thumbnails, Nothing),
            .video_url = If(String.IsNullOrWhiteSpace(product.video_url), Nothing, product.video_url),
            .r2_video_key = If(String.IsNullOrWhiteSpace(product.r2_video_key), Nothing, product.r2_video_key),
            .sizes = product.sizes,
            .colors = product.colors,
            .badges = product.badges,
            .tags = product.tags,
            .specifications = product.specifications,
            .track_inventory = product.track_inventory,
            .allow_backorder = product.allow_backorder,
            .is_preorder = product.is_preorder,
            .sku = product.sku,
            .barcode = product.barcode,
            .vendor = product.vendor,
            .weight = product.weight,
            .weight_unit = product.weight_unit,
            .slug = If(String.IsNullOrWhiteSpace(product.slug), Nothing, product.slug),
            .status = If(String.IsNullOrWhiteSpace(product.status), "active", product.status),
            .seller_id = AppSession.SellerId
        })

        Dim fallbackWithoutReorder = False

        Try
            If String.IsNullOrWhiteSpace(product.id) Then
                Dim created = Await SendAsync(HttpMethod.Post, "/rest/v1/express_products", payload, True, "return=representation")
                Using createdJson = JsonDocument.Parse(created)
                    If createdJson.RootElement.ValueKind = JsonValueKind.Array AndAlso createdJson.RootElement.GetArrayLength() > 0 Then
                        product.id = createdJson.RootElement(0).GetProperty("id").GetString()
                    Else
                        Throw New InvalidOperationException("The product was not created. Check your store permissions and required fields.")
                    End If
                End Using
            Else
                Dim path = "/rest/v1/express_products?id=eq." & Uri.EscapeDataString(product.id) &
                    "&seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=id"
                Dim saved = Await SendAsync(New HttpMethod("PATCH"), path, payload, True, "return=representation")
                EnsureReturnedRow(saved, "Product update")
            End If
        Catch ex As Exception
            If Not ex.Message.Contains("reorder_level", StringComparison.OrdinalIgnoreCase) Then
                Throw
            End If
            fallbackWithoutReorder = True
        End Try

        If fallbackWithoutReorder Then
            Dim fallbackPayload = JsonSerializer.Serialize(New With {
                .title = product.title,
                .price = product.price,
                .shipping_fee = product.shipping_fee,
                .category = product.category,
                .category_id = If(String.IsNullOrWhiteSpace(product.category_id), Nothing, product.category_id),
                .description = product.description,
                .cost_price = product.cost_price,
                .compare_at_price = product.compare_at_price,
                .discount = product.discount,
                .quantity = product.quantity,
                .thumbnail = product.thumbnail,
                .thumbnails = If(product.thumbnails IsNot Nothing AndAlso product.thumbnails.Count > 0, product.thumbnails, Nothing),
                .video_url = If(String.IsNullOrWhiteSpace(product.video_url), Nothing, product.video_url),
                .r2_video_key = If(String.IsNullOrWhiteSpace(product.r2_video_key), Nothing, product.r2_video_key),
                .sizes = product.sizes,
                .colors = product.colors,
                .badges = product.badges,
                .tags = product.tags,
                .specifications = product.specifications,
                .track_inventory = product.track_inventory,
                .allow_backorder = product.allow_backorder,
                .is_preorder = product.is_preorder,
                .sku = product.sku,
                .barcode = product.barcode,
                .vendor = product.vendor,
                .weight = product.weight,
                .weight_unit = product.weight_unit,
                .slug = If(String.IsNullOrWhiteSpace(product.slug), Nothing, product.slug),
                .status = If(String.IsNullOrWhiteSpace(product.status), "active", product.status),
                .seller_id = AppSession.SellerId
            })

            If String.IsNullOrWhiteSpace(product.id) Then
                Dim created = Await SendAsync(HttpMethod.Post, "/rest/v1/express_products", fallbackPayload, True, "return=representation")
                Using createdJson = JsonDocument.Parse(created)
                    If createdJson.RootElement.ValueKind = JsonValueKind.Array AndAlso createdJson.RootElement.GetArrayLength() > 0 Then
                        product.id = createdJson.RootElement(0).GetProperty("id").GetString()
                    Else
                        Throw New InvalidOperationException("The product was not created. Check your store permissions and required fields.")
                    End If
                End Using
            Else
                Dim path = "/rest/v1/express_products?id=eq." & Uri.EscapeDataString(product.id) &
                    "&seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=id"
                Dim saved = Await SendAsync(New HttpMethod("PATCH"), path, fallbackPayload, True, "return=representation")
                EnsureReturnedRow(saved, "Product update")
            End If
        End If
    End Function

    Public Shared Async Function DeleteProductAsync(productId As String) As Task(Of List(Of String))
        If String.IsNullOrWhiteSpace(productId) Then Throw New InvalidOperationException("Choose a product to delete.")

        If AppSession.Mode = StoreMode.Demo Then
            Dim data = LocalData.Load()
            Dim removed = data.Products.RemoveAll(Function(product) product IsNot Nothing AndAlso product.id = productId)
            If removed = 0 Then Throw New InvalidOperationException("The product no longer exists.")
            data.FlashSales.RemoveAll(Function(sale) sale IsNot Nothing AndAlso sale.product_id = productId)
            LocalData.Save()
            Return New List(Of String)()
        End If

        If String.IsNullOrWhiteSpace(AppSession.SellerId) Then
            Throw New InvalidOperationException("Your store session has expired. Sign in again to delete this product.")
        End If

        Dim encodedProductId = Uri.EscapeDataString(productId)
        Dim clearProductLink = JsonSerializer.Serialize(New With {.product_id = CType(Nothing, String)})
        Dim cleanupTasks As New List(Of Task(Of String)) From {
            TryCleanupProductReferenceAsync(HttpMethod.Delete, "/rest/v1/express_cart_items?product_id=eq." & encodedProductId, Nothing, "Cart items"),
            TryCleanupProductReferenceAsync(HttpMethod.Delete, "/rest/v1/express_wishlists?product_id=eq." & encodedProductId, Nothing, "Wishlist entries"),
            TryCleanupProductReferenceAsync(HttpMethod.Delete, "/rest/v1/express_reviews?product_id=eq." & encodedProductId, Nothing, "Product reviews"),
            TryCleanupProductReferenceAsync(HttpMethod.Delete, "/rest/v1/express_flash_sales?product_id=eq." & encodedProductId, Nothing, "Flash sales"),
            TryCleanupProductReferenceAsync(New HttpMethod("PATCH"), "/rest/v1/express_order_items?product_id=eq." & encodedProductId, clearProductLink, "Order history links"),
            TryCleanupProductReferenceAsync(New HttpMethod("PATCH"), "/rest/v1/reels?product_id=eq." & encodedProductId, clearProductLink, "Reel links")
        }
        Dim cleanupResults = Await Task.WhenAll(cleanupTasks)
        Dim warnings = cleanupResults.Where(Function(result) Not String.IsNullOrWhiteSpace(result)).ToList()

        Dim deletePath = "/rest/v1/express_products?id=eq." & encodedProductId &
            "&seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=id"
        Dim deletedRows = Await SendAsync(HttpMethod.Delete, deletePath, Nothing, True, "return=representation")
        EnsureReturnedRow(deletedRows, "Product deletion")
        Return warnings
    End Function

    Private Shared Async Function TryCleanupProductReferenceAsync(method As HttpMethod, path As String, body As String, label As String) As Task(Of String)
        Try
            Await SendAsync(method, path, body, True)
            Return Nothing
        Catch ex As Exception
            Return label & " could not be cleaned up: " & ex.Message
        End Try
    End Function

    Public Shared Async Function LoadFlashSalesAsync() As Task(Of List(Of FlashSaleItem))
        If AppSession.Mode = StoreMode.Demo Then
            Dim demoData = LocalData.Load()
            Return demoData.FlashSales.Select(Function(s)
                                                  Dim product = demoData.Products.FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.id = s.product_id)
                                                  Return New FlashSaleItem() With {
                                                      .id = s.id,
                                                      .title = If(product Is Nothing, "Product", product.title),
                                                      .image_url = If(product Is Nothing, Nothing, product.image),
                                                      .discount = 0,
                                                      .flash_price = s.flash_price,
                                                      .original_price = s.original_price,
                                                      .start_time = s.start_time.ToString("O"),
                                                      .end_time = s.end_time.ToString("O"),
                                                      .max_quantity = s.max_quantity,
                                                      .sold_quantity = s.sold_quantity,
                                                      .is_active = s.is_active,
                                                      .created_at = s.created_at.ToString("O")
                                                  }
                                              End Function).ToList()
        End If

        Dim path = "/rest/v1/express_flash_sales?seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
            "&select=id,product_id,flash_price,original_price,discount_percentage,start_time,end_time,max_quantity,sold_quantity,is_active,created_at,product:express_products(title,thumbnail,thumbnails)&order=created_at.desc&limit=100"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Dim remoteSales = JsonSerializer.Deserialize(Of List(Of RemoteFlashSale))(response, JsonOptions)
        Dim sales As New List(Of FlashSaleItem)()
        For Each remote In If(remoteSales, New List(Of RemoteFlashSale)())
            Dim productName = "Product"
            Dim productImage As String = Nothing
            If remote.product.ValueKind <> JsonValueKind.Undefined AndAlso remote.product.TryGetProperty("title", Nothing) Then
                productName = remote.product.GetProperty("title").GetString()
            End If
            If remote.product.ValueKind = JsonValueKind.Object Then
                Dim thumbnail As JsonElement
                If remote.product.TryGetProperty("thumbnail", thumbnail) AndAlso thumbnail.ValueKind = JsonValueKind.String Then
                    productImage = thumbnail.GetString()
                End If
                If String.IsNullOrWhiteSpace(productImage) Then
                    Dim thumbnails As JsonElement
                    If remote.product.TryGetProperty("thumbnails", thumbnails) AndAlso
                       thumbnails.ValueKind = JsonValueKind.Array AndAlso
                       thumbnails.GetArrayLength() > 0 AndAlso
                       thumbnails(0).ValueKind = JsonValueKind.String Then
                        productImage = thumbnails(0).GetString()
                    End If
                End If
            End If
            Dim discountPct = If(remote.original_price > 0, CInt(Math.Round((remote.original_price - remote.flash_price) / remote.original_price * 100)), 0)
            sales.Add(New FlashSaleItem() With {
                .id = remote.id,
                .title = productName,
                .image_url = productImage,
                .discount = discountPct,
                .flash_price = remote.flash_price,
                .original_price = remote.original_price,
                .start_time = remote.start_time,
                .end_time = remote.end_time,
                .max_quantity = remote.max_quantity,
                .sold_quantity = remote.sold_quantity,
                .is_active = remote.is_active,
                .created_at = remote.created_at
            })
        Next
        Return sales
    End Function

    Public Shared Async Function CreateFlashSaleAsync(productId As String, flashPrice As Decimal, startTime As DateTime, endTime As DateTime, maxQuantity As Integer?) As Task
        If String.IsNullOrWhiteSpace(productId) Then Throw New InvalidOperationException("Choose a product before creating the flash sale.")
        If flashPrice <= 0D Then Throw New InvalidOperationException("Flash price must be greater than zero.")
        If endTime <= startTime Then Throw New InvalidOperationException("End time must be after start time.")
        If maxQuantity.HasValue AndAlso maxQuantity.Value <= 0 Then Throw New InvalidOperationException("Max quantity must be greater than zero.")

        Dim currentOriginalPrice As Decimal
        Dim demoData As LocalData.AppData = Nothing
        If AppSession.Mode = StoreMode.Demo Then
            demoData = LocalData.Load()
            Dim product = demoData.Products.FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.id = productId)
            If product Is Nothing OrElse Not String.Equals(product.status, "active", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("The selected product is no longer active. Choose an active product and try again.")
            End If
            currentOriginalPrice = product.price
        Else
            If String.IsNullOrWhiteSpace(AppSession.SellerId) Then
                Throw New InvalidOperationException("Your store session has expired. Sign in again to continue.")
            End If

            Dim productPath = "/rest/v1/express_products?id=eq." & Uri.EscapeDataString(productId) &
                "&seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
                "&select=id,title,price,status&limit=1"
            Dim productResponse = Await SendAsync(HttpMethod.Get, productPath, Nothing, True)
            Dim currentProducts = JsonSerializer.Deserialize(Of List(Of RemoteProduct))(productResponse, JsonOptions)
            Dim currentProduct = If(currentProducts, New List(Of RemoteProduct)()).FirstOrDefault()
            If currentProduct Is Nothing OrElse Not String.Equals(currentProduct.status, "active", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("The selected product is no longer active in your store. Choose an active product and try again.")
            End If
            currentOriginalPrice = currentProduct.price
        End If

        If currentOriginalPrice <= 0D Then
            Throw New InvalidOperationException("The selected product needs a price greater than zero.")
        End If
        If flashPrice >= currentOriginalPrice Then
            Throw New InvalidOperationException("The flash price must be lower than the product's current price of GHC " &
                currentOriginalPrice.ToString("N2", System.Globalization.CultureInfo.CurrentCulture) & ". Update the flash price and try again.")
        End If

        If AppSession.Mode = StoreMode.Demo Then
            Dim sale = New LocalData.FlashSaleRecord() With {
                .id = Guid.NewGuid().ToString("N"),
                .product_id = productId,
                .flash_price = flashPrice,
                .original_price = currentOriginalPrice,
                .start_time = startTime,
                .end_time = endTime,
                .max_quantity = maxQuantity,
                .is_active = True,
                .created_at = DateTime.Now
            }
            demoData.FlashSales.Add(sale)
            LocalData.Save()
            Return
        End If

        Dim discountPct = (currentOriginalPrice - flashPrice) / currentOriginalPrice * 100D
        Dim payload = JsonSerializer.Serialize(New With {
            .product_id = productId,
            .seller_id = AppSession.SellerId,
            .flash_price = flashPrice,
            .original_price = currentOriginalPrice,
            .discount_percentage = discountPct,
            .start_time = startTime.ToString("o"),
            .end_time = endTime.ToString("o"),
            .max_quantity = If(maxQuantity.HasValue, maxQuantity.Value, Nothing),
            .is_active = True
        })
        Await SendAsync(HttpMethod.Post, "/rest/v1/express_flash_sales", payload, True, "return=representation")
    End Function

    Public Shared Async Function LoadCouponsAsync() As Task(Of List(Of CouponItem))
        If AppSession.Mode = StoreMode.Demo Then
            Return LocalData.Load().Coupons.Select(Function(c) New CouponItem() With {
                .id = c.id, .code = c.code, .description = c.description,
                .discount = CInt(c.discount_value), .discount_type = c.discount_type,
                .status = If(c.is_active, "Active", "Expired"), .uses = c.usage_count,
                .valid_from = c.created_at.ToString("O"), .valid_until = If(c.valid_until.HasValue, c.valid_until.Value.ToString("O"), String.Empty)
            }).ToList()
        End If

        Dim path = "/rest/v1/express_coupons?seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
            "&select=id,code,description,discount_type,discount_value,min_order_amount,max_discount_amount,usage_limit,usage_count,valid_from,valid_until,is_active,created_at&order=created_at.desc&limit=100"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Dim remoteCoupons = JsonSerializer.Deserialize(Of List(Of RemoteCoupon))(response, JsonOptions)
        Dim coupons As New List(Of CouponItem)()
        For Each remote In If(remoteCoupons, New List(Of RemoteCoupon)())
            Dim discountVal = If(remote.discount_type = "percentage", remote.discount_value, remote.discount_value)
            Dim status = If(remote.is_active, "Active", "Expired")
            coupons.Add(New CouponItem() With {
                .id = remote.id,
                .code = remote.code,
                .description = remote.description,
                .discount = CInt(discountVal),
                .discount_type = remote.discount_type,
                .status = status,
                .uses = remote.usage_count,
                .valid_from = remote.valid_from,
                .valid_until = remote.valid_until
            })
        Next
        Return coupons
    End Function

    Public Shared Async Function CreateCouponAsync(code As String, description As String, discountType As String, discountValue As Decimal, validUntil As DateTime?) As Task
        If AppSession.Mode = StoreMode.Demo Then
            Dim data = LocalData.Load()
            Dim coupon = New LocalData.CouponRecord() With {
                .id = Guid.NewGuid().ToString("N"),
                .code = code,
                .description = description,
                .discount_type = discountType,
                .discount_value = discountValue,
                .valid_until = validUntil,
                .is_active = True,
                .created_at = DateTime.Now
            }
            data.Coupons.Add(coupon)
            LocalData.Save()
            Return
        End If

        Dim payload = JsonSerializer.Serialize(New With {
            .code = code,
            .seller_id = AppSession.SellerId,
            .description = description,
            .discount_type = discountType,
            .discount_value = discountValue,
            .valid_until = If(validUntil.HasValue, validUntil.Value.ToString("o"), Nothing),
            .is_active = True
        })
        Await SendAsync(HttpMethod.Post, "/rest/v1/express_coupons", payload, True, "return=representation")
    End Function

    Public Shared Async Function LoadReelsAsync() As Task(Of List(Of ReelItem))
        If AppSession.Mode = StoreMode.Demo Then
            Return LocalData.Load().Reels.Select(Function(r) New ReelItem() With {
                .id = r.id, .title = r.title, .video_url = r.video_url,
                .thumbnail_url = r.thumbnail_url, .views = r.views,
                .likes = r.likes, .created_at = r.created_at.ToString("O")
            }).ToList()
        End If

        Dim path = "/rest/v1/reels?seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
            "&select=id,video_url,r2_key,thumbnail_url,title,description,price,category,tags,views,likes,is_active,created_at&order=created_at.desc&limit=100"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Dim remoteReels = JsonSerializer.Deserialize(Of List(Of RemoteReel))(response, JsonOptions)
        Dim reels As New List(Of ReelItem)()
        For Each remote In If(remoteReels, New List(Of RemoteReel)())
            reels.Add(New ReelItem() With {
                .id = remote.id,
                .title = remote.title,
                .video_url = remote.video_url,
                .thumbnail_url = remote.thumbnail_url,
                .views = remote.views,
                .likes = remote.likes,
                .created_at = remote.created_at
            })
        Next
        Return reels
    End Function

    Public Shared Async Function UpdateProductQuantityAsync(productId As String, quantity As Integer) As Task
        If quantity < 0 Then Throw New ArgumentOutOfRangeException(NameOf(quantity), "Quantity cannot be negative.")
        If AppSession.Mode = StoreMode.Demo Then
            Dim product = LocalData.Load().Products.Find(Function(item) item.id = productId)
            If product Is Nothing Then Throw New InvalidOperationException("The selected product no longer exists.")
            product.quantity = quantity
            product.updated_at = DateTime.Now
            LocalData.Save()
            Return
        End If

        Dim path = "/rest/v1/express_products?id=eq." & Uri.EscapeDataString(productId) &
            "&seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=id"
        Dim updated = Await SendAsync(New HttpMethod("PATCH"), path, JsonSerializer.Serialize(New With {.quantity = quantity}), True, "return=representation")
        EnsureReturnedRow(updated, "Inventory adjustment")
    End Function

    Public Shared Async Function LoadOrdersAsync() As Task(Of List(Of LocalData.OrderItem))
        If AppSession.Mode = StoreMode.Demo Then Return LocalData.Load().Orders

        Dim path = "/rest/v1/express_orders?seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
            "&select=id,order_number,status,total,service_fee,created_at,paid_at,payment_status,customer,items:express_order_items(quantity)&order=created_at.desc&limit=1000"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Dim remoteOrders = JsonSerializer.Deserialize(Of List(Of RemoteOrder))(response, JsonOptions)
        Dim orders As New List(Of LocalData.OrderItem)()
        For Each remote In If(remoteOrders, New List(Of RemoteOrder)())
            Dim occurred As DateTime
            DateTime.TryParse(remote.created_at, occurred)
            Dim paidAt As DateTime?
            Dim parsedPaidAt As DateTime
            If DateTime.TryParse(remote.paid_at, parsedPaidAt) Then paidAt = parsedPaidAt
            Dim customerName = "Customer"
            If remote.customer.ValueKind = JsonValueKind.Object Then
                For Each field In {"full_name", "name", "customer_name"}
                    Dim value As JsonElement
                    If remote.customer.TryGetProperty(field, value) AndAlso value.ValueKind = JsonValueKind.String Then
                        customerName = value.GetString()
                        Exit For
                    End If
                Next
            End If
            Dim itemCount = 0
            If remote.items.ValueKind = JsonValueKind.Array Then
                For Each orderItem In remote.items.EnumerateArray()
                    Dim quantity As JsonElement
                    Dim parsedQuantity As Integer
                    If orderItem.TryGetProperty("quantity", quantity) AndAlso quantity.TryGetInt32(parsedQuantity) Then
                        itemCount += parsedQuantity
                    End If
                Next
            End If
            orders.Add(New LocalData.OrderItem() With {
                .id = remote.id, .order_number = remote.order_number, .status = remote.status,
                .total = remote.total, .service_fee = remote.service_fee, .created_at = remote.created_at, .seller_id = AppSession.SellerId,
                .customer_name = customerName, .items = itemCount, .occurred_at = occurred,
                .paid_at = paidAt, .payment_status = remote.payment_status
            })
        Next
        Return orders
    End Function

    Public Shared Async Function LoadReviewsAsync() As Task(Of List(Of LocalData.ReviewItem))
        If AppSession.Mode = StoreMode.Demo Then Return LocalData.Load().Reviews

        Dim path = "/rest/v1/express_reviews?select=id,product_id,rating,title,comment,created_at,product:express_products!inner(title,seller_id),customer:express_profiles(full_name)&is_approved=eq.true&product.seller_id=eq." &
            Uri.EscapeDataString(AppSession.SellerId) & "&order=created_at.desc&limit=500"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Using reviewsJson = JsonDocument.Parse(response)
            Dim reviews As New List(Of LocalData.ReviewItem)()
            For Each row In reviewsJson.RootElement.EnumerateArray()
                Dim review = New LocalData.ReviewItem() With {
                    .id = GetString(row, "id"), .product_id = GetString(row, "product_id"),
                    .rating = GetInteger(row, "rating"), .title = GetString(row, "title"),
                    .comment = GetString(row, "comment")
                }
                DateTime.TryParse(GetString(row, "created_at"), review.created_at)
                Dim product As JsonElement
                If row.TryGetProperty("product", product) Then review.product_name = GetString(product, "title")
                Dim customer As JsonElement
                If row.TryGetProperty("customer", customer) Then review.customer_name = GetString(customer, "full_name")
                reviews.Add(review)
            Next
            Return reviews
        End Using
    End Function

    Private Shared Function GetString(element As JsonElement, propertyName As String) As String
        Dim value As JsonElement
        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(propertyName, value) AndAlso value.ValueKind = JsonValueKind.String Then
            Return value.GetString()
        End If
        Return String.Empty
    End Function

    Private Shared Function GetInteger(element As JsonElement, propertyName As String) As Integer
        Dim value As JsonElement
        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(propertyName, value) AndAlso value.TryGetInt32(GetInteger) Then
            Return GetInteger
        End If
        Return 0
    End Function

    Private Shared Sub EnsureReturnedRow(responseBody As String, operation As String)
        Using json = JsonDocument.Parse(responseBody)
            If json.RootElement.ValueKind <> JsonValueKind.Array OrElse json.RootElement.GetArrayLength() = 0 Then
                Throw New InvalidOperationException(operation & " was not applied. The store may not have permission to change this record.")
            End If
        End Using
    End Sub

    Private Shared Async Function SendAsync(method As HttpMethod, path As String, body As String, authenticated As Boolean, Optional prefer As String = Nothing, Optional allowRefresh As Boolean = True) As Task(Of String)
        Dim lastError As Exception = Nothing
        For attempt = 1 To MaxRetries
            Dim shouldRetry As Boolean = False
            Try
                Dim responseBody As String
                Dim statusCode As Integer
                Using request = New HttpRequestMessage(method, SupabaseUrl & path)
                    request.Headers.Add("apikey", SupabaseAnonKey)
                    request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                    If authenticated Then
                        If String.IsNullOrWhiteSpace(AppSession.AccessToken) Then
                            Throw New InvalidOperationException("Your session has expired. Sign in again to continue.")
                        End If
                        request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", AppSession.AccessToken)
                    End If
                    If Not String.IsNullOrWhiteSpace(prefer) Then request.Headers.TryAddWithoutValidation("Prefer", prefer)
                    If body IsNot Nothing Then
                        request.Content = New StringContent(body, Encoding.UTF8, "application/json")
                    End If


                    Using response = Await Client.SendAsync(request)
                        responseBody = Await response.Content.ReadAsStringAsync()
                        statusCode = CInt(response.StatusCode)
                    End Using
                End Using

                If statusCode = 401 AndAlso authenticated AndAlso allowRefresh Then
                    Await RefreshSessionAsync()
                    Return Await SendAsync(method, path, body, authenticated, prefer, False)
                End If
                If statusCode < 200 OrElse statusCode >= 300 Then Throw New InvalidOperationException(ReadError(responseBody, statusCode))
                Return responseBody
            Catch ex As TaskCanceledException
                Dim endpoint = path.Split("?"c)(0)
                Throw New TimeoutException("Supabase " & method.Method & " request to " & endpoint &
                    " timed out after " & CInt(Client.Timeout.TotalSeconds).ToString() &
                    " seconds. Check your internet connection and Supabase availability, then retry.", ex)
            Catch ex As TimeoutException
                Dim endpoint = path.Split("?"c)(0)
                Throw New TimeoutException("Supabase " & method.Method & " request to " & endpoint &
                    " timed out after " & CInt(Client.Timeout.TotalSeconds).ToString() &
                    " seconds. Check your internet connection and Supabase availability, then retry.", ex)
            Catch ex As System.IO.IOException
                lastError = ex
                shouldRetry = attempt < MaxRetries
            End Try
            If shouldRetry Then Await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt))
        Next
        Throw If(lastError, New InvalidOperationException("Request failed after " & MaxRetries.ToString() & " attempts."))
    End Function

    Private Shared Function ReadError(responseBody As String, statusCode As Integer) As String
        Try
            Using json = JsonDocument.Parse(responseBody)
                Dim root = json.RootElement
                For Each field In {"message", "msg", "error_description", "error"}
                    Dim value As JsonElement
                    If root.TryGetProperty(field, value) AndAlso value.ValueKind = JsonValueKind.String Then
                        Return value.GetString()
                    End If
                Next
            End Using
        Catch
        End Try
        Return "ExpressMart request failed (" & statusCode.ToString() & ")."
    End Function

    Private Class SellerResponse
        <JsonPropertyName("id")>
        Public Property Id As String

        <JsonPropertyName("name")>
        Public Property Name As String
    End Class

    Public Class FlashSaleItem
        Public Property id As String
        Public Property title As String
        Public Property image_url As String
        Public Property discount As Integer
        Public Property flash_price As Decimal
        Public Property original_price As Decimal
        Public Property start_time As String
        Public Property end_time As String
        Public Property max_quantity As Integer?
        Public Property sold_quantity As Integer
        Public Property is_active As Boolean
        Public Property created_at As String
    End Class

    Public Class CouponItem
        Public Property id As String
        Public Property code As String
        Public Property description As String
        Public Property discount As Integer
        Public Property discount_type As String
        Public Property status As String
        Public Property uses As Integer
        Public Property valid_from As String
        Public Property valid_until As String
        Public Property created_at As String
    End Class

    Public Class ReelItem
        Public Property id As String
        Public Property title As String
        Public Property video_url As String
        Public Property thumbnail_url As String
        Public Property views As Integer
        Public Property likes As Integer
        Public Property created_at As String
    End Class

    Public Class ProductCategory
        Public Property Id As String
        Public Property Name As String
    End Class

    Public Class ProductMediaUpload
        Public Property PublicUrl As String
        Public Property Key As String
    End Class

    Public Class ProductDraft
        Public Property title As String
        Public Property description As String
        Public Property category As String
        Public Property price As Decimal?
        Public Property quantity As Integer?
        Public Property tags As List(Of String)
        Public Property specifications As List(Of ProductSpecification)
    End Class

    Public Class ProductSpecification
        Public Property key As String
        Public Property value As String
    End Class

    Private Class ProductVisionResponse
        Public Property success As Boolean
        <JsonPropertyName("error")>
        Public Property ErrorMessage As String
        Public Property product As ProductDraft
    End Class

    Private Class RemoteProduct
        Public Property id As String
        Public Property title As String
        Public Property price As Decimal
        Public Property cost_price As Decimal?
        Public Property compare_at_price As Decimal?
        Public Property shipping_fee As Decimal?
        Public Property quantity As Integer?
        Public Property sold_count As Integer?
        Public Property reorder_level As Integer?
        Public Property sku As String
        Public Property barcode As String
        Public Property category As String
        Public Property category_id As String
        Public Property description As String
        Public Property status As String
        Public Property thumbnail As String
        Public Property thumbnails As List(Of String)
        Public Property video_url As String
        Public Property r2_video_key As String
        Public Property discount As Decimal?
        Public Property sizes As List(Of String)
        Public Property colors As List(Of String)
        Public Property badges As List(Of String)
        Public Property tags As List(Of String)
        Public Property specifications As Dictionary(Of String, String)
        Public Property track_inventory As Boolean?
        Public Property allow_backorder As Boolean?
        Public Property is_preorder As Boolean?
        Public Property vendor As String
        Public Property weight As Decimal?
        Public Property weight_unit As String
        Public Property slug As String
        Public Property seller_id As String
    End Class

    Private Class RemoteOrder
        Public Property id As String
        Public Property order_number As String
        Public Property status As String
        Public Property total As Decimal
        Public Property service_fee As Decimal
        Public Property created_at As String
        Public Property paid_at As String
        Public Property payment_status As String
        Public Property customer As JsonElement
        Public Property items As JsonElement
    End Class

    Private Class RemoteStoreSettings
        Public Property name As String
        Public Property email As String
        Public Property description As String
        Public Property store_description As String
        Public Property theme_color As String
        Public Property storefront_theme As String
        Public Property storefront_layout As String
        Public Property storefront_enabled As Boolean?
        Public Property avatar As String
        Public Property cover_image As String
        Public Property cover_image_2 As String
        Public Property social_website As String
        Public Property social_facebook As String
        Public Property social_instagram As String
        Public Property social_twitter As String
        Public Property social_whatsapp As String
    End Class

    Private Class RemoteFlashSale
        Public Property id As String
        Public Property product_id As String
        Public Property flash_price As Decimal
        Public Property original_price As Decimal
        Public Property discount_percentage As Decimal?
        Public Property start_time As String
        Public Property end_time As String
        Public Property max_quantity As Integer?
        Public Property sold_quantity As Integer?
        Public Property is_active As Boolean?
        Public Property created_at As String
        Public Property product As JsonElement
    End Class

    Private Class RemoteCoupon
        Public Property id As String
        Public Property code As String
        Public Property description As String
        Public Property discount_type As String
        Public Property discount_value As Decimal?
        Public Property usage_count As Integer?
        Public Property valid_from As String
        Public Property valid_until As String
        Public Property is_active As Boolean?
    End Class

    Private Class RemoteReel
        Public Property id As String
        Public Property title As String
        Public Property video_url As String
        Public Property thumbnail_url As String
        Public Property views As Integer?
        Public Property likes As Integer?
        Public Property created_at As String
    End Class
End Class