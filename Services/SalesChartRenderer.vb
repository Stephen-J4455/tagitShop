Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Shapes

Public NotInheritable Class SalesChartRenderer
    Private Sub New()
    End Sub

    Public Shared Sub Draw(canvas As Canvas, buckets As IReadOnlyList(Of SalesBucket))
        canvas.Children.Clear()
        If buckets Is Nothing OrElse buckets.Count = 0 Then Return

        Dim width = If(canvas.ActualWidth > 0, canvas.ActualWidth, 700D)
        Dim height = If(canvas.ActualHeight > 0, canvas.ActualHeight, 230D)
        Dim left = 54D
        Dim right = 12D
        Dim top = 14D
        Dim bottom = 32D
        Dim plotWidth = Math.Max(1D, width - left - right)
        Dim plotHeight = Math.Max(1D, height - top - bottom)
        Dim maxValue = buckets.Max(Function(bucket) Math.Max(bucket.Revenue, If(bucket.GrossProfit, 0D)))
        Dim minValue = buckets.Min(Function(bucket) Math.Min(0D, If(bucket.GrossProfit, 0D)))
        Dim valueRange = maxValue - minValue
        If valueRange <= 0D Then valueRange = 1D
        Dim muted = TryCast(Application.Current.Resources("TextMuted"), Brush)
        Dim revenueBrush = TryCast(Application.Current.Resources("TagitAccent"), Brush)
        Dim profitBrush = TryCast(Application.Current.Resources("TagitDark"), Brush)
        Dim gridBrush = TryCast(Application.Current.Resources("TagitBorder"), Brush)

        For gridIndex = 0 To 4
            Dim y = top + (plotHeight * gridIndex / 4D)
            Dim line = New Line() With {.X1 = left, .X2 = width - right, .Y1 = y, .Y2 = y, .Stroke = gridBrush, .StrokeThickness = 1}
            canvas.Children.Add(line)
            Dim gridValue = maxValue - (valueRange * gridIndex / 4D)
            Dim label = New TextBlock() With {.Text = gridValue.ToString("N0"), .FontSize = 9, .Foreground = muted, .Width = left - 8, .TextAlignment = TextAlignment.Right}
            Canvas.SetLeft(label, 0)
            Canvas.SetTop(label, y - 7)
            canvas.Children.Add(label)
        Next

        Dim zeroY = If(maxValue = 0D AndAlso minValue = 0D, top + plotHeight, top + (maxValue / valueRange * plotHeight))
        Dim slotWidth = plotWidth / buckets.Count
        Dim barWidth = Math.Min(22D, Math.Max(4D, slotWidth * 0.28D))
        For index = 0 To buckets.Count - 1
            Dim bucket = buckets(index)
            Dim groupWidth = barWidth * 2D + 4D
            Dim x = left + index * slotWidth + (slotWidth - groupWidth) / 2D
            AddBar(canvas, x, zeroY, bucket.Revenue, valueRange, plotHeight, barWidth, revenueBrush,
                   bucket.Label & " revenue / GHC " & bucket.Revenue.ToString("N2") & " / " & bucket.OrderCount.ToString() & " paid orders")
            If bucket.GrossProfit.HasValue Then
                AddBar(canvas, x + barWidth + 4D, zeroY, bucket.GrossProfit.Value, valueRange, plotHeight, barWidth, profitBrush,
                       bucket.Label & " gross profit / GHC " & bucket.GrossProfit.Value.ToString("N2"))
            End If

            Dim label = New TextBlock() With {.Text = bucket.Label, .FontSize = 9, .Foreground = muted, .Width = slotWidth, .TextAlignment = TextAlignment.Center}
            Canvas.SetLeft(label, left + index * slotWidth)
            Canvas.SetTop(label, top + plotHeight + 8D)
            canvas.Children.Add(label)
        Next
    End Sub

    Private Shared Sub AddBar(canvas As Canvas, x As Decimal, zeroY As Decimal, value As Decimal, valueRange As Decimal,
                              plotHeight As Decimal, barWidth As Decimal, fill As Brush, toolTip As String)
        Dim barHeight = Math.Abs(value) / valueRange * plotHeight
        Dim y = If(value >= 0D, zeroY - barHeight, zeroY)
        Dim bar = New Rectangle() With {
            .Width = barWidth,
            .Height = Math.Max(1D, barHeight),
            .Fill = fill,
            .RadiusX = 2,
            .RadiusY = 2,
            .ToolTip = toolTip
        }
        Canvas.SetLeft(bar, x)
        Canvas.SetTop(bar, y)
        canvas.Children.Add(bar)
    End Sub
End Class