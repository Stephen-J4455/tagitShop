Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Partial Public Class Stocktake
    Inherits Page

    Private rows As New List(Of StocktakeRow)()
    Private auditSaveFailed As Boolean

    Public Class StocktakeRow
        Public Property Product As LocalData.ProductItem
        Public Property ExpectedQuantity As Integer
        Public Property CountedQuantity As Integer
        Public ReadOnly Property Variance As Integer
            Get
                Return CountedQuantity - ExpectedQuantity
            End Get
        End Property
        Public ReadOnly Property ValueVariance As Decimal
            Get
                Return Variance * Product.price
            End Get
        End Property
    End Class

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await LoadCountSheetAsync()
        LoadHistory()
    End Sub

    Private Async Function LoadCountSheetAsync() As Threading.Tasks.Task
        Try
            Dim products = Await SupabaseService.LoadProductsAsync()
            rows = products.Select(Function(product) New StocktakeRow() With {
                .Product = product,
                .ExpectedQuantity = product.quantity,
                .CountedQuantity = product.quantity
            }).ToList()
            StocktakeGrid.ItemsSource = rows
            TxtCountFeedback.Text = rows.Count.ToString() & " products ready to count."
        Catch ex As Exception
            rows = New List(Of StocktakeRow)()
            StocktakeGrid.ItemsSource = rows
            TxtCountFeedback.Text = ex.Message
        End Try
    End Function

    Private Sub LoadHistory()
        Dim history = LocalData.Load().Stocktakes.OrderByDescending(Function(session) session.counted_at).Take(5).ToList()
        StocktakeHistory.ItemsSource = history
        Dim latest = history.FirstOrDefault()
        If latest IsNot Nothing Then TxtLastCount.Text = "Last count " & latest.counted_at.ToString("MMM d")
    End Sub

    Private Sub PageScrollViewer_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs)
        PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - e.Delta)
        e.Handled = True
    End Sub

    Private Async Sub BtnApplyCount_Click(sender As Object, e As RoutedEventArgs)
        StocktakeGrid.CommitEdit(DataGridEditingUnit.Cell, True)
        StocktakeGrid.CommitEdit(DataGridEditingUnit.Row, True)
        If rows.Count = 0 Then Return

        If rows.Any(Function(row) row.CountedQuantity < 0) Then
            MessageBox.Show("Counted quantities cannot be negative.", "Stocktake", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim changedRows = rows.Where(Function(row) row.Variance <> 0).ToList()
        If changedRows.Count = 0 Then
            MessageBox.Show("Every counted quantity matches the current stock record.", "Stocktake", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim netVariance = changedRows.Sum(Function(row) row.Variance)
        Dim prompt = changedRows.Count.ToString() & " products will change. Net variance: " & netVariance.ToString("+#;-#;0") & " units." & Environment.NewLine & Environment.NewLine & "Apply these inventory adjustments?"
        If MessageBox.Show(prompt, "Confirm stocktake", MessageBoxButton.YesNo, MessageBoxImage.Question) <> MessageBoxResult.Yes Then Return

        Dim applied As New List(Of StocktakeRow)()
        For Each row In changedRows
            Try
                Await SupabaseService.UpdateProductQuantityAsync(row.Product.id, row.CountedQuantity)
                applied.Add(row)
            Catch ex As Exception
                SaveAudit(applied)
                TxtCountFeedback.Text = "Count stopped after " & applied.Count.ToString() & " updates. " & ex.Message & If(auditSaveFailed, " The audit history could not be saved.", String.Empty)
                LoadHistory()
                Return
            End Try
        Next

        SaveAudit(applied)
        If auditSaveFailed Then Return
        TxtCountFeedback.Text = applied.Count.ToString() & " quantity adjustments applied."
        LoadHistory()
        Await LoadCountSheetAsync()
    End Sub

    Private Sub SaveAudit(appliedRows As List(Of StocktakeRow))
        auditSaveFailed = False
        If appliedRows.Count = 0 Then Return
        Dim session As New LocalData.StocktakeSession() With {
            .id = Guid.NewGuid().ToString("N"),
            .counted_at = DateTime.Now
        }
        For Each row In appliedRows
            session.items.Add(New LocalData.StocktakeItem() With {
                .product_id = row.Product.id,
                .product_name = row.Product.title,
                .expected_quantity = row.ExpectedQuantity,
                .counted_quantity = row.CountedQuantity,
                .variance = row.Variance
            })
        Next
        Dim stocktakes = LocalData.Load().Stocktakes
        stocktakes.Add(session)
        Try
            LocalData.Save()
        Catch ex As Exception
            stocktakes.Remove(session)
            TxtCountFeedback.Text = appliedRows.Count.ToString() & " quantity adjustments applied, but the audit history could not be saved: " & ex.Message
            auditSaveFailed = True
        End Try
    End Sub

End Class