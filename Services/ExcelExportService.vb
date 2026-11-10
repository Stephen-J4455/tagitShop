Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel

Public NotInheritable Class ExcelExportService
    Private Sub New()
    End Sub

    Public Shared Sub ExportSales(filePath As String, orders As IEnumerable(Of LocalData.OrderItem))
        If String.IsNullOrWhiteSpace(filePath) Then Throw New ArgumentException("An export path is required.", NameOf(filePath))
        If orders Is Nothing Then Throw New ArgumentNullException(NameOf(orders))

        Dim headers As String() = {
            "Order ID", "Order number", "Customer", "Order status", "Payment status",
            "Order date", "Paid date", "Items", "Total (GHC)", "Service fee (GHC)",
            "Cost of goods sold (GHC)", "Gross profit (GHC)", "Recorded as debt", "Debt amount (GHC)"
        }

        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("Sales records")
            WriteHeaders(worksheet, headers)

            Dim row = 2
            For Each order In orders
                worksheet.Cell(row, 1).Value = TextOrEmpty(order.id)
                worksheet.Cell(row, 2).Value = TextOrEmpty(order.order_number)
                worksheet.Cell(row, 3).Value = TextOrEmpty(order.customer_name)
                worksheet.Cell(row, 4).Value = TextOrEmpty(order.status)
                worksheet.Cell(row, 5).Value = TextOrEmpty(order.payment_status)

                Dim orderDate = ResolveOrderDate(order)
                If orderDate.HasValue Then worksheet.Cell(row, 6).Value = orderDate.Value
                If order.paid_at.HasValue Then worksheet.Cell(row, 7).Value = order.paid_at.Value

                worksheet.Cell(row, 8).Value = order.items
                worksheet.Cell(row, 9).Value = order.total
                worksheet.Cell(row, 10).Value = order.service_fee
                If order.cost_of_goods_sold.HasValue Then
                    worksheet.Cell(row, 11).Value = order.cost_of_goods_sold.Value
                    worksheet.Cell(row, 12).Value = order.total - order.cost_of_goods_sold.Value
                End If
                worksheet.Cell(row, 13).Value = order.recordDebt
                worksheet.Cell(row, 14).Value = order.debtAmount
                row += 1
            Next

            FormatWorksheet(worksheet, headers.Length, row - 1, "SalesRecords")
            For Each columnIndex In {9, 10, 11, 12, 14}
                worksheet.Column(columnIndex).Style.NumberFormat.Format = "#,##0.00"
            Next
            worksheet.Column(6).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm"
            worksheet.Column(7).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm"
            workbook.SaveAs(filePath)
        End Using
    End Sub

    Public Shared Sub ExportDebt(filePath As String, records As IEnumerable(Of LocalData.DebtRecord))
        If String.IsNullOrWhiteSpace(filePath) Then Throw New ArgumentException("An export path is required.", NameOf(filePath))
        If records Is Nothing Then Throw New ArgumentNullException(NameOf(records))

        Dim debtRecords = New List(Of LocalData.DebtRecord)(records)
        Using workbook As New XLWorkbook()
            Dim debtSheet = workbook.Worksheets.Add("Debt records")
            Dim debtHeaders As String() = {
                "Record ID", "Customer", "Recorded date", "Original amount (GHC)",
                "Paid amount (GHC)", "Outstanding balance (GHC)", "State", "Notes", "Payment count"
            }
            WriteHeaders(debtSheet, debtHeaders)

            Dim debtRow = 2
            For Each record In debtRecords
                debtSheet.Cell(debtRow, 1).Value = TextOrEmpty(record.id)
                debtSheet.Cell(debtRow, 2).Value = TextOrEmpty(record.customer)
                If record.recordedOn > DateTime.MinValue Then debtSheet.Cell(debtRow, 3).Value = record.recordedOn
                debtSheet.Cell(debtRow, 4).Value = record.amount
                debtSheet.Cell(debtRow, 5).Value = record.paid_amount
                debtSheet.Cell(debtRow, 6).Value = Math.Max(0D, record.amount - record.paid_amount)
                debtSheet.Cell(debtRow, 7).Value = DebtState(record)
                debtSheet.Cell(debtRow, 8).Value = TextOrEmpty(record.notes)
                debtSheet.Cell(debtRow, 9).Value = If(record.payments Is Nothing, 0, record.payments.Count)
                debtRow += 1
            Next
            FormatWorksheet(debtSheet, debtHeaders.Length, debtRow - 1, "DebtRecords")
            debtSheet.Column(3).Style.NumberFormat.Format = "yyyy-mm-dd"
            For Each columnIndex In {4, 5, 6}
                debtSheet.Column(columnIndex).Style.NumberFormat.Format = "#,##0.00"
            Next

            Dim paymentSheet = workbook.Worksheets.Add("Payment history")
            Dim paymentHeaders As String() = {"Record ID", "Customer", "Payment date", "Amount (GHC)", "Note"}
            WriteHeaders(paymentSheet, paymentHeaders)

            Dim paymentRow = 2
            For Each record In debtRecords
                If record.payments Is Nothing Then Continue For
                For Each payment In record.payments
                    paymentSheet.Cell(paymentRow, 1).Value = TextOrEmpty(record.id)
                    paymentSheet.Cell(paymentRow, 2).Value = TextOrEmpty(record.customer)
                    If payment.paid_at > DateTime.MinValue Then paymentSheet.Cell(paymentRow, 3).Value = payment.paid_at
                    paymentSheet.Cell(paymentRow, 4).Value = payment.amount
                    paymentSheet.Cell(paymentRow, 5).Value = TextOrEmpty(payment.note)
                    paymentRow += 1
                Next
            Next
            FormatWorksheet(paymentSheet, paymentHeaders.Length, paymentRow - 1, "DebtPayments")
            paymentSheet.Column(3).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm"
            paymentSheet.Column(4).Style.NumberFormat.Format = "#,##0.00"

            workbook.SaveAs(filePath)
        End Using
    End Sub

    Private Shared Sub WriteHeaders(worksheet As IXLWorksheet, headers As String())
        For columnIndex = 0 To headers.Length - 1
            worksheet.Cell(1, columnIndex + 1).Value = headers(columnIndex)
        Next
    End Sub

    Private Shared Sub FormatWorksheet(worksheet As IXLWorksheet, columnCount As Integer, lastRow As Integer, tableName As String)
        If lastRow > 1 Then
            Dim table = worksheet.Range(1, 1, lastRow, columnCount).CreateTable(tableName)
            table.Theme = XLTableTheme.TableStyleMedium2
        End If

        Dim header = worksheet.Range(1, 1, 1, columnCount)
        header.Style.Font.Bold = True
        header.Style.Font.FontColor = XLColor.White
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF5A79")
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center
        worksheet.Row(1).Height = 24
        worksheet.SheetView.FreezeRows(1)
        worksheet.Columns().AdjustToContents()
        For Each column In worksheet.ColumnsUsed()
            column.Width = Math.Min(column.Width, 52D)
        Next
    End Sub

    Private Shared Function TextOrEmpty(value As String) As String
        Return If(value, String.Empty)
    End Function

    Private Shared Function DebtState(record As LocalData.DebtRecord) As String
        If record.amount <= record.paid_amount Then Return "Paid"
        If record.paid_amount > 0D Then Return "Part paid"
        Return "Open"
    End Function

    Private Shared Function ResolveOrderDate(order As LocalData.OrderItem) As DateTime?
        If order.paid_at.HasValue Then Return order.paid_at.Value
        If order.occurred_at > DateTime.MinValue Then Return order.occurred_at
        Dim parsed As DateTime
        If DateTime.TryParse(order.created_at, parsed) Then Return parsed
        Return Nothing
    End Function
End Class
