using System.Xml.Linq;

namespace VSLoader.Tests;

public sealed class WorkspaceNameDialogVisualTests
{
    [Fact]
    public void Dialog_sizes_to_content_and_keeps_all_content_rows_auto_sized()
    {
        var path = TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "WorkspaceNameDialog.xaml");
        var document = XDocument.Load(path);
        var window = Assert.IsType<XElement>(document.Root);
        var presentation = window.Name.Namespace;

        Assert.Equal("Height", (string?)window.Attribute("SizeToContent"));
        Assert.Null(window.Attribute("Height"));

        var contentGrid = window
            .Descendants(presentation + "Grid")
            .Single(grid => grid.Elements(presentation + "Grid.RowDefinitions").Any(definitions =>
                definitions.Elements(presentation + "RowDefinition").Count() == 4));
        var rowHeights = contentGrid
            .Element(presentation + "Grid.RowDefinitions")!
            .Elements(presentation + "RowDefinition")
            .Select(row => (string?)row.Attribute("Height"))
            .ToList();

        Assert.Equal(["Auto", "Auto", "Auto", "Auto"], rowHeights);
    }
}
