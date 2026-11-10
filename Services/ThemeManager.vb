Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Media

Public NotInheritable Class ThemeManager
    Private Const LightMode As String = "light"
    Private Const DarkMode As String = "dark"
    Private Shared _currentMode As String = LightMode

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property CurrentMode As String
        Get
            Return _currentMode
        End Get
    End Property

    Public Shared ReadOnly Property IsDark As Boolean
        Get
            Return String.Equals(_currentMode, DarkMode, StringComparison.Ordinal)
        End Get
    End Property

    Public Shared Sub Initialize()
        Dim mode = LocalData.LoadThemeMode()
        _currentMode = If(String.Equals(mode, DarkMode, StringComparison.OrdinalIgnoreCase), DarkMode, LightMode)
        ApplyPalette()
    End Sub

    Public Shared Sub Toggle()
        Dim nextMode = If(IsDark, LightMode, DarkMode)
        LocalData.SaveThemeMode(nextMode)
        _currentMode = nextMode
        ApplyPalette()
    End Sub

    Private Shared Sub ApplyPalette()
        Dim palette As Dictionary(Of String, String)
        If IsDark Then
            palette = New Dictionary(Of String, String) From {
                {"WindowBackground", "#10151F"},
                {"TitleBarBackground", "#141B27"},
                {"SidebarBackground", "#141B27"},
                {"SidebarItemHover", "#202B3A"},
                {"SidebarItemActive", "#39212D"},
                {"SidebarItemActiveBorder", "#FF5A79"},
                {"SidebarTextSecondary", "#A5B0C0"},
                {"SidebarSurface", "#1B2432"},
                {"WindowControlHover", "#263142"},
                {"WindowCloseHover", "#9E334B"},
                {"ErrorText", "#FF9A9A"},
                {"TextDark", "#F3F4F6"},
                {"TextMuted", "#A5B0C0"},
                {"IconColor", "#A5B0C0"},
                {"IconActive", "#F3F4F6"},
                {"TagitDark", "#10151F"},
                {"TagitMuted", "#A5B0C0"},
                {"TagitSurface", "#222D3D"},
                {"TagitBorder", "#344156"},
                {"TagitBackground", "#1B2432"},
                {"CardSurface", "#1B2432"},
                {"InputSurface", "#111927"},
                {"DataGridAltSurface", "#202A39"},
                {"TableHeaderSurface", "#172130"}
            }
        Else
            palette = New Dictionary(Of String, String) From {
                {"WindowBackground", "#F1F1F0"},
                {"TitleBarBackground", "#FBFBFA"},
                {"SidebarBackground", "#FBFBFA"},
                {"SidebarItemHover", "#F1F1F0"},
                {"SidebarItemActive", "#FFF0F3"},
                {"SidebarItemActiveBorder", "#FF5A79"},
                {"SidebarTextSecondary", "#777777"},
                {"SidebarSurface", "#F3F3F2"},
                {"WindowControlHover", "#EAEAE9"},
                {"WindowCloseHover", "#E7E7E6"},
                {"ErrorText", "#A63D32"},
                {"TextDark", "#202020"},
                {"TextMuted", "#727272"},
                {"IconColor", "#6C6C6C"},
                {"IconActive", "#202020"},
                {"TagitDark", "#1C1C1C"},
                {"TagitMuted", "#707070"},
                {"TagitSurface", "#F1F1F0"},
                {"TagitBorder", "#E1E1E0"},
                {"TagitBackground", "#FFFFFF"},
                {"CardSurface", "#FFFFFF"},
                {"InputSurface", "#F7F7F6"},
                {"DataGridAltSurface", "#F8F8F7"},
                {"TableHeaderSurface", "#F7F7F6"}
            }
        End If

        For Each entry In palette
            Application.Current.Resources(entry.Key) =
                New SolidColorBrush(CType(ColorConverter.ConvertFromString(entry.Value), Color))
        Next
    End Sub
End Class
