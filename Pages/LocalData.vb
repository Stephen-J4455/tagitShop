Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json

    Public Class LocalData

        Private Shared ReadOnly DataPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tagitShop",
            "data.json")

        Public Class AppData
            Public Property Products As New List(Of ProductItem)()
            Public Property Orders As New List(Of OrderItem)()
            Public Property DebtRecords As New List(Of DebtRecord)()
            Public Property Stocktakes As New List(Of StocktakeSession)()
            Public Property Reviews As New List(Of ReviewItem)()
            Public Property Store As New StoreSettings()
        End Class

        Public Class StoreSettings
            Public Property name As String = "tagit Demo Store"
            Public Property email As String = "store@tagit.local"
            Public Property description As String = "A neighborhood store serving everyday essentials."
        End Class

        Public Class ProductItem
            Public Property id As String
            Public Property title As String
            Public Property price As Decimal
            Public Property cost_price As Decimal
            Public Property thumbnail As String
            Public Property seller_id As String
            Public Property status As String
            Public Property discount As Integer
            Public Property quantity As Integer
            Public Property reorder_level As Integer
            Public Property rating As Double
            Public Property category As String
            Public Property sku As String
            Public Property barcode As String
            Public Property updated_at As DateTime
            Public ReadOnly Property image As String
                Get
                    Return If(Not String.IsNullOrEmpty(thumbnail), thumbnail, "pack://application:,,,/assets/placeholder.png")
                End Get
            End Property
            Public Property Name As String
                Get
                    Return title
                End Get
                Set(value As String)
                    title = value
                End Set
            End Property
        End Class

        Public Class OrderItem
            Public Property id As String
            Public Property order_number As String
            Public Property status As String
            Public Property total As Decimal
            Public Property created_at As String
            Public Property seller_id As String
            Public Property customer_name As String
            Public Property items As Integer
            Public Property occurred_at As DateTime
            Public Property paid_at As DateTime?
            Public Property payment_status As String
            Public Property recordDebt As Boolean
            Public Property debtAmount As Decimal
            Public Property cost_of_goods_sold As Decimal?
        End Class

        Public Class DebtRecord
            Public Property id As String
            Public Property customer As String
            Public Property amount As Decimal
            Public Property paid_amount As Decimal
            Public Property recordedOn As DateTime
            Public Property notes As String
            Public Property payments As New List(Of DebtPayment)()
        End Class

        Public Class DebtPayment
            Public Property amount As Decimal
            Public Property paid_at As DateTime
            Public Property note As String
        End Class

        Public Class StocktakeSession
            Public Property id As String
            Public Property counted_at As DateTime
            Public Property notes As String
            Public Property items As New List(Of StocktakeItem)()
            Public ReadOnly Property ItemCount As Integer
                Get
                    Return items.Count
                End Get
            End Property
            Public ReadOnly Property VarianceCount As Integer
                Get
                    Return items.FindAll(Function(item) item.variance <> 0).Count
                End Get
            End Property
        End Class

        Public Class StocktakeItem
            Public Property product_id As String
            Public Property product_name As String
            Public Property expected_quantity As Integer
            Public Property counted_quantity As Integer
            Public Property variance As Integer
        End Class

        Public Class ReviewItem
            Public Property id As String
            Public Property product_id As String
            Public Property product_name As String
            Public Property customer_name As String
            Public Property rating As Integer
            Public Property title As String
            Public Property comment As String
            Public Property created_at As DateTime
        End Class

        Private Shared _data As AppData = Nothing
        Private Shared _lock As New Object()

        Public Shared Function Load() As AppData
            SyncLock _lock
                If _data IsNot Nothing Then Return _data

                Try
                    If File.Exists(DataPath) Then
                        Dim json = File.ReadAllText(DataPath)
                        _data = JsonSerializer.Deserialize(json, GetType(AppData))
                        If _data Is Nothing Then _data = New AppData()
                    Else
                        _data = New AppData()
                        SeedData(_data)
                        Save()
                    End If
                Catch
                    _data = New AppData()
                    SeedData(_data)
                End Try

                NormalizeData(_data)
                If EnsureDemoSales(_data) Then Save()

                Return _data
            End SyncLock
        End Function

        Public Shared Sub Save()
            SyncLock _lock
                If _data Is Nothing Then Return
                Try
                    Dim dir = Path.GetDirectoryName(DataPath)
                    If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
                    Dim options = New JsonSerializerOptions() With {.WriteIndented = True}
                    File.WriteAllText(DataPath, JsonSerializer.Serialize(_data, options))
                Catch ex As Exception
                    Throw New IOException("Could not save shop data to " & DataPath & ".", ex)
                End Try
            End SyncLock
        End Sub

        Private Shared Sub SeedData(data As AppData)
            data.Products.Add(New ProductItem() With {
                .id = "1", .title = "Fresh Organic Apples", .price = 3.49D,
                .cost_price = 2.1D, .sku = "PRD-1001", .barcode = "100000000001", .reorder_level = 18,
                .thumbnail = "pack://application:,,,/assets/placeholder.png",
                .seller_id = "local", .status = "active", .quantity = 100, .category = "Produce", .updated_at = DateTime.Today
            })
            data.Products.Add(New ProductItem() With {
                .id = "2", .title = "Whole Grain Bread", .price = 2.99D,
                .cost_price = 1.65D, .sku = "BAK-2001", .barcode = "100000000002", .reorder_level = 12,
                .thumbnail = "pack://application:,,,/assets/placeholder.png",
                .seller_id = "local", .status = "active", .quantity = 50, .category = "Bakery", .updated_at = DateTime.Today
            })
            data.Products.Add(New ProductItem() With {
                .id = "3", .title = "Free-Range Eggs", .price = 4.25D,
                .cost_price = 3.1D, .sku = "DAI-3001", .barcode = "100000000003", .reorder_level = 16,
                .thumbnail = "pack://application:,,,/assets/placeholder.png",
                .seller_id = "local", .status = "active", .quantity = 75, .category = "Dairy", .updated_at = DateTime.Today
            })
            data.Products.Add(New ProductItem() With {
                .id = "4", .title = "Organic Spinach", .price = 2.79D,
                .cost_price = 1.35D, .sku = "PRD-1002", .barcode = "100000000004", .reorder_level = 15,
                .thumbnail = "pack://application:,,,/assets/placeholder.png",
                .seller_id = "local", .status = "active", .quantity = 60, .category = "Produce", .updated_at = DateTime.Today
            })
            data.Products.Add(New ProductItem() With {
                .id = "5", .title = "Almond Milk", .price = 3.99D,
                .cost_price = 2.6D, .sku = "DAI-3002", .barcode = "100000000005", .reorder_level = 10,
                .thumbnail = "pack://application:,,,/assets/placeholder.png",
                .seller_id = "local", .status = "active", .quantity = 40, .category = "Dairy", .updated_at = DateTime.Today
            })

            data.Orders.Add(New OrderItem() With {
                .id = "1", .order_number = "ORD-1024", .status = "Delivered",
                .total = 45.75D, .created_at = DateTime.Today.AddHours(-1).ToString("O"), .seller_id = "seller-1",
                .customer_name = "Customer A", .items = 3, .occurred_at = DateTime.Today.AddHours(-1), .paid_at = DateTime.Today.AddHours(-1),
                .payment_status = "success", .recordDebt = False, .debtAmount = 0, .cost_of_goods_sold = 28D
            })
            data.Orders.Add(New OrderItem() With {
                .id = "2", .order_number = "ORD-1023", .status = "Processing",
                .total = 89.5D, .created_at = DateTime.Today.AddDays(-1).ToString("O"), .seller_id = "seller-1",
                .customer_name = "Customer B", .items = 2, .occurred_at = DateTime.Today.AddDays(-1), .paid_at = DateTime.Today.AddDays(-1),
                .payment_status = "success", .recordDebt = False, .debtAmount = 0, .cost_of_goods_sold = 56D
            })
            data.Orders.Add(New OrderItem() With {
                .id = "3", .order_number = "ORD-1022", .status = "Shipped",
                .total = 120D, .created_at = DateTime.Today.AddMonths(-1).ToString("O"), .seller_id = "seller-1",
                .customer_name = "Customer C", .items = 4, .occurred_at = DateTime.Today.AddMonths(-1), .paid_at = DateTime.Today.AddMonths(-1),
                .payment_status = "success", .recordDebt = False, .debtAmount = 0, .cost_of_goods_sold = 78D
            })

            data.DebtRecords.Add(New DebtRecord() With {
                .id = "DEBT-1001", .customer = "Amina Yusuf", .amount = 18750D, .paid_amount = 5000D,
                .recordedOn = DateTime.Today.AddDays(-4), .notes = "Weekly provisions", .payments = New List(Of DebtPayment) From {
                    New DebtPayment() With {.amount = 5000D, .paid_at = DateTime.Today.AddDays(-2), .note = "Part payment"}
                }
            })

            data.Reviews.Add(New ReviewItem() With {
                .id = "REV-1001", .product_id = "1", .product_name = "Fresh Organic Apples",
                .customer_name = "Maya Okafor", .rating = 5, .title = "Crisp and fresh",
                .comment = "Great quality, the whole family enjoyed them.", .created_at = DateTime.Today.AddDays(-1)
            })
            data.Reviews.Add(New ReviewItem() With {
                .id = "REV-1002", .product_id = "2", .product_name = "Whole Grain Bread",
                .customer_name = "Daniel Mensah", .rating = 4, .title = "Good everyday loaf",
                .comment = "Fresh and well packed.", .created_at = DateTime.Today.AddDays(-3)
            })
        End Sub

        Private Shared Sub NormalizeData(data As AppData)
            If data.Products Is Nothing Then data.Products = New List(Of ProductItem)()
            If data.Orders Is Nothing Then data.Orders = New List(Of OrderItem)()
            If data.DebtRecords Is Nothing Then data.DebtRecords = New List(Of DebtRecord)()
            If data.Stocktakes Is Nothing Then data.Stocktakes = New List(Of StocktakeSession)()
            If data.Reviews Is Nothing Then data.Reviews = New List(Of ReviewItem)()
            If data.Store Is Nothing Then data.Store = New StoreSettings()
            For Each debt In data.DebtRecords
                If debt IsNot Nothing AndAlso debt.payments Is Nothing Then
                    debt.payments = New List(Of DebtPayment)()
                End If
            Next

            For Each order In data.Orders
                If String.IsNullOrWhiteSpace(order.payment_status) Then
                    order.payment_status = If(String.Equals(order.status, "Delivered", StringComparison.OrdinalIgnoreCase) AndAlso Not order.recordDebt, "success", "pending")
                End If
                If order.occurred_at <= DateTime.MinValue Then
                    Dim parsed As DateTime
                    order.occurred_at = If(DateTime.TryParse(order.created_at, parsed), parsed, DateTime.Today)
                End If
                If String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase) AndAlso Not order.paid_at.HasValue Then
                    order.paid_at = order.occurred_at
                End If
                If order.recordDebt AndAlso order.debtAmount > 0D AndAlso Not data.DebtRecords.Any(Function(record) record.id = order.id) Then
                    data.DebtRecords.Add(New DebtRecord() With {
                        .id = If(String.IsNullOrWhiteSpace(order.id), Guid.NewGuid().ToString("N"), order.id),
                        .customer = order.customer_name,
                        .amount = order.debtAmount,
                        .paid_amount = 0D,
                        .recordedOn = order.occurred_at,
                        .notes = "Migrated from an earlier local order"
                    })
                End If
            Next
        End Sub

        Private Shared Function EnsureDemoSales(data As AppData) As Boolean
            Dim changed = False
            Dim seededCosts As New Dictionary(Of String, Decimal) From {
                {"1", 28D},
                {"2", 56D},
                {"3", 78D}
            }
            For Each order In data.Orders
                Dim seededCost As Decimal
                If Not String.IsNullOrEmpty(order.id) AndAlso seededCosts.TryGetValue(order.id, seededCost) AndAlso Not order.cost_of_goods_sold.HasValue Then
                    order.cost_of_goods_sold = seededCost
                    changed = True
                End If
            Next

            Dim revenues As Decimal() = {120D, 82.5D, 174D, 96D, 143.5D, 68D, 205D}
            Dim costs As Decimal() = {76D, 52D, 111D, 61D, 91D, 43D, 130D}
            Dim customers As String() = {"Maya Okafor", "Daniel Mensah", "Amina Yusuf", "Kojo Asante", "Esi Boateng", "Kwame Owusu", "Ama Nkrumah"}
            Dim itemCounts As Integer() = {4, 3, 5, 2, 4, 2, 6}

            For offset = 6 To 0 Step -1
                Dim index = 6 - offset
                Dim id = "DEMO-RECENT-" & index.ToString()
                Dim saleDate = DateTime.Today.AddDays(-offset).AddHours(10 + index)
                Dim order = data.Orders.FirstOrDefault(Function(item) String.Equals(item.id, id, StringComparison.Ordinal))
                If order Is Nothing Then
                    order = New OrderItem() With {.id = id}
                    data.Orders.Add(order)
                    changed = True
                End If

                If order.order_number <> id OrElse order.status <> "Delivered" OrElse order.total <> revenues(index) OrElse
                    order.created_at <> saleDate.ToString("O") OrElse order.customer_name <> customers(index) OrElse
                    order.items <> itemCounts(index) OrElse order.occurred_at <> saleDate OrElse order.paid_at <> saleDate OrElse
                    order.payment_status <> "success" OrElse order.cost_of_goods_sold <> costs(index) Then
                    order.order_number = id
                    order.status = "Delivered"
                    order.total = revenues(index)
                    order.created_at = saleDate.ToString("O")
                    order.seller_id = "seller-1"
                    order.customer_name = customers(index)
                    order.items = itemCounts(index)
                    order.occurred_at = saleDate
                    order.paid_at = saleDate
                    order.payment_status = "success"
                    order.recordDebt = False
                    order.debtAmount = 0D
                    order.cost_of_goods_sold = costs(index)
                    changed = True
                End If
            Next

            Return changed
        End Function
    End Class