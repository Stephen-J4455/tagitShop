Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading.Tasks

Public NotInheritable Class SupabaseService
    Private Const SupabaseUrl As String = "https://meiljgoztnhnyvtfkzuh.supabase.co"
    Private Const SupabaseAnonKey As String = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im1laWxqZ296dG5obnl2dGZrenVoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjUxMTI0OTksImV4cCI6MjA4MDY4ODQ5OX0.X7zve3MSvaoplAHl45BpC57h9G4IY5suhBBteIoEU3I"
    Private Shared ReadOnly Client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(25)}
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
                .category = remote.category, .status = remote.status,
                .thumbnail = If(Not String.IsNullOrWhiteSpace(remote.thumbnail), remote.thumbnail, If(remote.thumbnails IsNot Nothing AndAlso remote.thumbnails.Count > 0, remote.thumbnails(0), Nothing)),
                .discount = CInt(If(remote.discount, 0D)), .seller_id = If(String.IsNullOrWhiteSpace(remote.seller_id), AppSession.SellerId, remote.seller_id)
            })
        Next

        Return products
    End Function

    Private Shared Async Function LoadRemoteProductsAsync() As Task(Of List(Of RemoteProduct))
        If String.IsNullOrWhiteSpace(AppSession.SellerId) Then
            Return Await LoadAllProductsAsync()
        End If

        Dim sellerScopedPath = "/rest/v1/express_products?seller_id=eq." & Uri.EscapeDataString(AppSession.SellerId) &
            "&select=id,title,price,cost_price,quantity,sku,barcode,category,status,thumbnail,thumbnails,discount,seller_id&order=title.asc"

        Try
            Dim response = Await SendAsync(HttpMethod.Get, sellerScopedPath, Nothing, True)
            Dim remoteProducts = JsonSerializer.Deserialize(Of List(Of RemoteProduct))(response, JsonOptions)
            Return If(remoteProducts, New List(Of RemoteProduct)())
        Catch
            Return New List(Of RemoteProduct)()
        End Try
    End Function

    Private Shared Async Function LoadAllProductsAsync() As Task(Of List(Of RemoteProduct))
        Dim allPath = "/rest/v1/express_products?select=id,title,price,cost_price,quantity,sku,barcode,category,status,thumbnail,thumbnails,discount,seller_id&order=title.asc&limit=1000"
        Dim fallback = Await SendAsync(HttpMethod.Get, allPath, Nothing, True)
        Dim remoteProducts = JsonSerializer.Deserialize(Of List(Of RemoteProduct))(fallback, JsonOptions)
        Return If(remoteProducts, New List(Of RemoteProduct)())
    End Function

    Public Shared Async Function LoadStoreSettingsAsync() As Task(Of LocalData.StoreSettings)
        If AppSession.Mode = StoreMode.Demo Then Return LocalData.Load().Store
        Dim path = "/rest/v1/express_sellers?id=eq." & Uri.EscapeDataString(AppSession.SellerId) & "&select=name,email,description,store_description&limit=1"
        Dim response = Await SendAsync(HttpMethod.Get, path, Nothing, True)
        Dim settings = JsonSerializer.Deserialize(Of List(Of RemoteStoreSettings))(response, JsonOptions)
        If settings Is Nothing OrElse settings.Count = 0 Then Throw New InvalidOperationException("The store profile could not be loaded.")
        Return New LocalData.StoreSettings() With {
            .name = settings(0).name,
            .email = settings(0).email,
            .description = If(String.IsNullOrWhiteSpace(settings(0).store_description), settings(0).description, settings(0).store_description)
        }
    End Function

    Public Shared Async Function SaveStoreSettingsAsync(settings As LocalData.StoreSettings) As Task
        If AppSession.Mode = StoreMode.Demo Then
            LocalData.Load().Store = settings
            LocalData.Save()
            AppSession.StoreName = settings.name
            Return
        End If

        Dim path = "/rest/v1/express_sellers?id=eq." & Uri.EscapeDataString(AppSession.SellerId)
        Dim payload = JsonSerializer.Serialize(New With {
            .name = settings.name,
            .email = settings.email,
            .description = settings.description,
            .store_description = settings.description
        })
        Dim saved = Await SendAsync(New HttpMethod("PATCH"), path & "&select=id", payload, True, "return=representation")
        EnsureReturnedRow(saved, "Store profile update")
        AppSession.StoreName = settings.name
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
            .cost_price = product.cost_price,
            .quantity = product.quantity,
            .sku = product.sku,
            .barcode = product.barcode,
            .category = product.category,
            .status = If(String.IsNullOrWhiteSpace(product.status), "active", product.status),
            .discount = product.discount,
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
                .cost_price = product.cost_price,
                .quantity = product.quantity,
                .sku = product.sku,
                .barcode = product.barcode,
                .category = product.category,
                .status = If(String.IsNullOrWhiteSpace(product.status), "active", product.status),
                .discount = product.discount,
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
            "&select=id,order_number,status,total,created_at,paid_at,payment_status,customer&order=created_at.desc&limit=1000"
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
            orders.Add(New LocalData.OrderItem() With {
                .id = remote.id, .order_number = remote.order_number, .status = remote.status,
                .total = remote.total, .created_at = remote.created_at, .seller_id = AppSession.SellerId,
                .customer_name = customerName, .items = 1, .occurred_at = occurred,
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

    Private Class RemoteProduct
        Public Property id As String
        Public Property title As String
        Public Property price As Decimal
        Public Property cost_price As Decimal?
        Public Property quantity As Integer?
        Public Property reorder_level As Integer?
        Public Property sku As String
        Public Property barcode As String
        Public Property category As String
        Public Property status As String
        Public Property thumbnail As String
        Public Property thumbnails As List(Of String)
        Public Property discount As Decimal?
        Public Property seller_id As String
    End Class

    Private Class RemoteOrder
        Public Property id As String
        Public Property order_number As String
        Public Property status As String
        Public Property total As Decimal
        Public Property created_at As String
        Public Property paid_at As String
        Public Property payment_status As String
        Public Property customer As JsonElement
    End Class

    Private Class RemoteStoreSettings
        Public Property name As String
        Public Property email As String
        Public Property description As String
        Public Property store_description As String
    End Class
End Class