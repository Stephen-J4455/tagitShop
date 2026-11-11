Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Public NotInheritable Class DataCache
        Private Shared ReadOnly _lock As New Object()
        Private Shared _loaded As Boolean = False
        Private Shared _loading As Task(Of CacheData) = Nothing
        Private Shared _generation As Integer

        Private Class CacheData
            Public Products As New List(Of LocalData.ProductItem)()
            Public Orders As New List(Of LocalData.OrderItem)()
            Public Reviews As New List(Of LocalData.ReviewItem)()
            Public FlashSales As New List(Of SupabaseService.FlashSaleItem)()
            Public Coupons As New List(Of SupabaseService.CouponItem)()
            Public Reels As New List(Of SupabaseService.ReelItem)()
            Public StoreSettings As New LocalData.StoreSettings()
        End Class

        Public Shared Products As New List(Of LocalData.ProductItem)()
        Public Shared Orders As New List(Of LocalData.OrderItem)()
        Public Shared Reviews As New List(Of LocalData.ReviewItem)()
        Public Shared FlashSales As New List(Of SupabaseService.FlashSaleItem)()
        Public Shared Coupons As New List(Of SupabaseService.CouponItem)()
        Public Shared Reels As New List(Of SupabaseService.ReelItem)()
        Public Shared StoreSettings As New LocalData.StoreSettings()

        Public Shared Async Function EnsureLoadedAsync() As Task
            Dim loadTask As Task(Of CacheData) = Nothing
            Dim loadGeneration As Integer
            SyncLock _lock
                If _loaded Then Return
                If _loading IsNot Nothing Then
                    loadTask = _loading
                Else
                    _loading = LoadAllAsync()
                    loadTask = _loading
                End If
                loadGeneration = _generation
            End SyncLock

            If loadTask IsNot Nothing Then
                Dim data = Await loadTask
                SyncLock _lock
                    If loadGeneration <> _generation Then Return
                    Apply(data)
                    _loaded = True
                    _loading = Nothing
                End SyncLock
            End If
        End Function

        Public Shared Async Function RefreshProductsAsync() As Task(Of List(Of LocalData.ProductItem))
            Dim products = Await SupabaseService.LoadProductsAsync()
            DataCache.Products = products
            Return products
        End Function

        Public Shared Async Function RefreshReelsAsync() As Task(Of List(Of SupabaseService.ReelItem))
            Dim reels = Await SupabaseService.LoadReelsAsync()
            DataCache.Reels = reels
            Return reels
        End Function

        Public Shared Async Function RefreshCouponsAsync() As Task(Of List(Of SupabaseService.CouponItem))
            Dim coupons = Await SupabaseService.LoadCouponsAsync()
            DataCache.Coupons = coupons
            Return coupons
        End Function

        Public Shared Async Function RefreshStoreSettingsAsync() As Task(Of LocalData.StoreSettings)
            Dim settings = Await SupabaseService.LoadStoreSettingsAsync()
            DataCache.StoreSettings = settings
            Return settings
        End Function

        Private Shared Async Function LoadAllAsync() As Task(Of CacheData)
            Dim data As New CacheData()

            Try
                data.Products = Await SupabaseService.LoadProductsAsync()
            Catch
                data.Products = New List(Of LocalData.ProductItem)()
            End Try

            Try
                data.Orders = Await SupabaseService.LoadOrdersAsync()
            Catch
                data.Orders = New List(Of LocalData.OrderItem)()
            End Try

            Try
                data.Reviews = Await SupabaseService.LoadReviewsAsync()
            Catch
                data.Reviews = New List(Of LocalData.ReviewItem)()
            End Try

            Try
                data.FlashSales = Await SupabaseService.LoadFlashSalesAsync()
            Catch
                data.FlashSales = New List(Of SupabaseService.FlashSaleItem)()
            End Try

            Try
                data.Coupons = Await SupabaseService.LoadCouponsAsync()
            Catch
                data.Coupons = New List(Of SupabaseService.CouponItem)()
            End Try

            Try
                data.Reels = Await SupabaseService.LoadReelsAsync()
            Catch
                data.Reels = New List(Of SupabaseService.ReelItem)()
            End Try

            Try
                data.StoreSettings = Await SupabaseService.LoadStoreSettingsAsync()
            Catch
                data.StoreSettings = New LocalData.StoreSettings()
            End Try

            Return data
        End Function

        Private Shared Sub Apply(data As CacheData)
            Products = data.Products
            Orders = data.Orders
            Reviews = data.Reviews
            FlashSales = data.FlashSales
            Coupons = data.Coupons
            Reels = data.Reels
            StoreSettings = data.StoreSettings
        End Sub

        Public Shared Sub Clear()
            SyncLock _lock
                _generation += 1
                _loaded = False
                _loading = Nothing
            End SyncLock
            Products.Clear()
            Orders.Clear()
            Reviews.Clear()
            FlashSales.Clear()
            Coupons.Clear()
            Reels.Clear()
            StoreSettings = New LocalData.StoreSettings()
        End Sub
    End Class
