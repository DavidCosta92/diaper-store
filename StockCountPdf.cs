using panialera.Models;
using panialera.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Color = QuestPDF.Infrastructure.Color;

namespace panialera;

// Printable PDF of a stock count: every product with its stock according to the system
// and boxes to tick by hand. If the count already has verified items, differences or
// notes, they are printed too (so it also works as a record of a finished count).
// It is saved to Documents\Cariñitos and opened, like the CSV files.
public static class StockCountPdf
{
    private static readonly Color BorderColor = Color.FromHex("#9e9e9e");
    private static readonly Color HeaderBackground = Color.FromHex("#eeeeee");
    private static readonly Color Gray = Color.FromHex("#757575");
    private static readonly Color Red = Color.FromHex("#c0392b");
    private static readonly Color Green = Color.FromHex("#1e8449");

    public static string Export(StockCount count)
    {
        var path = CsvExporter.NewFilePath($"arqueo-{count.Id}", "pdf");
        Generate(count, path);
        CsvExporter.Open(path);
        return path;
    }

    // Writes the PDF to the given path without opening it.
    public static void Generate(StockCount count, string path)
    {
        var s = StockCountSummary.Of(count);

        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(9));

            page.Header().PaddingBottom(8).Column(col =>
            {
                col.Item().Text($"Cariñitos — Arqueo de stock #{count.Id}").FontSize(14).Bold();
                col.Item().Text(t =>
                {
                    t.Span($"Iniciado: {count.StartedAt:dd/MM/yyyy HH:mm}");
                    if (!string.IsNullOrEmpty(count.StartedBy))
                        t.Span($" por {count.StartedBy}");
                    if (count.FinishedAt.HasValue)
                    {
                        t.Span($"   ·   Finalizado: {count.FinishedAt:dd/MM/yyyy HH:mm}");
                        if (!string.IsNullOrEmpty(count.FinishedBy))
                            t.Span($" por {count.FinishedBy}");
                    }
                    else
                    {
                        t.Span("   ·   EN CURSO").Bold();
                    }
                });
                col.Item().Text($"Impreso: {DateTime.Now:dd/MM/yyyy HH:mm}   ·   {s.Items} productos   ·   " +
                                $"{s.Verified} verificados").FontColor(Gray);
            });

            page.Content().Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(22);   // check
                        c.ConstantColumn(62);   // code
                        c.RelativeColumn(3);    // product
                        c.RelativeColumn(1.3f); // category
                        c.ConstantColumn(42);   // system stock
                        c.ConstantColumn(46);   // counted
                        c.ConstantColumn(40);   // difference
                        c.RelativeColumn(2);    // note
                    });

                    table.Header(h =>
                    {
                        foreach (var title in new[] { "OK", "Código", "Producto", "Categoría", "Sistema", "Contado", "Dif.", "Nota" })
                            h.Cell().Element(HeaderCell).Text(title).SemiBold();
                    });

                    foreach (var i in count.Items)
                    {
                        // Empty box to tick by hand, or "X" if already verified in the app.
                        table.Cell().Element(Cell).AlignCenter().AlignMiddle()
                            .Width(11).Height(11).Border(0.8f, BorderColor)
                            .AlignCenter().AlignMiddle()
                            .Text(i.Verified ? "X" : "").FontSize(8).Bold();
                        table.Cell().Element(Cell).Text(i.Code ?? "");
                        table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(i.ProductDescription)
                            ? i.ProductName
                            : $"{i.ProductName} - {i.ProductDescription}");
                        table.Cell().Element(Cell).Text(i.Category ?? "");
                        table.Cell().Element(Cell).AlignRight().Text(i.SystemStock.ToString());
                        // Counted and difference only once verified; otherwise left blank to write in.
                        table.Cell().Element(Cell).AlignRight().Text(i.Verified ? i.Counted.ToString() : "");
                        var diff = table.Cell().Element(Cell).AlignRight();
                        if (i.Difference != 0)
                            diff.Text(i.Difference > 0 ? $"+{i.Difference}" : i.Difference.ToString())
                                .FontColor(i.Difference > 0 ? Green : Red).SemiBold();
                        else
                            diff.Text("");
                        table.Cell().Element(Cell).Text(i.Note ?? "");
                    }
                });

                // Totals only make sense once there are differences.
                if (s.WithDifference > 0)
                {
                    col.Item().PaddingTop(10).Text(t =>
                    {
                        t.Span($"Faltantes: {s.MissingUnits} u (${s.MissingValue:N2} a costo)").FontColor(Red);
                        t.Span("     ");
                        t.Span($"Sobrantes: {s.ExtraUnits} u (${s.ExtraValue:N2} a costo)").FontColor(Green);
                    });
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        })).GeneratePdf(path);
    }

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(HeaderBackground).BorderBottom(1, Unit.Point).BorderColor(BorderColor).PaddingVertical(3).PaddingHorizontal(2);

    private static IContainer Cell(IContainer c) =>
        c.BorderBottom(0.5f, Unit.Point).BorderColor(BorderColor).PaddingVertical(4).PaddingHorizontal(2);
}
