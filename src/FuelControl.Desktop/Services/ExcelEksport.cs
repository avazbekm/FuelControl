using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ClosedXML.Excel;

namespace FuelControl.Desktop.Services;

/// <summary>Jadvalni .xlsx ga yozadi: sarlavha, izoh (davr), ustunlar, qatorlar (yakun qatorlari qalin), oxirida "Jami".</summary>
public static class ExcelEksport
{
    /// <param name="qatorlar">Har qator: qiymatlar (string / long / decimal / int) va qalinmi (guruh yakuni).</param>
    /// <returns>Saqlangan fayl yo'li.</returns>
    public static string Saqla(string papkaNomi, string faylNomi, string sarlavha, string izoh,
        IReadOnlyList<string> ustunlar, IEnumerable<(object?[] Qiymatlar, bool Qalin)> qatorlar, object?[]? jami)
    {
        var papka = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FuelControl", papkaNomi);
        Directory.CreateDirectory(papka);
        var fayl = Path.Combine(papka, faylNomi);

        using var kitob = new XLWorkbook();
        var v = kitob.AddWorksheet(sarlavha.Length > 31 ? sarlavha[..31] : sarlavha);
        v.Cell(1, 1).Value = sarlavha;
        v.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        v.Cell(2, 1).Value = izoh;
        v.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);

        const int boshi = 4;
        for (int i = 0; i < ustunlar.Count; i++) v.Cell(boshi, i + 1).Value = ustunlar[i];
        var sarlavhaQatori = v.Range(boshi, 1, boshi, ustunlar.Count);
        sarlavhaQatori.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEFF"))
            .Border.SetBottomBorder(XLBorderStyleValues.Thin);

        int r = boshi + 1;
        foreach (var (qiymatlar, qalin) in qatorlar)
        {
            Yoz(v, r, qiymatlar);
            if (qalin) v.Range(r, 1, r, ustunlar.Count).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F4F6FB"));
            r++;
        }
        if (jami is not null)
        {
            Yoz(v, r, jami);
            v.Range(r, 1, r, ustunlar.Count).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Medium);
        }

        v.SheetView.FreezeRows(boshi);
        v.Columns(1, ustunlar.Count).AdjustToContents(boshi, r);
        kitob.SaveAs(fayl);

        // Haqiqiy ilovada papkani ochamiz (headless/sinov muhitida emas).
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            try { Process.Start(new ProcessStartInfo(papka) { UseShellExecute = true }); } catch (Exception) { }
        return fayl;
    }

    private static void Yoz(IXLWorksheet v, int r, object?[] qiymatlar)
    {
        for (int i = 0; i < qiymatlar.Length; i++)
        {
            var c = v.Cell(r, i + 1);
            switch (qiymatlar[i])
            {
                case long l: c.Value = l; c.Style.NumberFormat.Format = "#,##0"; break;
                case int n: c.Value = n; break;
                case decimal d: c.Value = d; c.Style.NumberFormat.Format = "#,##0.00"; break;
                case null: break;
                case var x: c.Value = x.ToString(); break;
            }
        }
    }
}
