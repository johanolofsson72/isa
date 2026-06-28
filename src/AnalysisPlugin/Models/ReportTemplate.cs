using System.Text.Json.Serialization;

namespace AnalysisPlugin.Models;

/// <summary>
/// Represents a report template configuration
/// </summary>
public class ReportTemplate
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "excel" or "pdf"

    [JsonPropertyName("style")]
    public string Style { get; set; } = string.Empty; // "corporate", "modern", "classic"

    [JsonPropertyName("templateFile")]
    public string TemplateFile { get; set; } = string.Empty;

    [JsonPropertyName("colors")]
    public TemplateColors Colors { get; set; } = new();

    [JsonPropertyName("fonts")]
    public TemplateFonts Fonts { get; set; } = new();

    [JsonPropertyName("layout")]
    public TemplateLayout Layout { get; set; } = new();

    [JsonPropertyName("components")]
    public List<TemplateComponent> Components { get; set; } = new();
}

/// <summary>
/// Template color scheme
/// </summary>
public class TemplateColors
{
    [JsonPropertyName("primary")]
    public string Primary { get; set; } = "#2E86AB";

    [JsonPropertyName("secondary")]
    public string Secondary { get; set; } = "#A23B72";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "#F18F01";

    [JsonPropertyName("background")]
    public string Background { get; set; } = "#FFFFFF";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "#333333";

    [JsonPropertyName("headerBackground")]
    public string HeaderBackground { get; set; } = "#F8F9FA";

    [JsonPropertyName("borderColor")]
    public string BorderColor { get; set; } = "#DEE2E6";
}

/// <summary>
/// Template font configuration
/// </summary>
public class TemplateFonts
{
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = "Segoe UI";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "Segoe UI";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "Consolas";

    [JsonPropertyName("headingSize")]
    public int HeadingSize { get; set; } = 16;

    [JsonPropertyName("bodySize")]
    public int BodySize { get; set; } = 11;

    [JsonPropertyName("dataSize")]
    public int DataSize { get; set; } = 10;
}

/// <summary>
/// Template layout configuration
/// </summary>
public class TemplateLayout
{
    [JsonPropertyName("margins")]
    public TemplateMargins Margins { get; set; } = new();

    [JsonPropertyName("headerHeight")]
    public int HeaderHeight { get; set; } = 80;

    [JsonPropertyName("footerHeight")]
    public int FooterHeight { get; set; } = 40;

    [JsonPropertyName("columnSpacing")]
    public int ColumnSpacing { get; set; } = 20;

    [JsonPropertyName("rowSpacing")]
    public int RowSpacing { get; set; } = 15;
}

/// <summary>
/// Template margins
/// </summary>
public class TemplateMargins
{
    [JsonPropertyName("top")]
    public int Top { get; set; } = 25;

    [JsonPropertyName("bottom")]
    public int Bottom { get; set; } = 25;

    [JsonPropertyName("left")]
    public int Left { get; set; } = 25;

    [JsonPropertyName("right")]
    public int Right { get; set; } = 25;
}

/// <summary>
/// Template component configuration
/// </summary>
public class TemplateComponent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "header", "table", "chart", "footer"

    [JsonPropertyName("position")]
    public TemplatePosition Position { get; set; } = new();

    [JsonPropertyName("style")]
    public Dictionary<string, object> Style { get; set; } = new();
}

/// <summary>
/// Component position
/// </summary>
public class TemplatePosition
{
    [JsonPropertyName("row")]
    public int Row { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
