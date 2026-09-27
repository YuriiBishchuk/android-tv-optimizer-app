using TvOptimizer.Core.Audit;

namespace TvOptimizer.App.Pages;

public class AuditResultCell : ViewCell
{
    public AuditResultCell()
    {
        var nameLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 16
        };
        nameLabel.SetBinding(Label.TextProperty, "PackageName");

        var tierLabel = new Label
        {
            FontSize = 14
        };
        tierLabel.SetBinding(Label.TextProperty, new Binding("Tier", stringFormat: "TIER_{0}"));

        var detailsLabel = new Label
        {
            FontSize = 13,
            TextColor = Colors.Gray,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        detailsLabel.SetBinding(Label.TextProperty, "Details");

        var actionIcon = new Image
        {
            WidthRequest = 24,
            HeightRequest = 24,
            Source = "warning.png"
        };
        actionIcon.SetBinding(Image.IsVisibleProperty, "NeedsAction");

        var layout = new Grid
        {
            Padding = new Thickness(10, 5),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.3, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        layout.Add(nameLabel, 0, 0);
        layout.Add(tierLabel, 1, 0);
        layout.Add(detailsLabel, 0, 1);
        Grid.SetColumnSpan(detailsLabel, 2);
        layout.Add(actionIcon, 2, 0);

        View = layout;
    }
}
